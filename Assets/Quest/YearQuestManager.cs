using System;
using System.Linq;
using System.Collections.Generic;     // ★ 유지
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// YearQuestManager.cs 안, public static YearQuestManager Instance; 아래나 아무 public 메서드 자리

[System.Serializable]
public class YearQuests
{
    [Range(2025, 2050)] public int year;
    public string[] questTexts = new string[4];
}

[System.Serializable]
public class YearGaugePiece
{
    public int year;
    public GameObject imageObj;
}

public class YearQuestManager : MonoBehaviour
{

    public bool[] GetCompletedSnapshot()
    {
        // 외부에서 completed 배열을 수정하지 못하게 복사본 반환
        return (bool[])completed.Clone();
    }
    // ★ UI 준비 전 들어오는 체크 이벤트 임시 저장
    private bool uiReady = false;
    private readonly Queue<int> pendingChecks = new Queue<int>();

    [Header("Auto-complete Rules")]
    [SerializeField][Range(0, 3)] private int factoryQuestIndex = 0;
    [SerializeField][Range(0, 3)] private int zeroEmissionQuestIndex = 1;
    [SerializeField][Range(0, 3)] private int chatQuestIndex = 3;

    public void OnBuildingInstalled(GameObject prefab, BuildingData data)
    {
        if (prefab == null || data == null) return;

        bool isFactory = IsFactory(prefab, data);
        bool isZero = IsZeroEmission(data);

        Debug.Log($"[YearQuestManager] Installed: {prefab.name}, isFactory={isFactory}, zero={isZero}");

        if (isFactory) CompleteQuest(factoryQuestIndex);
        if (isZero) CompleteQuest(zeroEmissionQuestIndex);
    }

    public void OnChatCompleted()
    {
        Debug.Log("[YearQuestManager] Chat quest completed!");
        CompleteQuest(chatQuestIndex);
    }

    private bool IsFactory(GameObject prefab, BuildingData data)
    {
        if (prefab != null && prefab.CompareTag("Factory")) return true;
        string n = prefab != null ? prefab.name.ToLower() : "";
        return n.Contains("factory") || n.Contains("plant"); // 필요 시 "공장"도 추가
    }

    private bool IsZeroEmission(BuildingData data)
    {
        return data.co2PerSecond <= 0f
            && data.instantCO2Change <= 0f
            && data.maxCO2Change <= 0f;
    }

    public static YearQuestManager Instance;

    [Header("Year Settings")]
    [SerializeField] private int currentYear = 2025;
    [SerializeField] private int minYear = 2025;
    [SerializeField] private int maxYear = 2050;
    [SerializeField] private int step = 5;

    [Header("Predefined Sets")]
    [SerializeField] private YearQuests[] yearSets;

    [Header("UI")]
    [SerializeField] private QuestUITemplate questUI;

    [Header("Gauge Pieces")]
    [SerializeField] private YearGaugePiece[] gaugePieces;

    [Header("Year Text (화면 중앙 표시)")]
    [SerializeField] private TextMeshProUGUI yearTextUI;

    private bool[] completed = new bool[4];

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // ★ 비활성 포함 탐색 (가능하면 인스펙터 드래그로 고정 권장)
#if UNITY_2022_2_OR_NEWER
        if (questUI == null)
            questUI = FindFirstObjectByType<QuestUITemplate>(FindObjectsInactive.Include);
#else
        if (questUI == null)
            questUI = Resources.FindObjectsOfTypeAll<QuestUITemplate>().FirstOrDefault();
#endif

        if (gaugePieces != null && gaugePieces.Length > 0)
            Array.Sort(gaugePieces, (a, b) => a.year.CompareTo(b.year));
    }

    void Start()
    {
        if (questUI == null)
            questUI = FindObjectOfType<QuestUITemplate>(true);

        LoadYear(currentYear);
    }

    private void LoadYear(int year)
    {
        uiReady = false;  // ★ 바인딩 전 잠금

        var set = yearSets.FirstOrDefault(s => s.year == year);
        string[] texts = (set == null || set.questTexts == null || set.questTexts.Length != 4)
            ? new[] { "Quest1", "Quest2", "Quest3", "Quest4" }
            : set.questTexts;

        completed = new bool[4] { false, false, false, false };
        questUI?.BindYear(year, texts, completed);

        RefreshGauge();
        RefreshYearText();

        FindObjectOfType<QuizManager>()?.ResetQuizCorrectCount();

        uiReady = true;   // ★ 바인딩 완료

        // ★ 바인딩 중 도착한 체크 처리
        while (pendingChecks.Count > 0)
            CompleteQuest_Internal(pendingChecks.Dequeue());
    }

    private void RefreshGauge()
    {
        if (gaugePieces == null) return;
        foreach (var p in gaugePieces)
        {
            if (p.imageObj == null) continue;
            p.imageObj.SetActive(currentYear >= p.year);
        }
    }

    private void RefreshYearText()
    {
        if (yearTextUI != null)
            yearTextUI.text = currentYear.ToString();
    }

    public void CompleteQuest(int index)
    {
        if (index < 0 || index > 3) return;
        Debug.Log($"[YQM] CompleteQuest request index={index} ui={(questUI ? questUI.name : "NULL")}");

        if (!uiReady)
        {
            pendingChecks.Enqueue(index); // ★ UI 준비 전이면 큐에만 넣고 끝
            return;
        }

        CompleteQuest_Internal(index);    // ★ 실제 처리는 내부에서만
    }

    // ★ 실제 처리 단일 경로
    private void CompleteQuest_Internal(int index)
    {
        if (index < 0 || index > 3) return;
        if (completed[index]) { Debug.Log($"[YQM] Already completed idx={index}"); return; }

        completed[index] = true;
        questUI?.UpdateCheck(index, true);

        // 모두 완료 시 다음 단계
        if (completed.All(x => x))
        {
            var tms = FindObjectOfType<TileManagerSequential>(true);
            if (tms != null) tms.UnlockTileForYear(currentYear);

            int next = Mathf.Clamp(currentYear + step, minYear, maxYear);
            if (next == currentYear) { Debug.Log("[YQM] 마지막 연도"); return; }

            currentYear = next;
            LoadYear(currentYear);
        }
    }

    public void ResetCurrent() => LoadYear(currentYear);
    public int GetCurrentYear() => currentYear;
}
