using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

using UnityEngine;




[DisallowMultipleComponent]
public class PreviewDoubleClickFocus : MonoBehaviour
{
    [Header("Click / Double-Click")]
    public float doubleClickThreshold = 0.28f;
    public float maxMoveForClick = 6f;

    [Header("Framing (기본값)")]
    [Tooltip("바운즈 반지름 대비 카메라 거리 배율 (건물별 오버라이드 가능)")]
    public float distanceFactor = 1.4f;
    [Tooltip("바운즈 높이의 일부만큼 타깃 중심을 위로 올림")]
    public float heightOffsetRatio = 0.25f;
    [Tooltip("0이면 현재 FOV 유지, >0이면 이 값으로 전환 (건물별 오버라이드 가능)")]
    public float targetFOV = 22f;

    [Header("Low-Angle (저각)")]
    [Range(0f, 1f)] public float lowAngleBias = 0.7f;
    public float lowAngleFactor = 0.45f;
    public LayerMask groundLayer = ~0;
    public float groundClearance = 0.6f;

    [Header("Return / Tween")]
    public float focusDuration = 0.35f;
    public float returnDuration = 0.28f;

    [Header("Cancel Keys")]
    public KeyCode cancelKey = KeyCode.Escape;
    public bool rightClickToCancel = true;
    [Tooltip("포커스 상태에서 좌클릭으로 바로 복귀할지 (더블클릭 취소 충돌 방지 위해 기본 false 권장)")]
    public bool leftClickToCancelWhileFocused = false;

    [Header("Gate to 'Placing' Only")]
    [Tooltip("설치 패널이 켜져 있을 때만 더블클릭 포커스 동작")]
    public bool onlyWhileInstallPanelActive = true;
    [Tooltip("설치 패널(예: TileClickInstaller.buildingInstallPanel)")]
    public GameObject installPanelRef;
    [Tooltip("프리뷰가 씬 루트(부모 없음)일 때만 유효. 설치(부모 생김) 후 자동 무시")]
    public bool requirePreviewUnparented = true;

    [Header("Preview Root")]
    [Tooltip("프리뷰 루트 이름(Installer에서 생성). 기본 'BuildingPreviewParent'")]
    public string previewRootName = "BuildingPreviewParent";

    [Header("Debug")]
    public bool verboseLog = false;
    [Header("Occlusion (시야 가림 회피)")]
    public LayerMask occluderMask = ~0;
    public float camCollisionRadius = 0.6f;
    public float maxRaise = 8f;
    public float raiseStep = 1f;
    public int azimuthSteps = 16;
    public float azimuthStepDeg = 12f;
    public float retreatMul = 1.15f;

    bool LineOfSightClear(Vector3 from, Vector3 to, Transform ignoreRoot)
    {
        Vector3 dir = to - from; float dist = dir.magnitude;
        if (dist < 1e-3f) return true; dir /= dist;
        if (Physics.SphereCast(from, camCollisionRadius, dir, out var hit, dist,
                               occluderMask, QueryTriggerInteraction.Ignore))
        {
            if (ignoreRoot && hit.collider && hit.collider.transform.root == ignoreRoot) return true;
            return false;
        }
        return true;
    }

    Vector3 FindUnobstructedPos(Vector3 center, Vector3 basePos, Transform ignoreRoot, float targetDist)
    {
        if (LineOfSightClear(basePos, center, ignoreRoot)) return basePos;

        // 위로
        for (float h = raiseStep; h <= maxRaise; h += raiseStep)
        {
            var p = basePos + new Vector3(0f, h, 0f);
            if (LineOfSightClear(p, center, ignoreRoot)) return p;
        }

        // 좌/우 방위각 + 약간 뒤로
        Vector3 flat = basePos - center; flat.y = 0f;
        float r = Mathf.Max(1f, flat.magnitude);
        for (int i = 1; i <= azimuthSteps; i++)
        {
            float ang = azimuthStepDeg * i;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 rotated = Quaternion.AngleAxis(ang * side, Vector3.up) * flat;

                var cand = center + rotated.normalized * r;
                cand.y = basePos.y;
                if (LineOfSightClear(cand, center, ignoreRoot)) return cand;

                var retreat = center + rotated.normalized * (r * retreatMul);
                retreat.y = cand.y;
                if (LineOfSightClear(retreat, center, ignoreRoot)) return retreat;
            }
        }
        return basePos;
    }

    Camera _cam;
    bool _isFocusing;
    bool _isFocused;
    Vector3 _camPosBefore;
    Quaternion _camRotBefore;
    float _fovBefore;

    float _lastClickTime = -999f;
    Vector2 _pressPos;
    bool _maybeClick;

    // 포커스 직후 잠깐 취소 입력 무시(더블클릭 2번째 클릭으로 즉시 취소되는 것 방지)
    float _cancelIgnoreUntil = 0f;
    const float CANCEL_GRACE = 0.15f;

    Transform _currentPreviewRoot;
    Bounds _lastTargetBounds;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (!_cam) _cam = Camera.main;
    }

    void Update()
    {
        // 설치 패널이 있을 때는 "활성"이면 무조건 동작
        if (onlyWhileInstallPanelActive)
        {
            if (!installPanelRef || !installPanelRef.activeInHierarchy)
            {
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

        // 포커스 상태 취소
        if (_isFocused)
        {
            if (Time.unscaledTime >= _cancelIgnoreUntil)
            {
                if (Input.GetKeyDown(cancelKey) || (rightClickToCancel && Input.GetMouseButtonDown(1)))
                    StartReturn();

                if (leftClickToCancelWhileFocused && Input.GetMouseButtonDown(0) && !IsPointerOnUI())
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

            // 어디서 더블클릭하든: 현재 프리뷰로 포커스 (레이캐스트 없음)
            if (_currentPreviewRoot && TryGetWorldBounds(_currentPreviewRoot, out Bounds b))
            {
                FocusToCurrentPreview(_currentPreviewRoot, b);
            }
            else if (verboseLog) Debug.LogWarning("[Focus] No active preview or bounds.");
        }
    }

    // === 현재 프리뷰로 포커싱 (BuildingData의 '건물 로컬 기준 방향' 사용) ===
    void FocusToCurrentPreview(Transform root, Bounds b)
    {
        // BuildingData에서 방향/오버라이드
        Vector3 viewDirWorld;
        bool topView;
        float dfOverride, fovOverride;

        if (!TryGetViewFromBuildingData(root, out viewDirWorld, out topView, out dfOverride, out fovOverride))
        {
            // BD 없으면 기본 Front(=root.forward 기준) 사용
            viewDirWorld = root.forward;
            topView = false;
            dfOverride = 0f;
            fovOverride = 0f;
        }

        StartFocus_WithOverrides(root, b, viewDirWorld, topView, dfOverride, fovOverride);
    }

    // BuildingData 해석: "건물 로컬 기준" 방향을 월드로
    bool TryGetViewFromBuildingData(Transform root, out Vector3 viewDirWorld, out bool topView,
                                    out float distFactorOverride, out float fovOverride)
    {
        viewDirWorld = Vector3.zero; topView = false;
        distFactorOverride = 0f; fovOverride = 0f;
        if (!root) return false;

        var bd = root.GetComponentInChildren<BuildingData>(true);
        if (!bd) return false;

        switch (bd.preferredView)
        {
            case BuildingData.PreferredView.Front: viewDirWorld = root.forward; break;
            case BuildingData.PreferredView.Back: viewDirWorld = -root.forward; break;
            case BuildingData.PreferredView.Left: viewDirWorld = -root.right; break;
            case BuildingData.PreferredView.Right: viewDirWorld = root.right; break;
            case BuildingData.PreferredView.Top: topView = true; break;
        }

        if (!topView)
        {
            viewDirWorld.y = 0f;
            if (viewDirWorld.sqrMagnitude < 1e-5f) viewDirWorld = Vector3.forward;
            viewDirWorld.Normalize();
        }

        distFactorOverride = bd.cameraDistanceFactorOverride;
        fovOverride = bd.cameraFOVOverride;
        return true;
    }

    // 실제 이동/회전 트윈 (프리뷰는 회전시키지 않음)
    void StartFocus_WithOverrides(Transform previewRoot, Bounds b,
                                  Vector3 viewDirWorld, bool topView,
                                  float distFactorOverride, float fovOverride)
    {
        // 재진입 허용: 현재 트윈 중이어도 끊고 새 포커스
        StopAllCoroutines();

        _isFocusing = true;
        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;
        _fovBefore = _cam.fieldOfView;

        // 타깃 중심(약간 위)
        Vector3 targetCenter = b.center + Vector3.up * (b.size.y * heightOffsetRatio);

        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        float df = (distFactorOverride > 0f) ? distFactorOverride : distanceFactor;
        float dist = Mathf.Max(1f, radius * df);

        Vector3 camTargetPos;
        Quaternion camTargetRot;
        float fovTarget;

        if (topView)
        {
            // 건물 위(Local Up)에서 내려다보기
            Vector3 upDir = previewRoot.up; // 건물 로컬 up을 기준
            if (upDir.sqrMagnitude < 1e-5f) upDir = Vector3.up;
            camTargetPos = targetCenter + upDir.normalized * Mathf.Max(radius * df, 3f);
            camTargetRot = Quaternion.LookRotation((targetCenter - camTargetPos).normalized, Vector3.up);
            fovTarget = (fovOverride > 0f) ? fovOverride : (targetFOV > 0f ? targetFOV : _cam.fieldOfView);
        }
        else
        {
            // Front/Back/Left/Right: "건물 로컬 기준 방향"에서 떨어져 바라봄
            Vector3 dir = viewDirWorld; // 이미 y=0 정규화됨
            Vector3 basePos = targetCenter - dir * dist + Vector3.up * Mathf.Min(radius * 0.35f, 6f);

            // 저각 보정
            float desiredLowY = targetCenter.y - (radius * Mathf.Max(0f, lowAngleFactor));
            Vector3 lowPos = new Vector3(basePos.x, desiredLowY, basePos.z);
            camTargetPos = Vector3.Lerp(basePos, lowPos, Mathf.Clamp01(lowAngleBias));

            // 지면 충돌 방지
            if (Physics.Raycast(new Vector3(camTargetPos.x, camTargetPos.y + 50f, camTargetPos.z),
                                Vector3.down, out var hitG, 200f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                float minY = hitG.point.y + Mathf.Max(0.05f, groundClearance);
                if (camTargetPos.y < minY) camTargetPos.y = minY;
            }

            camTargetRot = Quaternion.LookRotation((targetCenter - camTargetPos).normalized, Vector3.up);
            fovTarget = (fovOverride > 0f) ? fovOverride : (targetFOV > 0f ? targetFOV : _cam.fieldOfView);
        }

        _lastTargetBounds = b;

        // 더블클릭 2번째 클릭이 즉시 취소로 먹히지 않도록 그레이스 타임
        _cancelIgnoreUntil = Time.unscaledTime + CANCEL_GRACE;

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

    // ===== Helper =====

    bool IsPointerOnUI()
    {
        if (!EventSystem.current) return false;
        if (EventSystem.current.IsPointerOverGameObject()) return true;
        for (int i = 0; i < Input.touchCount; i++)
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
        return false;
    }

    Transform FindActivePreviewRoot()
    {
        var scene = gameObject.scene;
        if (!scene.IsValid()) return null;

        // 1) 마커 기반으로 가장 최근(seq 최대) 선택
        BuildingPreviewMarker[] markers =
            Object.FindObjectsByType<BuildingPreviewMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        BuildingPreviewMarker best = null;
        int bestSeq = int.MinValue;

        foreach (var m in markers)
        {
            if (!m) continue;
            var tr = m.transform;
            if (!tr.gameObject.activeInHierarchy) continue;
            if (requirePreviewUnparented && tr.parent != null) continue;

            if (m.seq > bestSeq)
            {
                best = m;
                bestSeq = m.seq;
            }
        }

        if (best) return best.transform;

        // 2) (백업) 이름 기반으로 찾되, 찾은 즉시 마커를 붙여 다음부터는 1)로 잡히게
        Transform newestByName = null;
        int newestInstanceId = int.MinValue;

        var roots = scene.GetRootGameObjects();
        foreach (var go in roots)
        {
            var tr = FindFirstByNameDeep(go.transform, previewRootName);
            if (!tr) continue;
            if (!tr.gameObject.activeInHierarchy) continue;
            if (requirePreviewUnparented && tr.parent != null) continue;

            // 가장 최근 객체로 근사: instanceID 큰 쪽이 보통 더 최근
            int id = tr.gameObject.GetInstanceID();
            if (id > newestInstanceId)
            {
                newestInstanceId = id;
                newestByName = tr;
            }
        }

        if (newestByName)
        {
            // 다음부터는 마커 경로로 안정적으로 잡히게 자동 부착
            var marker = newestByName.GetComponent<BuildingPreviewMarker>();
            if (!marker) marker = newestByName.gameObject.AddComponent<BuildingPreviewMarker>();
            return newestByName;
        }

        return null;
    }

    Transform FindFirstByNameDeep(Transform root, string name)
    {
        if (!root) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindFirstByNameDeep(root.GetChild(i), name);
            if (r) return r;
        }
        return null;
    }


    void CollectPreview(Transform t, ref Transform best, ref int bestDepth, int depth)
    {
        if (!t.gameObject.activeInHierarchy) return;
        if (t.name == previewRootName)
        {
            if (depth > bestDepth) { best = t; bestDepth = depth; }
        }
        for (int i = 0; i < t.childCount; i++)
            CollectPreview(t.GetChild(i), ref best, ref bestDepth, depth + 1);
    }

    // 바운즈: Renderer → Collider → fallback
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

    static float EaseInOutCubic(float x)
        => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
}

