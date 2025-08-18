using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public Camera mainCamera;
    [Range(5, 13)] public int mapSize = 5;

    public GameObject buildingPanel; 
    public GameObject chatPanel;
    public GameObject questUI;

    [Header("Zoom Settings")]
    public float zoomSpeed = 3f;
    public float minDistance = 5f;
    public float maxDistance = 10f;
    public Vector3 mapCenter = Vector3.zero; // ✅ 맵 중심 (인스펙터에서 지정 가능)

    [Header("Pan Settings")]
    public float panSpeed = 0.0005f; 

    private Vector3 cameraRotation = new Vector3(40f, 45f, 0f);
    private Vector3 lastMousePos;

    [Header("Pan Limits")]
    public Vector2 limitX = new Vector2(-5f, 5f);
    public Vector2 limitZ = new Vector2(-5f, 5f);

    void Start()
    {
        if (mainCamera != null)
        {
            mainCamera.orthographic = false;
        }

        AdjustCameraToMap();
    }

    void Update()
    {
        // ✅ 세 패널 중 하나라도 켜져 있으면 입력 무시
        if ((buildingPanel != null && buildingPanel.activeSelf) ||
            (chatPanel != null && chatPanel.activeSelf) ||
            (questUI != null && questUI.activeSelf))
        {
            return;
        }

        HandleZoom();
        HandleMouseDrag();
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
            case 5: return new Vector3(-3.5f, 4.5f, -3.5f);
            case 7: return new Vector3(-5.5f, 5f, -5.5f);
            case 9: return new Vector3(-7.5f, 5.5f, -7.5f);
            case 11: return new Vector3(-9.5f, 6f, -9.5f);
            case 13: return new Vector3(-11.5f, 6.5f, -11.5f);
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
            Vector3 direction = mainCamera.transform.forward;
            Vector3 newPos = mainCamera.transform.position + direction * scroll * zoomSpeed;

            // 맵 중심 기준 거리 계산
            float distance = Vector3.Distance(newPos, mapCenter);

            if (distance >= minDistance && distance <= maxDistance)
            {
                mainCamera.transform.position = newPos;
            }
        }
    }

    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(1))
        {
            lastMousePos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;

            // ✅ 카메라 기준 좌/우(right), 앞/뒤(forward)를 수평면(XZ)으로 투영
            Vector3 right = mainCamera.transform.right;   right.y = 0; right.Normalize();
            Vector3 forward = mainCamera.transform.forward; forward.y = 0; forward.Normalize();

            // 좌우 드래그→right, 위아래 드래그→forward 방향 이동 (Y는 자동으로 0)
            Vector3 move = (-right * delta.x - forward * delta.y) * panSpeed * 0.01f;

            // 이동을 먼저 가상으로 적용한 뒤, 경계 클램프하여 최종 위치로 반영
            Vector3 proposed = mainCamera.transform.position + move;
            proposed.x = Mathf.Clamp(proposed.x, limitX.x, limitX.y);
            proposed.z = Mathf.Clamp(proposed.z, limitZ.x, limitZ.y);
            // Y는 패닝에서 고정 (줌으로만 변화)
            // proposed.y = mainCamera.transform.position.y; // 굳이 명시하고 싶으면 이 줄 추가

            mainCamera.transform.position = proposed;
            lastMousePos = Input.mousePosition;
        }
    }
}