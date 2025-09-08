using UnityEngine;
using System.Collections.Generic;

public class CameraScaler : MonoBehaviour
{
    public Camera mainCamera;
    [Range(5, 13)] public int mapSize = 5;

    [Header("Block Camera When These Are Active")]
    [Tooltip("여기에 활성화되면 카메라 입력을 막을 UI 오브젝트들을 넣으세요.")]
    public List<GameObject> uiBlockers = new List<GameObject>();

    [Header("Zoom Settings")]
    public float zoomSpeed = 3f;
    public float minDistance = 5f;
    public float maxDistance = 10f;
    public Vector3 mapCenter = Vector3.zero;

    [Header("Pan Settings")]
    public float panSpeed = 0.0005f;

    [Header("Rotation Settings (Yaw Only)")]
    [Tooltip("마우스 중클릭 드래그로 좌우 회전할 때의 민감도")]
    public float yawDragSpeed = 0.2f;      // 픽셀 → 도/프레임 스케일
    [Tooltip("Q/E 키로 회전할 때의 도/초")]
    public float yawKeySpeed = 60f;

    private Vector3 cameraRotation = new Vector3(40f, 45f, 0f); // x(pitch) 고정, y는 yaw 가변
    private float currentYaw;                                    // 실시간 yaw
    private Vector3 lastMousePos;

    [Header("Pan Limits")]
    public Vector2 limitX = new Vector2(-5f, 5f);
    public Vector2 limitZ = new Vector2(-5f, 5f);

    void Start()
    {
        if (mainCamera != null)
            mainCamera.orthographic = false;

        AdjustCameraToMap();

        // 현재 카메라의 yaw를 저장 (pitch는 cameraRotation.x 유지)
        currentYaw = mainCamera.transform.eulerAngles.y;
        // pitch/roll 고정 보정
        mainCamera.transform.rotation = Quaternion.Euler(cameraRotation.x, currentYaw, 0f);
    }

    void Update()
    {
        // ✅ 목록 중 하나라도 활성화면 입력 무시
        if (IsAnyUIBlocking()) return;

        HandleZoom();
        HandleMouseDrag();     // 우클릭 패닝
        HandleYawRotate();     // 중클릭 드래그 + Q/E 회전
    }

    public void AdjustCameraToMap()
    {
        int clampedSize = Mathf.Clamp(mapSize, 5, 13);
        Vector3 targetPosition = GetCameraPositionForMapSize(clampedSize);

        mainCamera.transform.position = targetPosition;
        mainCamera.transform.rotation = Quaternion.Euler(cameraRotation);
    }

    Vector3 GetCameraPositionForMapSize(int size)
    {
        switch (size)
        {
            case 5:  return new Vector3(-3.5f, 4.5f, -3.5f);
            case 7:  return new Vector3(-5.5f, 5f,   -5.5f);
            case 9:  return new Vector3(-7.5f, 5.5f, -7.5f);
            case 11: return new Vector3(-9.5f, 6f,   -9.5f);
            case 13: return new Vector3(-11.5f, 6.5f,-11.5f);
            default:
                Debug.LogWarning($"[CameraScaler] 정의되지 않은 mapSize: {size}");
                return mainCamera.transform.position;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            // 현재 거리 계산 후 스크롤 양에 비례해 증감
            float curDist = Vector3.Distance(mainCamera.transform.position, mapCenter);
            float targetDist = Mathf.Clamp(curDist - scroll * zoomSpeed, minDistance, maxDistance);

            ApplyOrbitAtDistance(targetDist); // yaw/고정 pitch 기준으로 재배치
        }
    }

    void HandleMouseDrag()
    {
        // 우클릭 패닝
        if (Input.GetMouseButtonDown(1))
        {
            lastMousePos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;

            Vector3 right = mainCamera.transform.right;   right.y = 0; right.Normalize();
            Vector3 forward = mainCamera.transform.forward; forward.y = 0; forward.Normalize();

            Vector3 move = (-right * delta.x - forward * delta.y) * panSpeed * 0.01f;

            Vector3 proposed = mainCamera.transform.position + move;
            proposed.x = Mathf.Clamp(proposed.x, limitX.x, limitX.y);
            proposed.z = Mathf.Clamp(proposed.z, limitZ.x, limitZ.y);

            mainCamera.transform.position = proposed;
            lastMousePos = Input.mousePosition;
        }
    }

    // ✅ 좌우 회전(Orbit) — 중클릭 드래그 & Q/E 키
    void HandleYawRotate()
    {
        float yawDelta = 0f;

        // 마우스 중클릭 드래그로 yaw
        if (Input.GetMouseButtonDown(2))
        {
            lastMousePos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            yawDelta += delta.x * yawDragSpeed;           // 좌/우 드래그만 사용
            lastMousePos = Input.mousePosition;
        }

        // 키보드로 yaw
        if (Input.GetKey(KeyCode.Q)) yawDelta -= yawKeySpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) yawDelta += yawKeySpeed * Time.deltaTime;

        if (Mathf.Abs(yawDelta) > 0.0001f)
        {
            currentYaw += yawDelta;

            float dist = Mathf.Clamp(Vector3.Distance(mainCamera.transform.position, mapCenter),
                                     minDistance, maxDistance);
            ApplyOrbitAtDistance(dist);
        }
    }

    // 현재 pitch(cameraRotation.x)와 currentYaw로, 주어진 거리에서 mapCenter를 공전
    void ApplyOrbitAtDistance(float distance)
    {
        Quaternion rot = Quaternion.Euler(cameraRotation.x, currentYaw, 0f);
        Vector3 offset = rot * new Vector3(0f, 0f, -distance); // 뒤쪽 z축으로 distance만큼
        mainCamera.transform.position = mapCenter + offset;
        mainCamera.transform.rotation = rot;
    }

    /// <summary>
    /// uiBlockers 중 하나라도 활성화(activeInHierarchy)면 true
    /// </summary>
    bool IsAnyUIBlocking()
    {
        if (uiBlockers == null || uiBlockers.Count == 0) return false;

        for (int i = 0; i < uiBlockers.Count; i++)
        {
            var go = uiBlockers[i];
            if (go == null) continue;
            if (go.activeInHierarchy) return true;
        }
        return false;
    }

    // 선택: 런타임 등록/해제 편의 함수
    public void RegisterUIBlocker(GameObject go)
    {
        if (go != null && !uiBlockers.Contains(go))
            uiBlockers.Add(go);
    }
    public void UnregisterUIBlocker(GameObject go)
    {
        if (go != null)
            uiBlockers.Remove(go);
    }
}