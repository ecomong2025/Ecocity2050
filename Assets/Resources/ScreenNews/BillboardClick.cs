using UnityEngine;
using Ecocity.News;

[RequireComponent(typeof(Collider))]
public class BillboardClick : MonoBehaviour
{
    public NewsOverlayManager manager;

    void OnMouseDown()
    {
        // lastBillboardTexture가 있으면 overlay 표시
        if (manager != null && manager.lastBillboardTexture != null)
        {
            manager.ShowOverlayWithBillboardImage();
        }
    }
}