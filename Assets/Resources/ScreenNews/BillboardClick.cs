using UnityEngine;
using Ecocity.News;   // ✅ 이거 추가

[RequireComponent(typeof(Collider))]
public class BillboardClick : MonoBehaviour
{
    public NewsOverlayManager manager; // 연결해둘 매니저

    void OnMouseDown()
    {
        if (manager != null)
        {
            manager.RequestAndShowNews();
        }
    }
}