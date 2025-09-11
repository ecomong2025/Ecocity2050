using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[DisallowMultipleComponent]
public class PreviewFocusSimple : MonoBehaviour
{
    [Header("더블클릭")]
    public float doubleClickThreshold = 0.28f;
    public float maxMoveForClick = 6f;

    [Header("프레이밍 기본값")]
    public float distanceFactor = 1.4f;
    public float heightOffsetRatio = 0.25f;   // 타깃 바운즈 높이의 일부만큼 위로
    [Tooltip("0이면 현재 FOV 유지")]
    public float targetFOV = 22f;

    [Header("저각 옵션")]
    [Range(0f, 1f)] public float lowAngleBias = 0.7f;
    public float lowAngleFactor = 0.45f;      // radius 대비 아래로 당김
    public LayerMask groundLayer = ~0;
    public float groundClearance = 0.6f;

    [Header("트윈")]
    public float focusDuration = 0.35f;
    public float returnDuration = 0.28f;

    [Header("패널 게이트")]
    public GameObject installPanelRef;        // 설치 패널(켜져 있을 때만 동작시키고 싶으면 할당)
    public bool onlyWhileInstallPanelActive = true;

    [Header("취소 입력")]
    public KeyCode cancelKey = KeyCode.Escape;
    public bool rightClickToCancel = true;

    [Header("디버그")]
    public bool verboseLog = false;

    Camera _cam;
    bool _isFocusing;
    bool _isFocused;
    Vector3 _posBefore;
    Quaternion _rotBefore;
    float _fovBefore;

    float _lastClickTime = -999f;
    Vector2 _pressPos;
    bool _maybeClick;
    float _cancelIgnoreUntil;
    const float CANCEL_GRACE = 0.15f;

    TileClickInstaller _installer;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (!_cam) _cam = Camera.main;
        _installer = FindObjectOfType<TileClickInstaller>();
    }

    void Update()
    {
        if (onlyWhileInstallPanelActive)
        {
            if (!installPanelRef || !installPanelRef.activeInHierarchy) return;
        }
        if (_installer == null) return;
        if (Time.unscaledTime < _cancelIgnoreUntil) return; // ← 더블클릭 직후 잠깐(쿨다운) 입력 막기

        // 프리뷰 사라지면 자동 복귀
        if (_isFocused && _installer.CurrentPreviewRoot == null)
        {
            StartReturn();
            return;
        }

        // 입력
        HandleMouse();

        if (_isFocused && Time.unscaledTime >= _cancelIgnoreUntil)
        {
            if (Input.GetKeyDown(cancelKey) || (rightClickToCancel && Input.GetMouseButtonDown(1)))
                StartReturn();
        }
    }

    void HandleMouse()
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

            // 더블클릭: "현재 프리뷰" 기준으로 바로 포커스
            var root = _installer.CurrentPreviewRoot;
            var bd = _installer.CurrentBuildingData;
            if (!root || !bd) return;

            if (!TryGetWorldBounds(root, out Bounds b)) return;

            StartFocus(root, b, bd);
        }
    }

    void StartFocus(Transform root, Bounds b, BuildingData bd)
    {
        StopAllCoroutines();

        _isFocusing = true;
        _posBefore = _cam.transform.position;
        _rotBefore = _cam.transform.rotation;
        _fovBefore = _cam.fieldOfView;

        // 타깃 중심(약간 위)
        Vector3 center = b.center + Vector3.up * (b.size.y * heightOffsetRatio);

        // 거리/FOV 오버라이드
        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        float df = (bd.cameraDistanceFactorOverride > 0f) ? bd.cameraDistanceFactorOverride : distanceFactor;
        float dist = Mathf.Max(1f, radius * df);
        float fovTarget = (bd.cameraFOVOverride > 0f) ? bd.cameraFOVOverride
                          : (targetFOV > 0f ? targetFOV : _cam.fieldOfView);

        // 방향: 건물 "로컬 축" 기준
        Vector3 camPos;
        Quaternion camRot;

        if (bd.preferredView == BuildingData.PreferredView.Top)
        {
            Vector3 upDir = root.up; if (upDir.sqrMagnitude < 1e-5f) upDir = Vector3.up;
            camPos = center + upDir.normalized * Mathf.Max(radius * df, 3f);
            camRot = Quaternion.LookRotation((center - camPos).normalized, Vector3.up);
        }
        else
        {
            Vector3 dir = root.forward; // 기본 Front
            switch (bd.preferredView)
            {
                case BuildingData.PreferredView.Back: dir = -root.forward; break;
                case BuildingData.PreferredView.Left: dir = -root.right; break;
                case BuildingData.PreferredView.Right: dir = root.right; break;
                    // Front는 위의 기본값
            }
            dir.y = 0f; if (dir.sqrMagnitude < 1e-5f) dir = Vector3.forward; dir.Normalize();

            Vector3 basePos = center - dir * dist + Vector3.up * Mathf.Min(radius * 0.35f, 6f);

            // 저각(원하면 0으로 꺼도 됨)
            float desiredLowY = center.y - (radius * Mathf.Max(0f, lowAngleFactor));
            Vector3 lowPos = new Vector3(basePos.x, desiredLowY, basePos.z);
            camPos = Vector3.Lerp(basePos, lowPos, Mathf.Clamp01(lowAngleBias));

            // 지면 방지
            if (Physics.Raycast(new Vector3(camPos.x, camPos.y + 50f, camPos.z),
                                Vector3.down, out var hitG, 200f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                float minY = hitG.point.y + Mathf.Max(0.05f, groundClearance);
                if (camPos.y < minY) camPos.y = minY;
            }

            camRot = Quaternion.LookRotation((center - camPos).normalized, Vector3.up);
        }

        if (verboseLog) Debug.Log($"[Focus] {bd.preferredView} df={df}, fov={fovTarget}");

        _cancelIgnoreUntil = Time.unscaledTime + CANCEL_GRACE;

        StartCoroutine(TweenCamera(_cam.transform.position, camPos,
                                   _cam.transform.rotation, camRot,
                                   _cam.fieldOfView, fovTarget, focusDuration, () =>
                                   {
                                       _isFocusing = false;
                                       _isFocused = true;
                                   }));
    }

    void StartReturn()
    {
        StopAllCoroutines();
        StartCoroutine(TweenCamera(_cam.transform.position, _posBefore,
                                   _cam.transform.rotation, _rotBefore,
                                   _cam.fieldOfView, _fovBefore, returnDuration, () =>
                                   {
                                       _isFocused = false;
                                       _isFocusing = false;
                                   }));
    }

    IEnumerator TweenCamera(Vector3 fromPos, Vector3 toPos,
                            Quaternion fromRot, Quaternion toRot,
                            float fromFov, float toFov,
                            float duration, System.Action onComplete)
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
        _cam.transform.SetPositionAndRotation(toPos, toRot);
        _cam.fieldOfView = toFov;
        onComplete?.Invoke();
    }

    // ───── helpers ─────
    bool IsPointerOnUI()
    {
        if (!EventSystem.current) return false;
        if (EventSystem.current.IsPointerOverGameObject()) return true;
        for (int i = 0; i < Input.touchCount; i++)
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
        return false;
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

    static float EaseInOutCubic(float x)
        => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
}
