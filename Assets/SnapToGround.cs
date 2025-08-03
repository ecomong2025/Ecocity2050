using UnityEngine;

public class SnapToGround : MonoBehaviour
{
    void Start()
    {
        Snap();
    }

    void Snap()
    {
        // 바운딩 박스 기준으로 가장 아래 y값을 가져옴
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer rend in renderers)
        {
            bounds.Encapsulate(rend.bounds);
        }

        float bottomY = bounds.min.y;
        float offsetY = transform.position.y - bottomY;

        // 현재 위치에서 y축만 offset 시켜줌
        transform.position = new Vector3(
            transform.position.x,
            transform.position.y + offsetY,
            transform.position.z
        );
    }
}