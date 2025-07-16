using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public Camera mainCamera;
    [Range(5, 13)] public int mapSize = 5;

    [Header("Zoom Settings")]
    public float zoomSpeed = 10f;
    public float minDistance = 3f;
    public float maxDistance = 30f;

    [Header("Pan Settings")]
    public float panSpeed = 0.0005f; 

    private Vector3 cameraRotation = new Vector3(40f, 45f, 0f);
    private Vector3 lastMousePos;

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
            case 5: return new Vector3(-3.5f, 4.3f, -3.5f);
            case 7: return new Vector3(-5.5f, 4.3f, -5.5f);
            case 9: return new Vector3(-7.5f, 4.3f, -7.5f);
            case 11: return new Vector3(-9.5f, 4.3f, -9.5f);
            case 13: return new Vector3(-11.5f, 4.3f, -11.5f);
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

            float distance = Vector3.Distance(newPos, Vector3.zero);
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

            
            Vector3 move = (-mainCamera.transform.right * delta.x - mainCamera.transform.up * delta.y) * panSpeed * 0.01f;
            mainCamera.transform.Translate(move, Space.World);

            lastMousePos = Input.mousePosition;
        }
    }
}