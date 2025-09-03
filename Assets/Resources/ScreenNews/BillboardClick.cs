using UnityEngine;
using Ecocity.News;

[RequireComponent(typeof(Collider))]
public class BillboardClick : MonoBehaviour
{
    public NewsOverlayManager manager;

    void OnMouseDown()
    {
        // 특정 UI가 열려있으면 클릭 무시
        if (IsPanelActive("BuildingPanel") ||
            IsPanelActive("BuildingInstallPanel") ||
            IsPanelActive("ChatPanel") ||
            IsPanelActive("QuestUI") ||
            IsPanelActive("NewsOverlayCanvas"))
            return;

        if (manager != null && manager.lastBillboardTexture != null)
        {
            manager.ShowOverlayWithBillboardImage();
        }
    }

    bool IsPanelActive(string panelName)
    {
        var go = GameObject.Find(panelName);
        return go != null && go.activeInHierarchy;
    }
}