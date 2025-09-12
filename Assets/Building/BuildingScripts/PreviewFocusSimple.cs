using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[DisallowMultipleComponent]
public class PreviewFocusSimple : MonoBehaviour
{
    [Header("占쏙옙占쏙옙클占쏙옙")]
    public float doubleClickThreshold = 0.28f;
    public float maxMoveForClick = 6f;
    [Header("占쏙옙 占쏙옙占쏙옙 확占쏙옙(카占쌨띰옙占쏙옙占싹뤄옙 占쏙옙占쏙옙)")]
    [Tooltip("占쏙옙표 占신몌옙 占쏙옙占� 占쌍쇽옙 占쏙옙 占쏙옙占� (占쏙옙占쏙옙占쏙옙占쏙옙 占쏙옙 占쏙옙占쏙옙占쏙옙)")]
    public float zoomMinFactor = 0.35f;   // 占쏙옙: 0.35 占쏙옙 占쏙옙 占쏙옙占쏙옙占쏙옙
    [Tooltip("占쏙옙표 占신몌옙 占쏙옙占� 占쌍댐옙 占쏙옙 占쏙옙占� (클占쏙옙占쏙옙 占쏙옙 占쌍몌옙)")]
    public float zoomMaxFactor = 3.0f;    // 占쏙옙: 3.0  占쏙옙 占쏙옙 占쌍몌옙
    [Tooltip("占쏙옙크占쏙옙 占싸곤옙占쏙옙 占쏙옙占�(1=占쌓댐옙占�)")]
    public float zoomSpeedMul = 1.0f;     // 占쏙옙: 1.25 占쏙옙 占쏙옙크占쏙옙 占쏙옙 占쏙옙占쏙옙占쏙옙

    [Header("占쏙옙占쏙옙占싱뱄옙 占썩본占쏙옙")]
    public float distanceFactor = 1.4f;
    public float heightOffsetRatio = 0.25f;   // 타占쏙옙 占쌕울옙占쏙옙 占쏙옙占쏙옙占쏙옙 占싹부몌옙큼 占쏙옙占쏙옙
    [Tooltip("0占싱몌옙 占쏙옙占쏙옙 FOV 占쏙옙占쏙옙")]
    public float targetFOV = 22f;

    [Header("占쏙옙占쏙옙 占심쇽옙")]
    [Range(0f, 1f)] public float lowAngleBias = 0.7f;
    public float lowAngleFactor = 0.45f;      // radius 占쏙옙占� 占싣뤄옙占쏙옙 占쏙옙占�
    public LayerMask groundLayer = ~0;
    public float groundClearance = 0.6f;

    [Header("트占쏙옙")]
    public float focusDuration = 0.35f;
    public float returnDuration = 0.28f;

    [Header("占싻놂옙 占쏙옙占쏙옙트")]
    public GameObject installPanelRef;        // 占쏙옙치 占싻놂옙(占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쌜쏙옙키占쏙옙 占쏙옙占쏙옙占쏙옙 占쌀댐옙)
    public bool onlyWhileInstallPanelActive = true;

    [Header("취소 입력")]
    public KeyCode cancelKey = KeyCode.Escape;
    // 우클릭으로 취소하던 동작을 제거했습니다.

    [Header("占쏙옙占쏙옙占�")]
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
        if (Time.unscaledTime < _cancelIgnoreUntil) return; // 占쏙옙 占쏙옙占쏙옙클占쏙옙 占쏙옙占쏙옙 占쏙옙占�(占쏙옙牟占�) 占쌉뤄옙 占쏙옙占쏙옙

        // 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙占� 占쌘듸옙 占쏙옙占쏙옙
        if (_isFocused && _installer.CurrentPreviewRoot == null)
        {
            StartReturn();
            return;
        }

        // 占쌉뤄옙
        HandleMouse();

        if (_isFocused && Time.unscaledTime >= _cancelIgnoreUntil)
        {
            if (Input.GetKeyDown(cancelKey))
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

            // 占쏙옙占쏙옙클占쏙옙: "占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙" 占쏙옙占쏙옙占쏙옙占쏙옙 占쌕뤄옙 占쏙옙커占쏙옙
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

        // 타占쏙옙 占쌩쏙옙(占썅간 占쏙옙)
        Vector3 center = b.center + Vector3.up * (b.size.y * heightOffsetRatio);

        // 占신몌옙/FOV 占쏙옙占쏙옙占쏙옙占싱듸옙
        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        float df = (bd.cameraDistanceFactorOverride > 0f) ? bd.cameraDistanceFactorOverride : distanceFactor;
        float dist = Mathf.Max(1f, radius * df);
        // 占쏙옙 占쏙옙占쏙옙/占쌈듸옙 확占쏙옙 (CameraScaler 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙타占쏙옙 占쏙옙占쏙옙)
        var scaler = FindObjectOfType<CameraScaler>();
        if (scaler)
        {
            scaler.minDistance = Mathf.Min(scaler.minDistance, dist * zoomMinFactor);
            scaler.maxDistance = Mathf.Max(scaler.maxDistance, dist * zoomMaxFactor);
            scaler.zoomSpeed = Mathf.Max(0.01f, scaler.zoomSpeed * zoomSpeedMul);
        }

        float fovTarget = (bd.cameraFOVOverride > 0f) ? bd.cameraFOVOverride
                          : (targetFOV > 0f ? targetFOV : _cam.fieldOfView);

        // 占쏙옙占쏙옙: 占실뱄옙 "占쏙옙占쏙옙 占쏙옙" 占쏙옙占쏙옙
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
            Vector3 dir = root.forward; // 占썩본 Front
            switch (bd.preferredView)
            {
                case BuildingData.PreferredView.Back: dir = -root.forward; break;
                case BuildingData.PreferredView.Left: dir = -root.right; break;
                case BuildingData.PreferredView.Right: dir = root.right; break;
                    // Front占쏙옙 占쏙옙占쏙옙 占썩본占쏙옙
            }
            dir.y = 0f; if (dir.sqrMagnitude < 1e-5f) dir = Vector3.forward; dir.Normalize();

            Vector3 basePos = center - dir * dist + Vector3.up * Mathf.Min(radius * 0.35f, 6f);

            // 占쏙옙占쏙옙(占쏙옙占싹몌옙 0占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙)
            float desiredLowY = center.y - (radius * Mathf.Max(0f, lowAngleFactor));
            Vector3 lowPos = new Vector3(basePos.x, desiredLowY, basePos.z);
            camPos = Vector3.Lerp(basePos, lowPos, Mathf.Clamp01(lowAngleBias));

            // 占쏙옙占쏙옙 占쏙옙占쏙옙
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

    // 占쏙옙占쏙옙占쏙옙占쏙옙占쏙옙 helpers 占쏙옙占쏙옙占쏙옙占쏙옙占쏙옙
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
