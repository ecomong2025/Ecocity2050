using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    private List<QuizYearData> quizDataArray;
    private List<QuizItem> filteredQuizzes = new List<QuizItem>();

    private int currentQuizIndex = 0;
    private bool isHintVisible = false;
    private bool isAnswered = false;

    //퀘스트 관련
    private int quizCorrectCount = 0;  // 지금까지 맞춘 퀴즈 개수
    private int quizQuestIndex = 2;    // 퀴즈 관련 퀘스트 인덱스
    private int completeThreshold = 2; // 몇 개 맞추면 퀘스트 완료 처리할지 기준


    void Start()
    {
        gamePanel.SetActive(false);
        quizMainPanel.SetActive(true);

        startPanel.SetActive(true);
        quizPanel.SetActive(false);
        quizResultPanel.SetActive(false);

    LoadQuizData();
        FilterQuizByYear(2025);

        hintBubble.SetActive(false);
        hintButton.onClick.AddListener(ToggleHint);

        for (int i = 0; i < optionButtons.Count; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }

        quizTimer.OnTimeout = HandleTimeout;
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
        startPanel.SetActive(false);
        quizPanel.SetActive(true);
        quizResultPanel.SetActive(false);
        correctPanel.SetActive(false);
        incorrectPanel.SetActive(false);

        currentQuizIndex = Random.Range(0, filteredQuizzes.Count);
        DisplayQuiz(currentQuizIndex);
        quizTimer.StartTimer();
        isAnswered = false;
    }

    public void OnRetryQuiz()
    {
        correctPanel.SetActive(false);
        incorrectPanel.SetActive(false);
        quizResultPanel.SetActive(false);
        quizPanel.SetActive(true);

        currentQuizIndex = Random.Range(0, filteredQuizzes.Count);
        DisplayQuiz(currentQuizIndex);
        quizTimer.StartTimer();
        isAnswered = false;
    }

    public void OnBackToGame()
    {
        GameManager.Instance.CloseQuiz();
    }

    void LoadQuizData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("quiz/quiz");
        if (jsonFile == null)
        {
            Debug.LogError("quiz/quiz.json 파일이 없습니다!");
            return;
        }

        quizDataArray = JsonUtility.FromJson<Wrapper<QuizYearData>>(FixJson(jsonFile.text)).items.ToList();
        Debug.Log($"전체 연도 그룹 {quizDataArray.Count}개 로드됨");
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

    void FilterQuizByYear(int year)
    {
        filteredQuizzes.Clear();

        foreach (var yearData in quizDataArray)
        {
            if (yearData.year == year)
            {
                filteredQuizzes.AddRange(yearData.quiz);
                break;
            }
        }

        Debug.Log($"{year}년 퀴즈 {filteredQuizzes.Count}개 필터링됨");
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

        if (selectedIndex == quiz.answerIndex)
        {
            Debug.Log("✅ 정답입니다!");
            GameManager.Instance.AddBudget(30);
            ShowCorrectPanel();

            quizCorrectCount++;  // 맞춘 개수 증가

            // 기준 넘었으면 YearQuestManager에 완료 알림
            if (quizCorrectCount >= completeThreshold)
            {
                if (YearQuestManager.Instance != null)
                {
                    Debug.Log("YearQuestManager.Instance is NOT null - CompleteQuest 호출");
                    YearQuestManager.Instance.CompleteQuest(quizQuestIndex);
                }
                else
                {
                    Debug.LogError("YearQuestManager.Instance is NULL!");
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

    //퀴즈 개수 초기화
    public void ResetQuizCorrectCount()
    {
        quizCorrectCount = 0;
    }

    void HandleTimeout()
    {
        if (isAnswered) return;
        isAnswered = true;

        Debug.Log("⏰ 시간 초과 오답 처리");
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
