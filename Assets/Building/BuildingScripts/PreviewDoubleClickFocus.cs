using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PreviewDoubleClickFocus : MonoBehaviour
{
    [Header("Click / Double-Click")]
    public float doubleClickThreshold = 0.28f;
    public float maxMoveForClick = 6f;

    [Header("Raycast (optional)")]
    public LayerMask hitLayer = ~0;
    public float raycastMaxDistance = 2000f;

    [Header("Framing")]
    public float distanceFactor = 1.4f;
    public float heightOffsetRatio = 0.25f;    // 바운즈 높이의 일부만큼 위로 목표점 오프셋
    [Tooltip("0이면 현재 FOV 유지")]
    public float targetFOV = 22f;

    [Header("Low-Angle (저각)")]
    [Range(0f, 1f)] public float lowAngleBias = 0.7f; // 0=기존, 1=강한 저각
    public float lowAngleFactor = 0.45f;               // radius 대비 아래로 당김
    public LayerMask groundLayer = ~0;
    public float groundClearance = 0.6f;

    [Header("Return / Tween")]
    public float focusDuration = 0.35f;
    public float returnDuration = 0.28f;

    [Header("Keys (Optional)")]
    public KeyCode cancelKey = KeyCode.Escape;
    public bool rightClickToCancel = true;

    [Header("Debug")]
    public bool verboseLog = false;
    // === Add to fields in BuildingPreviewFocus / PreviewDoubleClickFocus ===
    [Header("Gate to 'Placing' Only")]
    [Tooltip("설치 패널이 켜져 있을 때만 더블클릭 포커스가 동작합니다.")]
    public bool onlyWhileInstallPanelActive = true;

    [Tooltip("설치 패널(예: TileClickInstaller.buildingInstallPanel)을 여기에 넣어주세요. 비우면 항상 동작.")]
    public GameObject installPanelRef;

    [Tooltip("프리뷰가 아직 씬 루트(부모 없음)일 때만 유효하다고 간주합니다. 설치 완료 후(타일 자식) 자동 무시.")]
    public bool requirePreviewUnparented = true;

    [Tooltip("프리뷰 루트의 이름(Installer에서 생성). 기본값은 'BuildingPreviewParent'.")]
    public string previewRootName = "BuildingPreviewParent";

    Camera _cam;
    bool _isFocusing;
    bool _isFocused;
    Vector3 _camPosBefore;
    Quaternion _camRotBefore;
    float _fovBefore;

    float _lastClickTime = -999f;
    Vector2 _pressPos;
    bool _maybeClick;

    Transform _currentPreviewRoot; // "BuildingPreviewParent"
    Bounds _lastTargetBounds;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (!_cam) _cam = Camera.main;
    }

    void Update()
    {
        // Update() 맨 앞쪽에 아래 가드 추가
        if (onlyWhileInstallPanelActive)
        {
            if (!installPanelRef || !installPanelRef.activeInHierarchy)
            {
                // 설치 패널이 꺼져 있으면 전체 기능 비활성
                _isFocused = false; _isFocusing = false;
                return;
            }
        }

        _currentPreviewRoot = FindActivePreviewRoot();

        // 프리뷰가 사라지면 자동 복귀
        if (_isFocused && !_currentPreviewRoot)
        {
            StartReturn();
            return;
        }

        HandleMouseClicks();

        // 포커스 상태에서 취소/다른 곳 클릭 → 복귀
        if (_isFocused)
        {
            if (Input.GetKeyDown(cancelKey) || (rightClickToCancel && Input.GetMouseButtonDown(1)))
                StartReturn();

            if (Input.GetMouseButtonDown(0) && !IsPointerOnUI())
            {
                if (!IsPointerOverPreview())
                    StartReturn();
            }
        }
    }

    void HandleMouseClicks()
    {
        if (IsPointerOnUI()) return;

        if (Input.GetMouseButtonDown(0))
        {
            _pressPos = Input.mousePosition;
            _maybeClick = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (!_maybeClick) return;
            _maybeClick = false;
            if (Vector2.Distance(Input.mousePosition, _pressPos) > maxMoveForClick) return;

            float now = Time.unscaledTime;
            bool isDouble = (now - _lastClickTime) <= doubleClickThreshold;
            _lastClickTime = now;
            if (!isDouble) return;

            // 더블클릭 처리: 레이캐스트 우선
            if (!TryFocusByHit())
            {
                // 콜라이더/레이캐스트 실패 시: 화면 사각형 판정
                if (_currentPreviewRoot && TryGetWorldBounds(_currentPreviewRoot, out Bounds b))
                {
                    if (IsScreenPointInsideBoundsRect((Vector2)Input.mousePosition, b))
                    {
                        // facingDir 불명 → 프리뷰 정면(앞)으로 대체
                        Vector3 facingDir = _currentPreviewRoot.forward;
                        facingDir.y = 0f; if (facingDir.sqrMagnitude < 0.01f) facingDir = Vector3.forward;
                        StartFocus(_currentPreviewRoot, b, true, facingDir.normalized, null, false);
                        if (verboseLog) Debug.Log("[Focus] Raycast miss → ScreenRect hit → use root.forward.");
                    }
                    else if (verboseLog) Debug.Log("[Focus] ScreenRect miss.");
                }
            }
        }
    }

    bool TryFocusByHit()
    {
        if (!TryRaycast(out RaycastHit hit))
            return false;

        Transform root = ResolvePreviewRootFromHit(hit.transform) ?? _currentPreviewRoot;
        if (!root) return false;

        if (!TryGetWorldBounds(root, out Bounds b)) return false;

        // === 클릭한 면의 정면을 바라보도록: 표면 노멀 기반 facingDir ===
        Vector3 facingDir = ComputeFacingDirFromHit(root, b, hit);

        // 히트 포인트를 참조해 카메라 높이 보정에 도움을 줄 수 있음
        StartFocus(root, b, true, facingDir, hit, true);
        if (verboseLog) Debug.Log("[Focus] Raycast hit → face-normal facing.");
        return true;
    }

    /// <summary>
    /// 클릭한 면 정면 방향을 계산.
    /// 1순위: hit.normal(수평 성분 우선)  /  2순위: 바운즈 중심→히트 포인트 벡터
    /// </summary>
    Vector3 ComputeFacingDirFromHit(Transform previewRoot, Bounds bounds, RaycastHit hit)
    {
        Vector3 n = hit.normal;
        if (n.sqrMagnitude < 1e-5f) n = previewRoot.forward; // 비상
        // 수평 성분을 우선시해 정면 느낌 유지(저각은 별도 파이프라인에서 처리)
        Vector3 nXZ = Vector3.ProjectOnPlane(n, Vector3.up);
        if (nXZ.sqrMagnitude < 1e-4f)
        {
            // 거의 수직이면, 바운즈 중심에서 히트 지점으로의 방향으로 대체
            Vector3 fallback = (hit.point - bounds.center);
            fallback.y = 0f;
            if (fallback.sqrMagnitude < 1e-4f) fallback = previewRoot.forward;
            return fallback.normalized;
        }
        return nXZ.normalized;
    }

    void StartFocus(Transform previewRoot, Bounds b, bool hasFacing, Vector3 facingDir, RaycastHit? hitOpt, bool fromHit)
    {
        if (_isFocusing) return;

        _isFocusing = true;
        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;
        _fovBefore = _cam.fieldOfView;

        // === 타깃 중심 (약간 위로)
        Vector3 targetCenter = b.center + Vector3.up * (b.size.y * heightOffsetRatio);

        // === 정면(위치) 방향
        Vector3 dir = hasFacing ? facingDir : previewRoot.forward;
        dir.y = 0f; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward; dir.Normalize();

        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        float dist = Mathf.Max(1f, radius * distanceFactor);

        // 기본 기준점(정면에서 거리를 두고 약간 위)
        Vector3 basePos = targetCenter - dir * dist + Vector3.up * Mathf.Min(radius * 0.35f, 6f);

        // 저각: 아래로 내리기
        float desiredLowY = targetCenter.y - (radius * Mathf.Max(0f, lowAngleFactor));
        Vector3 lowPos = new Vector3(basePos.x, desiredLowY, basePos.z);
        Vector3 camTargetPos = Vector3.Lerp(basePos, lowPos, Mathf.Clamp01(lowAngleBias));

        // 지면 충돌 방지
        if (Physics.Raycast(new Vector3(camTargetPos.x, camTargetPos.y + 50f, camTargetPos.z),
                            Vector3.down, out var hitG, 200f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            float minY = hitG.point.y + Mathf.Max(0.05f, groundClearance);
            if (camTargetPos.y < minY) camTargetPos.y = minY;
        }

        // 히트 지점이 있으면, 살짝 히트 포인트를 향해 앵커 가중치를 더해 자연스런 정면 프레이밍
        Vector3 lookTarget = targetCenter;
        if (fromHit && hitOpt.HasValue)
        {
            Vector3 hp = hitOpt.Value.point;
            // 타깃 중심과 히트 포인트 사이 보간 (정면 강조)
            lookTarget = Vector3.Lerp(targetCenter, new Vector3(hp.x, targetCenter.y, hp.z), 0.25f);
        }

        Quaternion camTargetRot = Quaternion.LookRotation((lookTarget - camTargetPos).normalized, Vector3.up);
        float fovTarget = (targetFOV > 0f) ? targetFOV : _cam.fieldOfView;

        _lastTargetBounds = b;

        StopAllCoroutines();
        StartCoroutine(TweenCamera(_cam.transform.position, camTargetPos,
                                   _cam.transform.rotation, camTargetRot,
                                   _cam.fieldOfView, fovTarget, focusDuration, onComplete: () =>
                                   {
                                       _isFocusing = false;
                                       _isFocused = true;
                                   }));
    }

    void StartReturn()
    {
        if (!_isFocused && !_isFocusing) return;

        StopAllCoroutines();
        StartCoroutine(TweenCamera(_cam.transform.position, _camPosBefore,
                                   _cam.transform.rotation, _camRotBefore,
                                   _cam.fieldOfView, _fovBefore, returnDuration, onComplete: () =>
                                   {
                                       _isFocused = false;
                                       _isFocusing = false;
                                   }));
    }

    IEnumerator TweenCamera(Vector3 fromPos, Vector3 toPos,
                            Quaternion fromRot, Quaternion toRot,
                            float fromFov, float toFov,
                            float duration, System.Action onComplete = null)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
            float e = EaseInOutCubic(Mathf.Clamp01(t));

            _cam.transform.position = Vector3.LerpUnclamped(fromPos, toPos, e);
            _cam.transform.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, e);
            _cam.fieldOfView = Mathf.LerpUnclamped(fromFov, toFov, e);
            yield return null;
        }
        _cam.transform.position = toPos;
        _cam.transform.rotation = toRot;
        _cam.fieldOfView = toFov;
        onComplete?.Invoke();
    }



    bool TryRaycast(out RaycastHit hit)
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out hit, raycastMaxDistance, hitLayer, QueryTriggerInteraction.Ignore);
    }

    bool IsPointerOnUI()
    {
        if (!EventSystem.current) return false;
        if (EventSystem.current.IsPointerOverGameObject()) return true;
        for (int i = 0; i < Input.touchCount; i++)
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
        return false;
    }

    bool IsPointerOverPreview()
    {
        // 1) 레이캐스트로 우선 확인
        if (TryRaycast(out RaycastHit hit))
        {
            var root = ResolvePreviewRootFromHit(hit.transform);
            if (root && _currentPreviewRoot) return root == _currentPreviewRoot;
        }
        // 2) 실패 시: 화면 투영 사각형 판정
        if (_currentPreviewRoot && TryGetWorldBounds(_currentPreviewRoot, out Bounds b))
            return IsScreenPointInsideBoundsRect((Vector2)Input.mousePosition, b);

        return false;
    }

    Transform ResolvePreviewRootFromHit(Transform t)
    {
        for (var cur = t; cur != null; cur = cur.parent)
            if (cur.name == "BuildingPreviewParent") return cur;
        return null;
    }

    // 기존 FindActivePreviewRoot() 교체
Transform FindActivePreviewRoot()
{
    // 씬 루트부터 순회
    var scene = gameObject.scene;
    if (!scene.IsValid()) return null;

    Transform best = null;
    int bestDepth = -1;

    var roots = scene.GetRootGameObjects();
    foreach (var go in roots)
        CollectPreview(go.transform, ref best, ref bestDepth, 0);

    // 설치 완료 후(타일 자식) 프리뷰는 무시
    if (requirePreviewUnparented && best && best.parent != null)
        return null;

    return best;
}

void CollectPreview(Transform t, ref Transform best, ref int bestDepth, int depth)
{
    if (!t.gameObject.activeInHierarchy) return;
    if (t.name == previewRootName)
    {
        // 가장 최근/가장 깊은(대개 가장 나중에 생성된) 것을 선택
        if (depth > bestDepth) { best = t; bestDepth = depth; }
    }
    for (int i = 0; i < t.childCount; i++)
        CollectPreview(t.GetChild(i), ref best, ref bestDepth, depth + 1);
}


    bool TryGetWorldBounds(Transform root, out Bounds b)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return true;
        }
        var cols = root.GetComponentsInChildren<Collider>(true);
        if (cols != null && cols.Length > 0)
        {
            b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return true;
        }
        b = new Bounds(root.position, Vector3.one);
        return false;
    }

    bool IsScreenPointInsideBoundsRect(Vector2 screenPt, Bounds worldBounds)
    {
        // 바운즈 8코너를 스크린으로 투영 → AABB 포함 판정
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        Vector3[] corners = new Vector3[8];
        corners[0] = c + new Vector3(e.x, e.y, e.z);
        corners[1] = c + new Vector3(e.x, e.y, -e.z);
        corners[2] = c + new Vector3(e.x, -e.y, e.z);
        corners[3] = c + new Vector3(e.x, -e.y, -e.z);
        corners[4] = c + new Vector3(-e.x, e.y, e.z);
        corners[5] = c + new Vector3(-e.x, e.y, -e.z);
        corners[6] = c + new Vector3(-e.x, -e.y, e.z);
        corners[7] = c + new Vector3(-e.x, -e.y, -e.z);

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < 8; i++)
        {
            Vector3 sp = _cam.WorldToScreenPoint(corners[i]);
            if (sp.z < 0f) continue;
            min = Vector2.Min(min, (Vector2)sp);
            max = Vector2.Max(max, (Vector2)sp);
        }

        if (min.x > max.x || min.y > max.y) return false;
        return (screenPt.x >= min.x && screenPt.x <= max.x &&
                screenPt.y >= min.y && screenPt.y <= max.y);
    }

    static float EaseInOutCubic(float x)
       => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
}
