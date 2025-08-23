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

[DefaultExecutionOrder(100)]
public class YearQuestManager : MonoBehaviour
{
    public static YearQuestManager Instance;
    public static event System.Action<int> OnYearChanged;

    [Header("Next Year Popup (Overlay)")]
    [SerializeField] private GameObject nextYearPanel;
    [SerializeField] private TextMeshProUGUI nextYearTextTMP;
    [SerializeField] private TextMeshProUGUI announceTextTMP;
    [SerializeField] private float popupSeconds = 1.8f;
    [SerializeField] private bool fadeWithCanvasGroup = true;
    private bool advancing = false;

    public QuizManager quizManager;

    private IEnumerator ShowNextYearAndAdvance(int nextYear)
    {
        if (nextYearTextTMP) nextYearTextTMP.text = nextYear.ToString();
        if (announceTextTMP) announceTextTMP.text = $"{nextYear}년도에 도달했어요!";

        if (nextYearPanel) nextYearPanel.SetActive(true);

        CanvasGroup cg = null;
        if (fadeWithCanvasGroup && nextYearPanel)
        {
            cg = nextYearPanel.GetComponent<CanvasGroup>() ?? nextYearPanel.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.SmoothStep(0f, 1f, t / 0.15f);
                yield return null;
            }
        }

        float timer = 0f;
        while (timer < popupSeconds)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

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

        if (nextYearPanel) nextYearPanel.SetActive(false);

        currentYear = nextYear;
        LoadYear(currentYear);
        advancing = false;
    }

    public bool[] GetCompletedSnapshot() => (bool[])completed.Clone();
    private bool uiReady = false;
    private readonly Queue<int> pendingChecks = new();

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

        if (prefab.CompareTag("BikeRoad") && currentYear == 2030) CompleteQuest(0);
        if (prefab.CompareTag("EnergySaving") && currentYear == 2035) CompleteQuest(0);
        if (prefab.CompareTag("PublicTransport") && currentYear == 2040) CompleteQuest(0);
        if ((prefab.CompareTag("EcoPlant") || prefab.name.Contains("발전소")) && currentYear == 2045) CompleteQuest(0);

        if (currentYear == 2040) CheckAdviceBasedQuest(prefab, data);
        if (currentYear == 2045 && data.incomePer5Minutes > 0) CheckBudgetQuestBuilding();
    }

    private void CheckAdviceBasedQuest(GameObject prefab, BuildingData data)
    {
        bool ok = false;
        var n = (prefab != null ? prefab.name : "").ToLower();
        if (n.Contains("공원") || n.Contains("park")) ok = true;
        else if (IsZeroEmission(data)) ok = true;
        else if (data.incomePer5Minutes > 0) ok = true;

        if (ok) CompleteQuest(2);
    }

    private void CheckBudgetQuestBuilding()
    {
        var gptManager = FindObjectOfType<GPTChatManager>();
        if (gptManager != null && gptManager.IsChatCompletedForYear(2045))
            CompleteQuest(2);
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

        // quizManager 자동 바인딩(인스펙터 미지정 대비)
        if (quizManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            quizManager = FindFirstObjectByType<QuizManager>(FindObjectsInactive.Include);
#else
            quizManager = FindObjectOfType<QuizManager>(true);
#endif
        }
    }

    void OnEnable()
    {
        OnYearChanged += HandleYearChanged;
    }

    void OnDisable()
    {
        OnYearChanged -= HandleYearChanged;
    }

    void Start()
    {
        if (nextYearPanel) nextYearPanel.SetActive(false);

        if (questUI == null)
            questUI = FindObjectOfType<QuestUITemplate>(true);

        LoadYear(currentYear); // 여기서 OnYearChanged(currentYear)까지 호출됨
    }

    private void HandleYearChanged(int year)
    {
        // 퀴즈 매니저가 없거나 아직 준비 전이면 안전하게 스킵
        if (quizManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            quizManager = FindFirstObjectByType<QuizManager>(FindObjectsInactive.Include);
#else
            quizManager = FindObjectOfType<QuizManager>(true);
#endif
            if (quizManager == null)
            {
                Debug.LogWarning("[YQM] QuizManager가 씬에 없습니다. 퀴즈 갱신 생략.");
                return;
            }
        }

        if (!quizManager.IsReady)
        {
            // 한 프레임 뒤 재시도—무한 루프 방지로 1회만
            StartCoroutine(DelayUpdateQuizOnce(year));
            return;
        }

        quizManager.UpdateYearQuiz(year);
    }

    private IEnumerator DelayUpdateQuizOnce(int year)
    {
        yield return null;
        if (quizManager != null && quizManager.IsReady)
            quizManager.UpdateYearQuiz(year);
        else
            Debug.LogWarning("[YQM] QuizManager가 아직 준비되지 않았습니다(재시도 후).");
    }

    private void LoadYear(int year)
    {
        uiReady = false;

        var set = yearSets != null ? yearSets.FirstOrDefault(s => s != null && s.year == year) : null;
        string[] texts = (set == null || set.questTexts == null || set.questTexts.Length != 4)
            ? new[] { "Quest1", "Quest2", "Quest3", "Quest4" }
            : set.questTexts;

        completed = new bool[4] { false, false, false, false };
        questUI?.BindYear(year, texts, completed);

        RefreshGauge();
        RefreshYearText();

        // 퀴즈 카운트 초기화
        if (quizManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            quizManager = FindFirstObjectByType<QuizManager>(FindObjectsInactive.Include);
#else
            quizManager = FindObjectOfType<QuizManager>(true);
#endif
        }
        quizManager?.ResetQuizCorrectCount();

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
            if (p == null || p.imageObj == null) continue;
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
        if (!uiReady) { pendingChecks.Enqueue(index); return; }
        CompleteQuest_Internal(index);
    }

    private void CompleteQuest_Internal(int index)
    {
        if (index < 0 || index > 3) return;
        if (completed[index]) return;

        completed[index] = true;
        questUI?.UpdateCheck(index, true);

        if (completed.All(x => x))
        {
            var tms = FindObjectOfType<TileManagerSequential>(true);
            if (tms != null) tms.UnlockTileForYear(currentYear);

            int next = Mathf.Clamp(currentYear + step, minYear, maxYear);
            if (next == currentYear) { Debug.Log("[YQM] 마지막 연도"); return; }

            if (!advancing)
            {
                advancing = true;
                StartCoroutine(ShowNextYearAndAdvance(next));
            }
        }
    }

    public void ResetCurrent() => LoadYear(currentYear);
    public int GetCurrentYear() => currentYear;
}