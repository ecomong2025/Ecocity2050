using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class QuizRequirement
{
    [Range(2025, 2045)] public int year = 2025;
    [Range(1, 10)] public int requiredCorrect = 2; // 이 해에 필요한 정답 수
}

/// <summary>
/// 퀘스트 자동완성 "규칙" 전담 스크립트.
/// 인덱스 고정: 0=건물, 1=시민 만족도(제로에미션 포함), 2=퀴즈, 3=시민 챗봇
/// </summary>
public class QuestAutoCompleter : MonoBehaviour
{
    // ===== 고정 인덱스 매핑 =====
    private const int QUEST_BUILDING = 0;
    private const int QUEST_SATISFACTION = 1;
    private const int QUEST_QUIZ = 2;
    private const int QUEST_CHAT = 3;

    [Header("Quiz-based Requirements (연도별)")]
    [SerializeField] private List<QuizRequirement> quizRequirements = new();

    [Header("Satisfaction Settings")]
    [Tooltip("2045년 '좋음 이상 유지' 요구 시간(초). 기본 600초=10분")]
    [SerializeField] private float sustainSeconds2045 = 600f;   // ★ 추가

    private YearQuestManager yqm;
    private GPTChatManager gpt;

    // 내부 진행상태
    private readonly Dictionary<int, int> _yearToCorrectCount = new();
    private readonly HashSet<(int year, int questIndex)> _clearedOnce = new();

    // 2045년 지속 타이머
    private float _sustainTimer2045 = 0f; // ★ 추가

    void Awake()
    {
#if UNITY_2022_2_OR_NEWER
        yqm = FindFirstObjectByType<YearQuestManager>(FindObjectsInactive.Include);
        gpt = FindFirstObjectByType<GPTChatManager>(FindObjectsInactive.Include);
#else
        yqm = FindObjectByType<YearQuestManager>(true);
        if (yqm == null) yqm = FindObjectOfType<YearQuestManager>(true);
        gpt = FindObjectOfType<GPTChatManager>(true);
#endif
    }

    public void OnYearLoaded(int year)
    {
        _yearToCorrectCount[year] = 0;
        _sustainTimer2045 = 0f; // ★ 추가: 연도 바뀌면 타이머 리셋
        // 필요하면 같은 연도의 잠금 초기화:
        // _clearedOnce.RemoveWhere(t => t.year == year);
    }

    // ====== 빌딩 조건 ======
    public void HandleBuildingInstalled(GameObject prefab, BuildingData data, int currentYear)
    {
        if (prefab == null || data == null || yqm == null) return;

        // 연도별 디자인 룰(유지)
        if (IsFactory(prefab, data) && currentYear == 2025) TryComplete(currentYear, QUEST_BUILDING);
        if (IsZeroEmission(data) && currentYear == 2025) TryComplete(currentYear, QUEST_SATISFACTION);
        if (prefab.CompareTag("BikeRoad") && currentYear == 2030) TryComplete(currentYear, QUEST_BUILDING);
        if (prefab.CompareTag("EnergySaving") && currentYear == 2035) TryComplete(currentYear, QUEST_BUILDING);
        if (prefab.CompareTag("PublicTransport") && currentYear == 2040) TryComplete(currentYear, QUEST_BUILDING);
        if ((prefab.CompareTag("EcoPlant") || prefab.name.Contains("발전소")) && currentYear == 2045) TryComplete(currentYear, QUEST_BUILDING);

        // 2045: 수익 건물 + GPT 대화 완료 → 챗봇 퀘스트
        if (currentYear == 2045 && data.incomePer5Minutes > 0 && gpt != null && gpt.IsChatCompletedForYear(2045))
            TryComplete(currentYear, QUEST_CHAT);
    }

    // ====== 채팅 조건 ======
    public void HandleChatCompleted(int currentYear)
    {
        if (yqm == null) return;
        TryComplete(currentYear, QUEST_CHAT);
    }

    // ====== 퀴즈 조건 ======
    public void HandleQuizCorrect(int currentYear)
    {
        if (yqm == null) return;

        if (!_yearToCorrectCount.ContainsKey(currentYear))
            _yearToCorrectCount[currentYear] = 0;

        _yearToCorrectCount[currentYear]++;

        for (int i = 0; i < quizRequirements.Count; i++)
        {
            var req = quizRequirements[i];
            if (req == null || req.year != currentYear) continue;

            var key = (year: currentYear, questIndex: QUEST_QUIZ);
            if (_clearedOnce.Contains(key)) continue;

            if (_yearToCorrectCount[currentYear] >= req.requiredCorrect)
            {
                TryComplete(currentYear, QUEST_QUIZ);
                _clearedOnce.Add(key);
            }
        }
    }

    // ====== 시민 만족도 조건 (기획 반영) ======
    void Update() // 시민 만족도 체크
    {
        if (yqm == null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;

        int year = yqm.GetCurrentYear();
        string level = gm.GetSatisfactionLevel(); // "매우 좋음" / "좋음" / "보통" 등

        // 이미 완료된 경우 더 확인 안 함
        if (_clearedOnce.Contains((year, QUEST_SATISFACTION))) return;

        switch (year)
        {
            case 2030:
                // 보통 이상
                if (level == "보통" || level == "좋음" || level == "매우 좋음")
                    TryComplete(year, QUEST_SATISFACTION);
                break;

            case 2035:
                // 좋음 이상
                if (level == "좋음" || level == "매우 좋음")
                    TryComplete(year, QUEST_SATISFACTION);
                break;

            case 2040:
                // 매우 좋음
                if (level == "매우 좋음")
                    TryComplete(year, QUEST_SATISFACTION);
                break;

            case 2045:
                // 좋음 이상 10분 유지
                if (level == "좋음" || level == "매우 좋음")
                {
                    _sustainTimer2045 += Time.deltaTime;
                    if (_sustainTimer2045 >= sustainSeconds2045)
                        TryComplete(year, QUEST_SATISFACTION);
                }
                else
                {
                    _sustainTimer2045 = 0f; // 조건 깨지면 리셋
                }
                break;
        }
    }

    // ====== 내부 유틸 ======
    private void TryComplete(int year, int questIndex)
    {
        var key = (year, questIndex);
        if (_clearedOnce.Contains(key)) return; // 같은 연도 같은 퀘스트 중복 방지

        yqm.CompleteQuest(questIndex);
        _clearedOnce.Add(key);
    }

    private bool IsFactory(GameObject prefab, BuildingData data)
    {
        if (prefab != null && prefab.CompareTag("Factory")) return true;
        var n = prefab != null ? prefab.name.ToLower() : "";
        return n.Contains("factory") || n.Contains("plant") || n.Contains("발전소");
    }

    private bool IsZeroEmission(BuildingData data)
    {
        return data.co2PerSecond <= 0f
            && data.instantCO2Change <= 0f
            && data.maxCO2Change <= 0f;
    }
}