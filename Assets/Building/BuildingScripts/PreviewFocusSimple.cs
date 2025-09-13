using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[DisallowMultipleComponent]
public class PreviewFocusSimple : MonoBehaviour
{
    [Header("더블클릭 설정")]
    public float doubleClickThreshold = 0.28f;
    public float maxMoveForClick = 6f;

    [Header("미리보기 포커스 설정 (설치 패널 사용 중에만 활성화 권장)")]
    [Tooltip("카메라 최소 줌 배율(작을수록 더 가깝게)")]
    public float zoomMinFactor = 0.35f;
    [Tooltip("카메라 최대 줌 배율(클수록 더 멀리)")]
    public float zoomMaxFactor = 3.0f;
    [Tooltip("줌 속도 곱셈 계수(1 = 기본)")]
    public float zoomSpeedMul = 1.0f;

    [Header("포커스 기본값")]
    public float distanceFactor = 1.4f;
    public float heightOffsetRatio = 0.25f;   // 바운드 상단에서 카메라 높이 오프셋 비율
    [Tooltip("0이면 카메라 FOV 그대로 사용")]
    public float targetFOV = 22f;

    [Header("시점 보정")]
    [Range(0f, 1f)] public float lowAngleBias = 0.7f;
    public float lowAngleFactor = 0.45f;      // 낮은 각도 보정용 반지름 비율
    public LayerMask groundLayer = ~0;
    public float groundClearance = 0.6f;

    [Header("시간")]
    public float focusDuration = 0.35f;
    public float returnDuration = 0.28f;

    [Header("설치 패널 연동")]
    public GameObject installPanelRef;        // 설치 패널 참조 (활성화 여부로 동작 제어 가능)
    public bool onlyWhileInstallPanelActive = true;

    [Header("취소 입력")]
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("로그")]
    public bool verboseLog = false;

    Camera _cam;
    bool _isFocusing;
    bool _isFocused;
    Vector3 _posBefore;
    Quaternion _rotBefore;
    float _fovBefore;
    CameraScaler _scalerRef; // CameraScaler 참조 (포커스 중 입력 충돌 방지용)

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
        _scalerRef = FindObjectOfType<CameraScaler>();
    }

    void Update()
    {
        if (onlyWhileInstallPanelActive)
        {
            if (!installPanelRef || !installPanelRef.activeInHierarchy) return;
        }
        if (_installer == null) return;
        if (Time.unscaledTime < _cancelIgnoreUntil) return; // 짧은 시간 동안 취소 입력 무시

        // 스크롤이 들어오면 무조건 CameraScaler 기능으로 복귀
        float scrollImmediate = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollImmediate) > 0.0001f)
        {
            // 즉시 CameraScaler 활성화
            if (_scalerRef != null && !_scalerRef.enabled) _scalerRef.enabled = true;
            // 포커스 중이면 복귀 시작
            if (_isFocused || _isFocusing)
            {
                StartReturn();
                return;
            }
            // 포커스 중이 아니면 그냥 바로 반환(입력은 CameraScaler가 처리)
            return;
        }

        // 포커스가 유지 중인데 미리보기 루트가 사라지면 복귀
        if (_isFocused && _installer.CurrentPreviewRoot == null)
        {
            StartReturn();
            return;
        }

        // 입력 처리
        HandleMouse();

        // (스크롤 처리는 위에서 즉시 처리)

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

            // 더블클릭: 현재 설치 미리보기의 루트와 빌딩 데이터를 사용하여 포커스 시작
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

        // 포커스 시작 시 CameraScaler 비활성화하여 입력 충돌 방지
        if (_scalerRef != null) _scalerRef.enabled = false;

        _isFocusing = true;
        _posBefore = _cam.transform.position;
        _rotBefore = _cam.transform.rotation;
        _fovBefore = _cam.fieldOfView;

        // 바운드 중심(상단 오프셋 포함)
        Vector3 center = b.center + Vector3.up * (b.size.y * heightOffsetRatio);

        // 카메라 거리 및 FOV 계산
        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        float df = (bd.cameraDistanceFactorOverride > 0f) ? bd.cameraDistanceFactorOverride : distanceFactor;
        float dist = Mathf.Max(1f, radius * df);
        // CameraScaler에 최소/최대 거리와 줌 속도를 조정하여 포커스 시 적절한 줌 범위를 확보
        var scaler = FindObjectOfType<CameraScaler>();
        if (scaler)
        {
            scaler.minDistance = Mathf.Min(scaler.minDistance, dist * zoomMinFactor);
            scaler.maxDistance = Mathf.Max(scaler.maxDistance, dist * zoomMaxFactor);
            scaler.zoomSpeed = Mathf.Max(0.01f, scaler.zoomSpeed * zoomSpeedMul);
        }

        float fovTarget = (bd.cameraFOVOverride > 0f) ? bd.cameraFOVOverride
                          : (targetFOV > 0f ? targetFOV : _cam.fieldOfView);

        // 카메라 목표 위치/회전 계산
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
            }
            dir.y = 0f; if (dir.sqrMagnitude < 1e-5f) dir = Vector3.forward; dir.Normalize();

            Vector3 basePos = center - dir * dist + Vector3.up * Mathf.Min(radius * 0.35f, 6f);

            // 낮은 각도 보정
            float desiredLowY = center.y - (radius * Mathf.Max(0f, lowAngleFactor));
            Vector3 lowPos = new Vector3(basePos.x, desiredLowY, basePos.z);
            camPos = Vector3.Lerp(basePos, lowPos, Mathf.Clamp01(lowAngleBias));

            // 지면 충돌 방지: 레이캐스트로 지면 높이를 확인하고 카메라 높이 보정
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
        // 즉시 CameraScaler 활성화(스크롤 후 즉시 카메라 컨트롤 복구 목적)
        if (_scalerRef != null && !_scalerRef.enabled) _scalerRef.enabled = true;

        StopAllCoroutines();
        StartCoroutine(TweenCamera(_cam.transform.position, _posBefore,
                                   _cam.transform.rotation, _rotBefore,
                                   _cam.fieldOfView, _fovBefore, returnDuration, () =>
                                   {
                                       _isFocused = false;
                                       _isFocusing = false;
                                       // 복귀 완료 시 상태 정리(스케일러는 이미 활성화됨)
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

    // 입력/유틸 헬퍼
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
