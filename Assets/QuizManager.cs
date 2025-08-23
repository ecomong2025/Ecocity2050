using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class QuizItem
{
    public string question;
    public List<string> options;
    public int answerIndex;
    public string hint;
    public string wrongNote;
}

[System.Serializable]
public class QuizYearData
{
    public int year;
    public List<QuizItem> quiz;
}

[DefaultExecutionOrder(-100)]
public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }
    public bool IsReady { get; private set; }

    [Header("Panels")]
    public GameObject startPanel;
    public GameObject quizPanel;
    public GameObject quizResultPanel;
    public GameObject correctPanel;
    public GameObject incorrectPanel;

    public GameObject gamePanel;
    public GameObject quizMainPanel;

    [Header("Quiz Elements")]
    public TMP_Text questionText;
    public List<TMP_Text> optionTexts;
    public List<Button> optionButtons;

    [Header("Hint Elements")]
    public GameObject hintBubble;
    public TMP_Text hintText;
    public Button hintButton;

    [Header("Result Elements")]
    public TMP_Text reasonText;

    [Header("Timer")]
    public QuizTimer quizTimer;

    private List<QuizYearData> quizDataArray = new List<QuizYearData>();
    private List<QuizItem> filteredQuizzes = new List<QuizItem>();

    private int currentQuizIndex = 0;
    private bool isHintVisible = false;
    private bool isAnswered = false;

    // 퀘스트 관련
    private int quizCorrectCount = 0;  // 지금까지 맞춘 퀴즈 개수
    private int quizQuestIndex = 2;    // 퀴즈 관련 퀘스트 인덱스
    private int completeThreshold = 2; // 몇 개 맞추면 퀘스트 완료 처리할지 기준

    // 하루 제한 관련
    private int dailyQuizCount = 0;
    private int dailyLimit = 5;  // 하루 5개
    private DateTime lastResetTime;

    // 이미 푼 퀴즈 인덱스
    private HashSet<int> usedQuizIndices = new HashSet<int>();

    private Dictionary<int, int> yearCorrectThreshold = new Dictionary<int, int>
    {
        {2025, 2},
        {2030, 3},
        {2035, 4},
        {2040, 5},
        {2045, 6},
        {2050, 7}
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        SafeSetActive(gamePanel, false);
        SafeSetActive(quizMainPanel, true);

        SafeSetActive(startPanel, true);
        SafeSetActive(quizPanel, false);
        SafeSetActive(quizResultPanel, false);
        SafeSetActive(correctPanel, false);
        SafeSetActive(incorrectPanel, false);

        // 데이터 먼저 로드
        IsReady = LoadQuizData();

        // 일일 제한 로드
        LoadDailyQuizData();

        // 힌트 버튼
        if (hintBubble) hintBubble.SetActive(false);
        if (hintButton != null) hintButton.onClick.AddListener(ToggleHint);

        // 선택지 버튼 연결
        for (int i = 0; i < optionButtons.Count; i++)
        {
            int index = i;
            if (optionButtons[i] != null)
                optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }

        if (quizTimer != null)
            quizTimer.OnTimeout = HandleTimeout;
        else
            Debug.LogWarning("[Quiz] quizTimer가 할당되지 않았습니다.");

        // 초기 연도 필터링은 YearQuestManager의 OnYearChanged에서 통일 처리
    }

    private void SafeSetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    public void UpdateYearQuiz(int year)
    {
        if (!IsReady)
        {
            // 로드가 늦게 끝났다면 재시도
            IsReady = LoadQuizData();
            if (!IsReady)
            {
                Debug.LogError("[Quiz] UpdateYearQuiz 호출 시점에 데이터 로드 실패/지연");
                filteredQuizzes.Clear();
                ResetQuizCorrectCount();
                return;
            }
        }

        FilterQuizByYear(year);
        ResetQuizCorrectCount(); // 정답 카운트 초기화

        if (yearCorrectThreshold.TryGetValue(year, out var th))
            completeThreshold = th;
        else
            completeThreshold = 2; // 기본값
    }

    // 하루 제한 불러오기
    private void LoadDailyQuizData()
    {
        dailyQuizCount = PlayerPrefs.GetInt("DailyQuizCount", 0);

        string timeStr = PlayerPrefs.GetString("LastResetTime", "");
        if (string.IsNullOrEmpty(timeStr))
        {
            lastResetTime = DateTime.UtcNow;
            SaveDailyQuizData();
            return;
        }

        lastResetTime = DateTime.Parse(timeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

        // 테스트용: 30초 후 초기화
        if ((DateTime.UtcNow - lastResetTime).TotalSeconds >= 30)
        {
            ResetDailyQuizCount();
        }
    }

    private void SaveDailyQuizData()
    {
        PlayerPrefs.SetInt("DailyQuizCount", dailyQuizCount);
        PlayerPrefs.SetString("LastResetTime", lastResetTime.ToString("o")); // Roundtrip format
        PlayerPrefs.Save();
    }

    public void ResetDailyQuizCount()
    {
        dailyQuizCount = 0;
        lastResetTime = DateTime.UtcNow;
        SaveDailyQuizData();
        Debug.Log("퀴즈 제한이 초기화되었습니다.");
    }

    public void ResetQuizUI()
    {
        SafeSetActive(startPanel, true);
        SafeSetActive(quizPanel, false);
        SafeSetActive(quizResultPanel, false);
        SafeSetActive(correctPanel, false);
        SafeSetActive(incorrectPanel, false);
        if (hintBubble) hintBubble.SetActive(false);
        isAnswered = false;
        isHintVisible = false;
    }

    public void OnGameStart()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[Quiz] 데이터가 준비되지 않았습니다.");
            return;
        }

        LoadDailyQuizData(); // 매번 시작할 때 검사

        if (dailyQuizCount >= dailyLimit)
        {
            Debug.Log("오늘은 더 이상 퀴즈를 풀 수 없습니다!");
            return;
        }

        // 푼 적 없는 문제 찾기
        if (filteredQuizzes == null || filteredQuizzes.Count == 0)
        {
            Debug.Log("해당 연도 퀴즈가 없습니다.");
            return;
        }

        List<int> availableIndices = Enumerable.Range(0, filteredQuizzes.Count)
                                               .Where(i => !usedQuizIndices.Contains(i))
                                               .ToList();

        if (availableIndices.Count == 0)
        {
            Debug.Log("더 이상 풀 수 있는 퀴즈가 없습니다!");
            return;
        }

        currentQuizIndex = availableIndices[UnityEngine.Random.Range(0, availableIndices.Count)];
        usedQuizIndices.Add(currentQuizIndex);

        SafeSetActive(startPanel, false);
        SafeSetActive(quizPanel, true);
        SafeSetActive(quizResultPanel, false);
        SafeSetActive(correctPanel, false);
        SafeSetActive(incorrectPanel, false);

        DisplayQuiz(currentQuizIndex);
        if (quizTimer != null) quizTimer.StartTimer();
        isAnswered = false;
    }

    public void OnRetryQuiz()
    {
        OnGameStart(); // Retry도 사실상 새 퀴즈 시작
    }

    public void OnBackToGame()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.CloseQuiz();
        else
            Debug.LogWarning("[Quiz] GameManager.Instance가 없습니다.");
    }

    private bool LoadQuizData()
    {
        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("Quiz/quiz");
            if (jsonFile == null)
            {
                Debug.LogError("Quiz/quiz.json 파일이 없습니다! (Resources/Quiz/quiz.json)");
                quizDataArray = new List<QuizYearData>();
                return false;
            }

            var wrapped = JsonUtility.FromJson<Wrapper<QuizYearData>>(FixJson(jsonFile.text));
            if (wrapped == null || wrapped.items == null)
            {
                Debug.LogError("[Quiz] JSON 파싱 실패(wrapped/items null).");
                quizDataArray = new List<QuizYearData>();
                return false;
            }

            quizDataArray = wrapped.items.ToList();
            // 내부 null 정리
            foreach (var yd in quizDataArray)
                if (yd.quiz == null) yd.quiz = new List<QuizItem>();

            Debug.Log($"[Quiz] 전체 연도 그룹 {quizDataArray.Count}개 로드됨");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Quiz] LoadQuizData 예외: {e.Message}");
            quizDataArray = new List<QuizYearData>();
            return false;
        }
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] items;
    }

    private string FixJson(string value)
    {
        // 루트가 배열인 JSON을 감싸기 위한 래퍼
        return "{\"items\":" + value + "}";
    }

    private void FilterQuizByYear(int year)
    {
        filteredQuizzes.Clear();

        if (quizDataArray == null || quizDataArray.Count == 0)
        {
            Debug.LogWarning("[Quiz] quizDataArray가 비어있습니다.");
            return;
        }

        var yearData = quizDataArray.FirstOrDefault(y => y != null && y.year == year);
        if (yearData == null || yearData.quiz == null)
        {
            Debug.LogWarning($"[Quiz] {year}년 데이터가 없거나 quiz가 null입니다.");
            return;
        }

        filteredQuizzes.AddRange(yearData.quiz.Where(q => q != null));
        Debug.Log($"{year}년 퀴즈 {filteredQuizzes.Count}개 필터링됨");

        // 새 연도 진입 시, 이미 푼 목록 초기화
        usedQuizIndices.Clear();
    }

    private void DisplayQuiz(int index)
    {
        if (filteredQuizzes == null || filteredQuizzes.Count == 0)
        {
            Debug.LogWarning("[Quiz] 표시할 퀴즈가 없습니다.");
            return;
        }

        if (index < 0 || index >= filteredQuizzes.Count)
        {
            Debug.LogWarning($"[Quiz] 잘못된 인덱스 {index}");
            return;
        }

        var quiz = filteredQuizzes[index];
        if (quiz == null)
        {
            Debug.LogWarning("[Quiz] quiz가 null입니다.");
            return;
        }

        if (questionText != null) questionText.text = quiz.question ?? "";

        for (int i = 0; i < optionTexts.Count; i++)
        {
            bool hasOption = (quiz.options != null && i < quiz.options.Count);
            if (i < optionButtons.Count && optionButtons[i] != null)
                optionButtons[i].gameObject.SetActive(hasOption);

            if (hasOption && optionTexts[i] != null)
                optionTexts[i].text = quiz.options[i] ?? "";
        }

        if (hintBubble) hintBubble.SetActive(false);
        isHintVisible = false;
        isAnswered = false;
    }

    private void ToggleHint()
    {
        if (hintBubble == null || hintText == null) return;

        isHintVisible = !isHintVisible;
        hintBubble.SetActive(isHintVisible);

        if (isHintVisible)
        {
            if (filteredQuizzes != null &&
                currentQuizIndex >= 0 &&
                currentQuizIndex < filteredQuizzes.Count &&
                filteredQuizzes[currentQuizIndex] != null)
            {
                hintText.text = filteredQuizzes[currentQuizIndex].hint ?? "";
            }
            else
            {
                hintText.text = "";
            }
        }
    }

    private void OnOptionSelected(int selectedIndex)
    {
        if (isAnswered) return;
        isAnswered = true;
        if (quizTimer != null) quizTimer.StopTimer();

        if (filteredQuizzes == null ||
            currentQuizIndex < 0 ||
            currentQuizIndex >= filteredQuizzes.Count ||
            filteredQuizzes[currentQuizIndex] == null)
        {
            Debug.LogWarning("[Quiz] 정답 확인 불가(데이터 미존재).");
            ShowIncorrectPanel();
            if (reasonText) reasonText.text = "";
            return;
        }

        var quiz = filteredQuizzes[currentQuizIndex];

        dailyQuizCount++;
        SaveDailyQuizData();

        if (selectedIndex == quiz.answerIndex)
        {
            Debug.Log("✅ 정답입니다!");
            if (GameManager.Instance != null) GameManager.Instance.AddBudget(30);
            ShowCorrectPanel();

            quizCorrectCount++;  // 맞춘 개수 증가

            // 연도에 맞는 퀘스트 완료
            int year = (YearQuestManager.Instance != null) ? YearQuestManager.Instance.GetCurrentYear() : 0;
            if (quizCorrectCount >= completeThreshold)
            {
                if (YearQuestManager.Instance != null)
                    YearQuestManager.Instance.CompleteQuest(quizQuestIndex);
            }
        }
        else
        {
            Debug.Log("❌ 오답입니다!");
            ShowIncorrectPanel();
            if (reasonText) reasonText.text = quiz.wrongNote ?? "";
        }
    }

    // 퀴즈 개수 초기화
    public void ResetQuizCorrectCount()
    {
        quizCorrectCount = 0;
    }

    private void HandleTimeout()
    {
        if (isAnswered) return;
        isAnswered = true;

        Debug.Log("⏰ 시간 초과 오답 처리");

        dailyQuizCount++;
        SaveDailyQuizData();

        ShowIncorrectPanel();
        if (reasonText != null &&
            filteredQuizzes != null &&
            currentQuizIndex >= 0 &&
            currentQuizIndex < filteredQuizzes.Count &&
            filteredQuizzes[currentQuizIndex] != null)
        {
            reasonText.text = filteredQuizzes[currentQuizIndex].wrongNote ?? "";
        }
    }

    private void ShowCorrectPanel()
    {
        SafeSetActive(quizPanel, false);
        SafeSetActive(quizResultPanel, true);
        SafeSetActive(correctPanel, true);
        SafeSetActive(incorrectPanel, false);
    }

    private void ShowIncorrectPanel()
    {
        SafeSetActive(quizPanel, false);
        SafeSetActive(quizResultPanel, true);
        SafeSetActive(correctPanel, false);
        SafeSetActive(incorrectPanel, true);
    }
}