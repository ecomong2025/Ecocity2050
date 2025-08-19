using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildingHoverManager : MonoBehaviour
{
    [Header("Hierarchy")]
    public Transform contentRoot;                        // ScrollView의 Content
    [SerializeField] private string overlayName = "BuildingLockOverlay"; // 잠금 오버레이 자식 이름

    // 버튼 오브젝트 -> (인포패널, 오버레이, 버튼, 트리거) 캐시
    private readonly Dictionary<GameObject, (GameObject panel, GameObject overlay, Button btn, EventTrigger trig)> map = new();

    void Start()
    {
        if (!contentRoot)
        {
            Debug.LogWarning("[BuildingHoverManager] contentRoot가 비어 있습니다.");
            return;
        }

        foreach (Transform child in contentRoot)
        {
            var buttonObj = child.gameObject;

            // 필수: InfoPanel 찾기
            var infoTr = buttonObj.transform.Find("BuildingInfoPanel");
            if (!infoTr) continue;

            var infoPanel = infoTr.gameObject;
            infoPanel.SetActive(false);

            // 선택: 잠금 오버레이 찾기
            var overlayTr = buttonObj.transform.Find(overlayName);
            GameObject overlay = overlayTr ? overlayTr.gameObject : null;

            // 컴포넌트들
            var btn = buttonObj.GetComponent<Button>();
            var trigger = buttonObj.GetComponent<EventTrigger>() ?? buttonObj.AddComponent<EventTrigger>();

            map[buttonObj] = (infoPanel, overlay, btn, trigger);

            // 진입
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((_) =>
            {
                if (CanShow(buttonObj))
                    infoPanel.SetActive(true);
            });
            trigger.triggers.Add(entryEnter);

            // 이탈(잠금 여부와 상관없이 꺼준다)
            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((_) => infoPanel.SetActive(false));
            trigger.triggers.Add(entryExit);
        }
    }

    void OnDisable()
    {
        // 매니저 비활성화 시 모든 패널 끄기(안전)
        foreach (var kv in map)
            if (kv.Value.panel) kv.Value.panel.SetActive(false);
    }

    /// <summary>
    /// 잠금 상태(오버레이/버튼 상태)에 따라 호버 패널을 띄워도 되는지 판단
    /// </summary>
    private bool CanShow(GameObject buttonObj)
    {
        if (!map.TryGetValue(buttonObj, out var tup)) return false;

        var (panel, overlay, btn, trig) = tup;

        // 1) 잠금 오버레이가 활성화되어 있으면 표시 금지
        //    - Image.raycastTarget / CanvasGroup.blocksRaycasts 여부와 상관없이
        if (overlay && overlay.activeInHierarchy) return false;

        // 2) 버튼이 비활성화 상태면 표시 금지
        if (btn && !btn.interactable) return false;

        // 3) 기타: 여기서 추가 규칙 필요하면 더 체크
        return true;
    }
}
