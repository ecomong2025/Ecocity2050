using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public Camera mainCamera;
    [Range(5, 13)] public int mapSize = 5;

    private Vector3 cameraRotation = new Vector3(40f, 45f, 0f); // 고정 대각 시점

    void Start()
    {
        AdjustCameraToMap();
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
            case 7: return new Vector3(-3.5f, 4.3f, -5.5f);
            case 9: return new Vector3(-3.5f, 4.3f, -7.5f);
            case 11: return new Vector3(-3.5f, 4.3f, -9.5f);
            case 13: return new Vector3(-3.5f, 4.3f, -11.5f);
            default:
                Debug.LogWarning($"[CameraScaler] 정의되지 않은 mapSize: {size}");
                return mainCamera.transform.position;
        }
    }
}