using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
    [Tooltip("2045년 '좋음 이상 유지' 요구 시간(초). 기본 300초=5분")]
    [SerializeField] private float sustainSeconds2045 = 300f;   // ★ 추가

    private YearQuestManager yqm;
    private GPTChatManager gpt;

    // 내부 진행상태
    private readonly Dictionary<int, int> _yearToCorrectCount = new();
    private readonly HashSet<(int year, int questIndex)> _clearedOnce = new();

    // 2045년 지속 타이머
    private float _sustainTimer2045 = 0f; // ★ 추가

    // 연도별로 챗봇이 권장한 '조언 원문'을 저장
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>> _yearAdvice
        = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>>();

    // "조언이 등록된 이후" 설치된 건물만 처리하기 위한 대기 목록 (원문 저장)
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>> _pendingAdvice
        = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>>();

    // ★ 새: 조언(자연어) → 프리팹 매칭 토큰 맵 (간단 매핑)
    // 필요하면 여기에 매핑을 추가하세요.
    private readonly System.Collections.Generic.Dictionary<string, string> _adviceToPrefabToken
        = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // 한국어/영어 매핑 예시
        { "공원", "park" },
        { "park", "park" },
        { "공원2", "park2" },
        { "apartment", "apartment" },
        { "아파트", "apartment" },
        { "주거지", "house" },
        { "house", "house" },
        { "company", "company" },
        { "회사", "company" },
        { "smartfactory", "smartfactory" },
        { "스마트팩토리", "smartfactory" },
        { "solarplant", "solarplant" },
        { "솔라", "solarplant" },
        { "windplant", "windplant" },
        { "풍력", "windplant" },
        { "evcharger", "evcharger" },
        { "충전", "evcharger" },
        { "학교", "school" },
        { "school", "school" }
    };

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

        // 연도별 디자인 룰(기존 유지)
        if (IsFactory(prefab, data) && currentYear == 2025)
        {
            TryComplete(currentYear, QUEST_BUILDING);
            return;
        }
        if (IsZeroEmission(data) && currentYear == 2025) TryComplete(currentYear, QUEST_SATISFACTION);
        if (prefab.CompareTag("BikeRoad") && currentYear == 2030) TryComplete(currentYear, QUEST_BUILDING);
        if (prefab.CompareTag("EnergySaving") && currentYear == 2035) TryComplete(currentYear, QUEST_BUILDING);
        if (prefab.CompareTag("PublicTransport") && currentYear == 2040) TryComplete(currentYear, QUEST_BUILDING);
        if ((prefab.CompareTag("EcoPlant") || prefab.name.Contains("발전소")) && currentYear == 2045) TryComplete(currentYear, QUEST_BUILDING);

        // 프리팹의 핵심 이름 (예: "ParkPrefab(Clone)" -> "park")
        string prefabCore = GetPrefabCoreName(prefab);

        // --- 2040: 조언이 등록된 이후, 조언 원문에 설치된 프리팹이 언급되어 있으면 완료 ---
        if (currentYear == 2040)
        {
            if (_pendingAdvice.TryGetValue(currentYear, out var pending) && pending.Count > 0)
            {
                foreach (var advice in pending.ToArray())
                {
                    if (string.IsNullOrWhiteSpace(advice)) continue;
                    var adviceLower = advice.ToLower();

                    if (!string.IsNullOrEmpty(prefabCore) && adviceLower.Contains(prefabCore))
                    {
                        TryComplete(currentYear, QUEST_CHAT);
                        Debug.Log($"[QuestAutoCompleter] 2040: 조언 매칭(원문 포함). prefabCore='{prefabCore}' advice='{advice}' prefab='{prefab.name}'");
                        pending.Remove(advice);
                        return;
                    }
                }
            }
        }

        // --- 2045: 예산 상담 + 수익성 건물 설치: 조언 이후 설치된 프리팹명이 조언 원문에 포함되면 완료 ---
        if (currentYear == 2045)
        {
            if (data != null && data.incomePer5Minutes > 0 && gpt != null && gpt.IsChatCompletedForYear(2045))
            {
                if (_pendingAdvice.TryGetValue(currentYear, out var budgetPending) && budgetPending.Count > 0)
                {
                    foreach (var advice in budgetPending.ToArray())
                    {
                        if (string.IsNullOrWhiteSpace(advice)) continue;
                        var adviceLower = advice.ToLower();

                        if (!string.IsNullOrEmpty(prefabCore) && adviceLower.Contains(prefabCore))
                        {
                            TryComplete(currentYear, QUEST_CHAT);
                            Debug.Log($"[QuestAutoCompleter] 2045: 조언 매칭(원문 포함). prefabCore='{prefabCore}' advice='{advice}' prefab='{prefab.name}'");
                            budgetPending.Remove(advice);
                            return;
                        }
                    }
                    // 등록된 조언은 있으나 매칭된 것이 없음 -> 미완료
                }
                else
                {
                    // 조언 원문이 없으면 상담 플래그 + 수익성 건물 설치만으로 완료
                    TryComplete(currentYear, QUEST_CHAT);
                    Debug.Log($"[QuestAutoCompleter] 2045: 상담 플래그 + 수익성 건물 설치로 완료: {prefab.name}");
                    return;
                }
            }
        }
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
                // 좋음 이상 5분 유지
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
    
    // helper: prefab 이름에서 "Prefab", "(Clone)" 등 제거하고 핵심 토큰 반환 (소문자)
    private string GetPrefabCoreName(GameObject prefab)
    {
        if (prefab == null || string.IsNullOrEmpty(prefab.name)) return null;
        var name = prefab.name;

        // 제거 패턴들
        name = name.Replace("(clone)", "", StringComparison.OrdinalIgnoreCase);
        name = name.Replace("clone", "", StringComparison.OrdinalIgnoreCase);
        name = name.Replace("prefab", "", StringComparison.OrdinalIgnoreCase);

        // 일반적 접미사/구분자 제거
        name = name.Replace("_", " ").Replace("-", " ").Trim();

        // 숫자 접미사 예외 제거: "park2" -> "park2" (유지) ; 필요 시 변경
        // 소문자로 통일
        var core = name.ToLowerInvariant();

        // 공백 제거하여 매칭에 사용 (예: "solar plant" -> "solarplant")
        core = System.Text.RegularExpressions.Regex.Replace(core, @"\s+", "");

        return core;
    }

    // 외부(GPTChatManager 등)에서 '조언 원문'을 등록할 때 호출
    public void RegisterChatAdvice(int year, params string[] advices)
    {
        if (year <= 0 || advices == null || advices.Length == 0) return;
        if (!_yearAdvice.ContainsKey(year)) _yearAdvice[year] = new System.Collections.Generic.HashSet<string>();
        if (!_pendingAdvice.ContainsKey(year)) _pendingAdvice[year] = new System.Collections.Generic.HashSet<string>();

        foreach (var a in advices)
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            var orig = a.Trim();
            _yearAdvice[year].Add(orig);           // 원문 기록(참조용)
            _pendingAdvice[year].Add(orig);        // 대기 목록(설치 시 원문 기준으로 소비)
        }

        Debug.Log($"[QuestAutoCompleter] RegisterChatAdvice year={year} advices={string.Join(" | ", advices)}");
    }
}