using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildingHoverManager : MonoBehaviour
{
    public Transform contentRoot; // ScrollView의 Content
    private readonly Dictionary<GameObject, GameObject> infoPanels = new();

    void Start()
    {
        foreach (Transform child in contentRoot)
        {
            GameObject buttonObj = child.gameObject;

            // BuildingInfoPanel 찾기
            Transform infoPanelTransform = buttonObj.transform.Find("BuildingInfoPanel");
            if (infoPanelTransform == null)
                continue;

            GameObject infoPanel = infoPanelTransform.gameObject;
            infoPanel.SetActive(false); // 초기에는 꺼두기

            infoPanels[buttonObj] = infoPanel;

            // EventTrigger 추가
            EventTrigger trigger = buttonObj.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = buttonObj.AddComponent<EventTrigger>();

            // PointerEnter
            EventTrigger.Entry entryEnter = new()
            {
                eventID = EventTriggerType.PointerEnter
            };
            entryEnter.callback.AddListener((_) => infoPanel.SetActive(true));
            trigger.triggers.Add(entryEnter);

            // PointerExit
            EventTrigger.Entry entryExit = new()
            {
                eventID = EventTriggerType.PointerExit
            };
            entryExit.callback.AddListener((_) => infoPanel.SetActive(false));
            trigger.triggers.Add(entryExit);
        }
    }
}
