using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public static YearQuestManager Instance;
    public static event System.Action<int> OnYearChanged;

    // ─────────────────────────────────────────────────────────────────────────────
    //  Overlay: NextYearCanvas / NextYearPannel (항상 켜진 오버레이에 배치)
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Next Year Popup (Overlay)")]
    [SerializeField] private GameObject nextYearPanel;            // NextYearPannel
    [SerializeField] private TextMeshProUGUI nextYearTextTMP;     // 자식: Year (TMP)
    [SerializeField] private TextMeshProUGUI announceTextTMP;     // 자식: Announce (TMP)
    [SerializeField] private float popupSeconds = 1.8f;           // 표시 시간
    [SerializeField] private bool fadeWithCanvasGroup = true;     // 페이드 여부
    private bool advancing = false;                               // 중복 방지

    public QuizManager quizManager;

    private IEnumerator ShowNextYearAndAdvance(int nextYear)
    {
        // 텍스트 세팅
        if (nextYearTextTMP) nextYearTextTMP.text = nextYear.ToString();
        if (announceTextTMP) announceTextTMP.text = $"{nextYear}년도에 도달했어요!";

        // 패널 On
        if (nextYearPanel) nextYearPanel.SetActive(true);

        // 페이드 인
        CanvasGroup cg = null;
        if (fadeWithCanvasGroup && nextYearPanel)
        {
            cg = nextYearPanel.GetComponent<CanvasGroup>() ?? nextYearPanel.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // 오버레이지만 입력 막지 않음
            cg.interactable   = false;
            cg.alpha = 0f;
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.SmoothStep(0f, 1f, t / 0.15f);
                yield return null;
            }
        }

        // 대기
        float timer = 0f;
        while (timer < popupSeconds)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        // 페이드 아웃
        if (fadeWithCanvasGroup && cg)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.SmoothStep(1f, 0f, t / 0.2f);
                yield return null;
            }
        }

        // 패널 Off
        if (nextYearPanel) nextYearPanel.SetActive(false);

        // 실제 연도 갱신 + 로드
        currentYear = nextYear;
        LoadYear(currentYear);
        advancing = false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────────────────────
    public bool[] GetCompletedSnapshot() => (bool[])completed.Clone(); // 방어적 복사
    private bool uiReady = false;                      // UI 바인딩 전 잠금
    private readonly Queue<int> pendingChecks = new(); // UI 준비 전 체크 대기

    [Header("Auto-complete Rules")]
    [SerializeField][Range(0, 3)] private int factoryQuestIndex = 0;
    [SerializeField][Range(0, 3)] private int zeroEmissionQuestIndex = 1;
    [SerializeField][Range(0, 3)] private int chatQuestIndex = 3;

    public void OnBuildingInstalled(GameObject prefab, BuildingData data)
    {
        if (prefab == null || data == null) return;

        bool isFactory = IsFactory(prefab, data);
        bool isZero = IsZeroEmission(data);

        if (isFactory) CompleteQuest(factoryQuestIndex);
        if (isZero) CompleteQuest(zeroEmissionQuestIndex);

        // 2030: 자전거 도로
        if (prefab.CompareTag("BikeRoad") && currentYear == 2030) CompleteQuest(0);
        // 2035: 에너지 절약형
        if (prefab.CompareTag("EnergySaving") && currentYear == 2035) CompleteQuest(0);
        // 2040: 지하철
        if (prefab.CompareTag("PublicTransport") && currentYear == 2040) CompleteQuest(0);
        // 2045: 친환경 발전소
        if ((prefab.CompareTag("EcoPlant") || prefab.name.Contains("발전소")) && currentYear == 2045) CompleteQuest(0);

        // 2040: 조언 기반
        if (currentYear == 2040) CheckAdviceBasedQuest(prefab, data);

        // 2045: 예산 연계
        if (currentYear == 2045 && data.incomePer5Minutes > 0) CheckBudgetQuestBuilding();
    }

    private void CheckAdviceBasedQuest(GameObject prefab, BuildingData data)
    {
        bool ok = false;
        var n = prefab.name.ToLower();
        if (n.Contains("공원") || n.Contains("park")) ok = true;
        else if (IsZeroEmission(data)) ok = true;
        else if (data.incomePer5Minutes > 0) ok = true;

        if (ok) CompleteQuest(2); // 가정: 2040년 퀘스트 인덱스 2
    }

    private void CheckBudgetQuestBuilding()
    {
        var gptManager = FindObjectOfType<GPTChatManager>();
        if (gptManager != null && gptManager.IsChatCompletedForYear(2045))
            CompleteQuest(2); // 가정: 2045년 퀘스트 인덱스 2
    }

    public void OnChatCompleted() => CompleteQuest(chatQuestIndex);

    private bool IsFactory(GameObject prefab, BuildingData data)
    {
        if (prefab != null && prefab.CompareTag("Factory")) return true;
        string n = prefab != null ? prefab.name.ToLower() : "";
        return n.Contains("factory") || n.Contains("plant");
    }

    private bool IsZeroEmission(BuildingData data)
    {
        return data.co2PerSecond <= 0f
            && data.instantCO2Change <= 0f
            && data.maxCO2Change <= 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Year / UI
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Year Settings")]
    [SerializeField] private int currentYear = 2025;
    [SerializeField] private int minYear = 2025;
    [SerializeField] private int maxYear = 2050;
    [SerializeField] private int step = 5;

    [Header("Predefined Sets")]
    [SerializeField] private YearQuests[] yearSets;
    public YearQuests[] GetYearSets() => yearSets;

    [Header("UI")]
    [SerializeField] private QuestUITemplate questUI;

    [Header("Gauge Pieces")]
    [SerializeField] private YearGaugePiece[] gaugePieces;

    [Header("Year Text (화면 중앙 표시)")]
    [SerializeField] private TextMeshProUGUI yearTextUI;

    private bool[] completed = new bool[4];

    // ─────────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

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
        YearQuestManager.OnYearChanged += HandleYearChanged;
        // 오버레이 패널은 기본 꺼두기
        if (nextYearPanel) nextYearPanel.SetActive(false);

        if (questUI == null)
            questUI = FindObjectOfType<QuestUITemplate>(true);

        LoadYear(currentYear);
    }

    void HandleYearChanged(int year)
    {
        quizManager.UpdateYearQuiz(year); // 연도별 퀴즈 필터링 및 정답 카운트 초기화
    }

    private void LoadYear(int year)
    {
        uiReady = false;

        var set = yearSets.FirstOrDefault(s => s.year == year);
        string[] texts = (set == null || set.questTexts == null || set.questTexts.Length != 4)
            ? new[] { "Quest1", "Quest2", "Quest3", "Quest4" }
            : set.questTexts;

        completed = new bool[4] { false, false, false, false };
        questUI?.BindYear(year, texts, completed);

        RefreshGauge();
        RefreshYearText();

        FindObjectOfType<QuizManager>()?.ResetQuizCorrectCount();

        var gptManager = FindObjectOfType<GPTChatManager>();
        if (gptManager != null) gptManager.OnYearChanged(year);

        uiReady = true;

        while (pendingChecks.Count > 0)
            CompleteQuest_Internal(pendingChecks.Dequeue());

        OnYearChanged?.Invoke(year);
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

    // ─────────────────────────────────────────────────────────────────────────────
    // Quest Control
    // ─────────────────────────────────────────────────────────────────────────────
    public void CompleteQuest(int index)
    {
        if (index < 0 || index > 3) return;
        if (!uiReady) { pendingChecks.Enqueue(index); return; }
        CompleteQuest_Internal(index);
    }

    private void CompleteQuest_Internal(int index)
    {
        if (index < 0 || index > 3) return;
        if (completed[index]) return;

        completed[index] = true;
        questUI?.UpdateCheck(index, true);

        // 모두 완료 시 → 오버레이 팝업 띄우고 그 다음 연도로 로드
        if (completed.All(x => x))
        {
            var tms = FindObjectOfType<TileManagerSequential>(true);
            if (tms != null) tms.UnlockTileForYear(currentYear);

            int next = Mathf.Clamp(currentYear + step, minYear, maxYear);
            if (next == currentYear) { Debug.Log("[YQM] 마지막 연도"); return; }

            if (!advancing)
            {
                advancing = true;
                StartCoroutine(ShowNextYearAndAdvance(next)); // 오버레이라 어디서나 보임
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // External
    // ─────────────────────────────────────────────────────────────────────────────
    public void ResetCurrent() => LoadYear(currentYear);
    public int GetCurrentYear() => currentYear;
}