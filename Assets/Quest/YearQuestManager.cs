using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
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
    public static event System.Action<int> OnYearChanged;

    [Header("External Managers")]
    public QuizManager quizManager;
    [SerializeField] private QuestAutoCompleter questAutoCompleter; // 규칙 전담자
    [SerializeField] private QuestUITemplate questUI;               // UI 전담자

    // UI 준비 전 들어온 완료 신호를 보관
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

#if UNITY_2022_2_OR_NEWER
        if (questUI == null)
            questUI = FindFirstObjectByType<QuestUITemplate>(FindObjectsInactive.Include);
#else
        if (questUI == null)
            questUI = Resources.FindObjectsOfTypeAll<QuestUITemplate>().FirstOrDefault();
#endif

        // quizManager 자동 바인딩
        if (quizManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            quizManager = FindFirstObjectByType<QuizManager>(FindObjectsInactive.Include);
#else
            quizManager = FindObjectOfType<QuizManager>(true);
#endif
        }

        // 규칙 전담자 자동 바인딩
        if (questAutoCompleter == null)
        {
#if UNITY_2022_2_OR_NEWER
            questAutoCompleter = FindFirstObjectByType<QuestAutoCompleter>(FindObjectsInactive.Include);
#else
            questAutoCompleter = FindObjectOfType<QuestAutoCompleter>(true);
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
        LoadYear(currentYear); // 내부에서 OnYearChanged(currentYear)까지 호출됨
    }

    // ====== 외부 이벤트 진입점 (규칙은 분리된 스크립트로 위임) ======
    public void OnBuildingInstalled(GameObject prefab, BuildingData data)
    {
        questAutoCompleter?.HandleBuildingInstalled(prefab, data, currentYear);
    }

    public void OnChatCompleted()
    {
        questAutoCompleter?.HandleChatCompleted(currentYear);
    }

    /// <summary>퀴즈에서 정답을 맞힐 때 QuizManager가 호출</summary>
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

        // UI 전담자에게 바인딩 위임 (연도표시/게이지까지 내부에서 처리)
        questUI?.BindYear(year, texts, completed);

        // 퀴즈 초기화
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

        // 규칙 모듈에 "올해 시작" 알림(퀴즈 카운터 등 내부 초기화)
        questAutoCompleter?.OnYearLoaded(year);

        uiReady = true;

        // UI 바인딩 이전에 들어온 완료 신호 처리
        while (pendingChecks.Count > 0)
            CompleteQuest_Internal(pendingChecks.Dequeue());

        OnYearChanged?.Invoke(year);
    }

    // ====== 퀘스트 완료 처리(공용) ======
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

        // 4개 모두 완료되면 타일 언락 + 다음 해 팝업(코루틴은 UI가, 연도 갱신은 매니저가)
        if (completed.All(x => x))
        {
            var tms = FindObjectOfType<TileManagerSequential>(true);
            if (tms != null) tms.UnlockTileForYear(currentYear);

            int next = Mathf.Clamp(currentYear + step, minYear, maxYear);
            if (next == currentYear) { Debug.Log("[YQM] 마지막 연도"); return; }

            if (!advancing)
            {
                advancing = true;
                StartCoroutine(AdvanceAfterPopup(next));
            }
        }
    }

    /// <summary>
    /// 다음 해 팝업(UI에서 재생) 끝난 후 실제로 연도 증가 → 로드
    /// </summary>
    private IEnumerator AdvanceAfterPopup(int nextYear)
    {
        if (questUI != null)
            yield return StartCoroutine(questUI.PlayNextYearPopup(nextYear));

        currentYear = nextYear;
        LoadYear(currentYear);
        advancing = false;
    }

    // ====== 퀴즈 연동 ======
    private void HandleYearChanged(int year)
    {
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
            StartCoroutine(DelayUpdateQuizOnce(year)); // 한 프레임 뒤 1회 재시도
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

    // ====== 유틸 ======
    public void ResetCurrent() => LoadYear(currentYear);
    public int GetCurrentYear() => currentYear;
}