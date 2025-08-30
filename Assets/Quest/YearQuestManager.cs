using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class YearQuests
{
    [Range(2025, 2045)] public int year;
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
    public static event Action<int> OnYearChanged;

    [Header("External Managers")]
    public QuizManager quizManager;
    [SerializeField] private QuestAutoCompleter questAutoCompleter;
    [SerializeField] private QuestUITemplate questUI;

    private bool uiReady = false;
    private readonly Queue<int> pendingChecks = new();

    [Header("Year Settings")]
    [SerializeField] private int currentYear = 2025;
    [SerializeField] private int minYear = 2025;
    [SerializeField] private int maxYear = 2045;
    [SerializeField] private int step = 5;

    [Header("Predefined Sets")]
    [SerializeField] private YearQuests[] yearSets;
    public YearQuests[] GetYearSets() => yearSets;

    private bool[] completed = new bool[4];
    private bool advancing = false;

    // ====== Unity Lifecycle ======
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (questUI == null)
            questUI = Resources.FindObjectsOfTypeAll<QuestUITemplate>().FirstOrDefault();

        if (quizManager == null)
            quizManager = FindObjectOfType<QuizManager>(true);

        if (questAutoCompleter == null)
            questAutoCompleter = FindObjectOfType<QuestAutoCompleter>(true);
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
        LoadYear(currentYear);
    }

    // ====== 외부 이벤트 진입점 ======
    public void OnBuildingInstalled(GameObject prefab, BuildingData data)
    {
        questAutoCompleter?.HandleBuildingInstalled(prefab, data, currentYear);
    }

    public void OnChatCompleted()
    {
        questAutoCompleter?.HandleChatCompleted(currentYear);
    }

    public void ReportQuizCorrect()
    {
        questAutoCompleter?.HandleQuizCorrect(currentYear);
    }

    // ====== 연도 로딩/동기화 ======
    private void LoadYear(int year)
    {
        uiReady = false;

        var set = yearSets != null ? yearSets.FirstOrDefault(s => s != null && s.year == year) : null;
        string[] texts = (set == null || set.questTexts == null || set.questTexts.Length != 4)
            ? new[] { "Quest1", "Quest2", "Quest3", "Quest4" }
            : set.questTexts;

        completed = new bool[4] { false, false, false, false };

        questUI?.BindYear(year, texts, completed);

        quizManager?.ResetQuizCorrectCount();

        var gptManager = FindObjectOfType<GPTChatManager>();
        gptManager?.OnYearChanged(year);

        questAutoCompleter?.OnYearLoaded(year);

        uiReady = true;

        while (pendingChecks.Count > 0)
            CompleteQuest_Internal(pendingChecks.Dequeue());

        OnYearChanged?.Invoke(year);
    }

    // ====== 퀘스트 완료 처리 ======
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
            tms?.UnlockTileForYear(currentYear);

            int next = Mathf.Clamp(currentYear + step, minYear, maxYear);
            if (next == currentYear) return;

            if (!advancing)
            {
                advancing = true;
                StartCoroutine(AdvanceAfterPopup(next));
            }
        }
    }

    private IEnumerator AdvanceAfterPopup(int nextYear)
    {
        if (questUI != null)
            questUI.EnqueueNextYearPopup(nextYear);

        currentYear = nextYear;
        LoadYear(currentYear);
        advancing = false;
        yield break;
    }

    // ====== 퀴즈 연동 ======
    private void HandleYearChanged(int year)
    {
        if (quizManager == null || !quizManager.IsReady) return;

        quizManager.UpdateYearQuiz(year);
    }

    // ====== 유틸 ======
    public void ResetCurrent() => LoadYear(currentYear);
    public int GetCurrentYear() => currentYear;
}