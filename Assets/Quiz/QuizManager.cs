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

public class QuizManager : MonoBehaviour
{
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

    private int defaultYear = 2025;

    public QuizlimitController quizLimitController; //기회소진 안내 매니저
    public bool IsReady { get; private set; } = false;

    private Dictionary<int, int> yearCorrectThreshold = new Dictionary<int, int>
{
    {2025, 2},
    {2030, 3},
    {2035, 4},
    {2040, 5},
    {2045, 6},
    {2050, 7}
};

    void Start()
    {
        gamePanel.SetActive(false);
        quizMainPanel.SetActive(true);

        startPanel.SetActive(true);
        quizPanel.SetActive(false);
        quizResultPanel.SetActive(false);

        LoadQuizData();
        UpdateYearQuiz(defaultYear);

        LoadDailyQuizData();

        hintBubble.SetActive(false);
        hintButton.onClick.AddListener(ToggleHint);

        for (int i = 0; i < optionButtons.Count; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }

        quizTimer.OnTimeout = HandleTimeout;
    }

    void LoadQuizData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Quiz/quiz");
        if (jsonFile == null)
        {
            Debug.LogError("quiz/quiz.json 파일이 없습니다!");
            return;
        }

        try
        {
            var wrapper = JsonUtility.FromJson<Wrapper<QuizYearData>>(jsonFile.text);
            if (wrapper == null || wrapper.items == null)
            {
                Debug.LogError("JSON 파싱 실패 또는 items가 null");
                return;
            }

            quizDataArray = wrapper.items.ToList();

            // 확인 로그
            foreach (var y in quizDataArray)
            {
                Debug.Log($"Year: {y.year}, Quiz count: {y.quiz?.Count}");
            }

            Debug.Log($"전체 연도 그룹 {quizDataArray.Count}개 로드됨");
            IsReady = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"퀴즈 로드 중 오류 발생: {e.Message}");
        }
    }

    void FilterQuizByYear(int year)
    {
        filteredQuizzes.Clear();

        var yearData = quizDataArray.FirstOrDefault(q => q.year == year);
        if (yearData != null && yearData.quiz != null && yearData.quiz.Count > 0)
        {
            filteredQuizzes.AddRange(yearData.quiz);
            Debug.Log($"{year}년 퀴즈 {filteredQuizzes.Count}개 필터링됨");
        }
        else
        {
            // 경고 대신 그냥 무시
            Debug.Log($"{year}년 데이터 없음, 필터링 건너뜀");
        }
    }


    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] items;
    }

    string FixJson(string value)
    {
        return "{\"items\":" + value + "}";
    }

    public void UpdateYearQuiz(int year)
    {
        FilterQuizByYear(year);
        ResetQuizCorrectCount(); // 정답 카운트 초기화

        completeThreshold = yearCorrectThreshold.ContainsKey(year) ? yearCorrectThreshold[year] : 2;
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
        startPanel.SetActive(true);
        quizPanel.SetActive(false);
        quizResultPanel.SetActive(false);
        correctPanel.SetActive(false);
        incorrectPanel.SetActive(false);
        hintBubble.SetActive(false);
        isAnswered = false;
        isHintVisible = false;
    }

    public void OnGameStart()
    {
        LoadDailyQuizData(); // 매번 시작할 때 검사

        if (dailyQuizCount >= dailyLimit)
        {
            Debug.Log("오늘은 더 이상 퀴즈를 풀 수 없습니다!");
            quizMainPanel.SetActive(false);
            quizLimitController.ShowLimitPanel();
            return;
        }

        // 푼 적 없는 문제 찾기
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

        startPanel.SetActive(false);
        quizPanel.SetActive(true);
        quizResultPanel.SetActive(false);
        correctPanel.SetActive(false);
        incorrectPanel.SetActive(false);

        DisplayQuiz(currentQuizIndex);
        quizTimer.StartTimer();
        isAnswered = false;
    }

    public void OnRetryQuiz()
    {
        OnGameStart(); // Retry도 사실상 새 퀴즈 시작
    }

    public void OnBackToGame()
    {
        GameManager.Instance.CloseQuiz();
    }


    void DisplayQuiz(int index)
    {
        QuizItem quiz = filteredQuizzes[index];
        questionText.text = quiz.question;

        for (int i = 0; i < optionTexts.Count; i++)
        {
            if (i < quiz.options.Count)
            {
                optionTexts[i].text = quiz.options[i];
                optionButtons[i].gameObject.SetActive(true);
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }

        hintBubble.SetActive(false);
        isHintVisible = false;
        isAnswered = false;
    }

    void ToggleHint()
    {
        isHintVisible = !isHintVisible;
        hintBubble.SetActive(isHintVisible);

        if (isHintVisible)
        {
            hintText.text = filteredQuizzes[currentQuizIndex].hint;
        }
    }

    void OnOptionSelected(int selectedIndex)
    {
        if (isAnswered) return;
        isAnswered = true;
        quizTimer.StopTimer();

        QuizItem quiz = filteredQuizzes[currentQuizIndex];

        dailyQuizCount++;
        SaveDailyQuizData();

        if (selectedIndex == quiz.answerIndex)
        {
            Debug.Log("✅ 정답입니다!");
            GameManager.Instance.AddBudget(30);
            ShowCorrectPanel();

            quizCorrectCount++;  // 맞춘 개수 증가

            // 연도에 맞는 퀴스트 완료
            int year = YearQuestManager.Instance.GetCurrentYear();
            if (quizCorrectCount >= completeThreshold)
            {
                if (YearQuestManager.Instance != null)
                {
                    YearQuestManager.Instance.CompleteQuest(quizQuestIndex);
                }
            }
        }
        else
        {
            Debug.Log("❌ 오답입니다!");
            ShowIncorrectPanel();
            reasonText.text = quiz.wrongNote;
        }
    }

    // 퀴즈 개수 초기화
    public void ResetQuizCorrectCount()
    {
        quizCorrectCount = 0;
    }

    void HandleTimeout()
    {
        if (isAnswered) return;
        isAnswered = true;

        Debug.Log("⏰ 시간 초과 오답 처리");

        dailyQuizCount++;
        SaveDailyQuizData();

        ShowIncorrectPanel();
        reasonText.text = filteredQuizzes[currentQuizIndex].wrongNote;
    }

    void ShowCorrectPanel()
    {
        quizPanel.SetActive(false);
        quizResultPanel.SetActive(true);
        correctPanel.SetActive(true);
        incorrectPanel.SetActive(false);
    }

    void ShowIncorrectPanel()
    {
        quizPanel.SetActive(false);
        quizResultPanel.SetActive(true);
        correctPanel.SetActive(false);
        incorrectPanel.SetActive(true);
    }
}
