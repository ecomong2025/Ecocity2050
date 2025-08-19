using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BuildingLockBulkManager : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField] private Transform root; // 버튼들이 들어있는 부모(비우면 자기 자신)
    [SerializeField] private string overlayName = "BuildingLockOverlay"; // 자식 오버레이 오브젝트 이름

    [Header("Defaults")]
    [SerializeField] private int defaultUnlockYear = 2025; // 메타가 없을 때 쓸 기본 연도

    struct Item
    {
        public Button btn;
        public EventTrigger trigger;
        public GameObject overlay;
        public int unlockYear;
    }
    private readonly List<Item> items = new();

    private void Awake()
    {
        if (!root) root = transform;

        // 비활성 포함해서 전부 스캔
        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            // 오버레이가 없는 버튼은 잠금 대상이 아니라고 보고 스킵
            var overlayTr = b.transform.Find(overlayName);
            if (!overlayTr) continue;

            // 버튼에 메타가 있으면 그 값, 없으면 기본 연도
            var meta = b.GetComponent<BuildingUnlockMeta>();
            int year = (meta != null && meta.unlockYear > 0) ? meta.unlockYear : defaultUnlockYear;

            items.Add(new Item
            {
                btn = b,
                trigger = b.GetComponent<EventTrigger>(),
                overlay = overlayTr.gameObject,
                unlockYear = year
            });
        }
    }

    private void OnEnable()
    {
        // YearQuestManager의 이벤트가 static이라고 가정
        YearQuestManager.OnYearChanged += RefreshAll;

        if (YearQuestManager.Instance != null)
            RefreshAll(YearQuestManager.Instance.GetCurrentYear());
    }

    private void OnDisable()
    {
        YearQuestManager.OnYearChanged -= RefreshAll;
    }

    private void RefreshAll(int currentYear)
    {
        foreach (var it in items)
        {
            bool unlocked = currentYear >= it.unlockYear;
            if (it.btn) it.btn.interactable = unlocked;
            if (it.trigger) it.trigger.enabled = unlocked;
            if (it.overlay) it.overlay.SetActive(!unlocked);
        }
    }
}
