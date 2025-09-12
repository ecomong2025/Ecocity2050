using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.IO;

[System.Serializable]
public class APIConfig
{
    public string openai_api_key;
}

[System.Serializable]
public class GPTMessage
{
    public string role;
    public string content;
}

[System.Serializable]
public class GPTRequest
{
    public string model;
    public GPTMessage[] messages;
    public float temperature;
    public int max_tokens;
}

[System.Serializable]
public class GPTResponse
{
    public GPTChoice[] choices;
}

[System.Serializable]
public class GPTChoice
{
    public GPTMessage message;
}

[DefaultExecutionOrder(200)]
public class GPTChatManager : MonoBehaviour
{
    [Header("GPT Settings")]
    private string apiKey;
    [SerializeField] private string apiUrl = "https://api.openai.com/v1/chat/completions";
    [SerializeField] private string model = "gpt-3.5-turbo";
    [SerializeField] private float temperature = 0.7f;
    [SerializeField] private int maxTokens = 800;

    // 연도별 채팅 완료 상태 추적
    private System.Collections.Generic.Dictionary<int, bool> yearChatCompleted =
        new System.Collections.Generic.Dictionary<int, bool>();

    private System.Collections.Generic.List<GPTMessage> conversationHistory =
        new System.Collections.Generic.List<GPTMessage>();

    void Start()
    {
        LoadAPIKey();
        SetupSystemMessage();

        // 연도별 채팅 상태 초기화
        InitializeChatStatus();
    }

    // 연도별 채팅 상태 초기화
    private void InitializeChatStatus()
    {
        yearChatCompleted[2025] = false;
        yearChatCompleted[2030] = false;
        yearChatCompleted[2035] = false;
        yearChatCompleted[2040] = false;
        yearChatCompleted[2045] = false;
    }

    // 연도 변경 시 채팅 상태 리셋 (YearQuestManager에서 호출)
    public void OnYearChanged(int newYear)
    {
        // 새 연도의 채팅 상태가 없으면 false로 초기화
        if (!yearChatCompleted.ContainsKey(newYear))
        {
            yearChatCompleted[newYear] = false;
        }

        Debug.Log($"[GPTChatManager] 연도 변경: {newYear}년, 채팅 완료 상태: {yearChatCompleted[newYear]}");
    }

    private void LoadAPIKey()
    {
        TextAsset configFile = Resources.Load<TextAsset>("api_key");

        if (configFile != null)
        {
            try
            {
                APIConfig config = JsonUtility.FromJson<APIConfig>(configFile.text);
                apiKey = config.openai_api_key;
                Debug.Log("[시스템] API 키가 성공적으로 로드되었습니다.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"API 설정 파일 로드 실패: {e.Message}");
                apiKey = "";
            }
        }
        else
        {
            Debug.LogError("API 설정 파일을 찾을 수 없습니다: Resources/api_key.json");
            apiKey = "";
        }
    }

    private void SetupSystemMessage()
    {
        int currentYear = YearQuestManager.Instance != null
            ? YearQuestManager.Instance.GetCurrentYear()
            : 2025;

        string systemMessage = $@"
당신은 도시 건설 게임의 전문 조언자입니다. 
플레이어에게 현재 게임 상황과 연도({currentYear}년)에 맞춰 구체적이고 실용적인 조언을 제공해주세요.

현재 연도: {currentYear}년
- 연도가 바뀔 때마다 시대적 상황(기술, 건물, 환경, 교통 등)을 고려한 전략 조언 제공
- 아래는 연도별 설치 가능한 건물 목록입니다.
- Road1~4, Shrub1~4, BikeLane1~4는 각각 다른 디자인의 동일 기능 건물입니다. 하나만 설치해도 효과는 같습니다.

[2025]
주거지: House (예산 -50 / 탄소 +10/5초, 최대 +30)
수입원: Factory (예산 -150, 수익 +30/5분, 최대 +300 / 탄소 +10/5초, 최대 +300)
친환경 공간: Park (예산 -100 / 즉시 -50, 이후 -10/5초, 최대 -200)
나무: Tree (예산 -10 / 즉시 -20)
교통수단: Road1/Road2/Road3/Road4 (예산 -20 / 탄소 +10/5초, 최대 +30)

[2030]
주거지: Apartment (예산 -80 / 탄소 +10/5초, 최대 +50)
수입원: Hospital (예산 -120, 수익 +20/5분, 최대 +120 / 탄소 +5/5초, 최대 +50)
친환경 공간: RecycleHub (예산 -120 / 즉시 -30, 이후 -10/5초, 최대 -100)
나무: Shrub1/Shrub2 (예산 -10 / 즉시 -10)
교통수단: BikeLane/BikeLane2/BikeLane3/BikeLane4 (예산 -20 / 즉시 -10)

[2035]
주거지: House2 (예산 -70 / 탄소 +5/5초, 최대 +10)
수입원: Company (예산 -130, 수익 +30/5분, 최대 +240 / 탄소 +10/5초, 최대 +100)
친환경 공간: Park2 (예산 -100 / 즉시 -50, 이후 -10/5초, 최대 -200)
나무: Tree2 (예산 -10 / 즉시 -20)
교통수단: BusStop (예산 -50 / 탄소 +2/5초, 최대 +10)

[2040]
주거지: Apartment2 (예산 -100 / 탄소 +5/5초, 최대 +20)
수입원: SmartFactory (예산 -180, 수익 +30/5분, 최대 +300 / 탄소 +10/5초, 최대 +150)
친환경 공간: SolarPlant (예산 -300 / 즉시 -50, 이후 -10/5초, 최대 -200)
나무: Shrub3/Shrub4 (예산 -10 / 즉시 -15)
교통수단: Subway (예산 -80 / 탄소 +1/5초, 최대 +5)

[2045]
주거지: Apartment3 (예산 -110 / 탄소 +5/5초, 최대 +15)
친환경 공간: WindPlant (예산 -350 / 즉시 -50, 이후 -10/5초, 최대 -250) (수입원 아님)
나무: Tree3 (예산 -10 / 즉시 -20)
교통수단: EVCharger (예산 -60 / 즉시 -20)
기타: School (예산 -200, 수익 없음 / 탄소 +5/5초, 최대 +50)

역할과 조언 방식:
1. 현재 설치된 건물들을 분석하여 도시 발전 상태 평가
2. 부족한 부분을 파악하고 우선순위별 개선 방안 제시
3. 예산 대비 효율적인 건물 건설 순서 추천
4. CO2와 수익의 균형을 고려한 전략적 조언
5. 설치된 건물의 시너지 효과를 고려한 다음 건물 추천

답변 규칙:
- 답변에 절대 이모지는 사용하지 마세요 대신 느낌표, 물음표 등은 자유롭게 사용 가능
- 구체적인 건물명과 수치를 포함하여 실용적인 조언 제공
- 150자 내로 간결하되 핵심적인 정보 포함

답변 스타일: 전문적이면서도 친근한 도시계획 전문가처럼 조언해주세요.
";

        GPTMessage systemMsg = new GPTMessage
        {
            role = "system",
            content = systemMessage
        };
        conversationHistory.Add(systemMsg);
    }

    private string GetBuildingStatus()
    {
        if (GameManager.Instance == null) return "";

        string buildingsInfo = GameManager.Instance.GetBuildingsInfo();
        string buildingAnalysis = GameManager.Instance.GetBuildingAnalysis();

        return $"{buildingsInfo}\n\n{buildingAnalysis}";
    }

    public void SendMessageToGPT(string userMessage, System.Action<string> onResponse)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API 키가 설정되지 않았습니다.");
            onResponse?.Invoke("API 키가 설정되지 않아 조언을 제공할 수 없습니다.");
            return;
        }

        Debug.Log($"[사용자 메시지] {userMessage}");

        string gameState = $"예산: {GameManager.Instance.budget}원, CO2: {GameManager.Instance.co2}, 만족도: {GameManager.Instance.GetSatisfactionLevel()}";
        string buildingStatus = GetBuildingStatus();

        string contextMessage = $@"[현재 도시 상황]
{gameState}

[건물 현황]
{buildingStatus}

[플레이어 질문]
{userMessage}";

        GPTMessage userMsg = new GPTMessage
        {
            role = "user",
            content = contextMessage
        };

        conversationHistory.Add(userMsg);

        if (conversationHistory.Count > 8)
        {
            var systemMsg = conversationHistory[0];
            conversationHistory.RemoveRange(1, conversationHistory.Count - 8);
            conversationHistory.Insert(0, systemMsg);
        }

        StartCoroutine(CallGPTAPI(userMessage, onResponse));
    }

    IEnumerator CallGPTAPI(string originalUserMessage, System.Action<string> onResponse)
    {
        GPTRequest request = new GPTRequest
        {
            model = model,
            messages = conversationHistory.ToArray(),
            temperature = temperature,
            max_tokens = maxTokens
        };

        string jsonData = JsonUtility.ToJson(request);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                string errorMessage = "죄송합니다. 현재 조언을 제공할 수 없습니다. 잠시 후 다시 시도해주세요.";
                Debug.LogError($"GPT API Error: {webRequest.error}");
                onResponse?.Invoke(errorMessage);
            }
            else
            {
                try
                {
                    GPTResponse response = JsonUtility.FromJson<GPTResponse>(webRequest.downloadHandler.text);
                    string botMessage = response.choices[0].message.content.Trim();

                    Debug.Log($"[GPT 응답] {botMessage}");

                    GPTMessage botMsg = new GPTMessage
                    {
                        role = "assistant",
                        content = botMessage
                    };
                    conversationHistory.Add(botMsg);

                    // 연도별 채팅 퀘스트 처리
                    ProcessChatQuest(originalUserMessage, botMessage);

                    onResponse?.Invoke(botMessage);
                }
                catch (System.Exception e)
                {
                    string parseErrorMessage = "응답을 처리하는 중 오류가 발생했습니다.";
                    Debug.LogError($"JSON Parse Error: {e.Message}");
                    onResponse?.Invoke(parseErrorMessage);
                }
            }
        }
    }

    // 연도별 채팅 퀘스트 처리
    private void ProcessChatQuest(string userMessage, string botMessage)
    {
        int currentYear = YearQuestManager.Instance != null ? YearQuestManager.Instance.GetCurrentYear() : 2025;

        if (yearChatCompleted.ContainsKey(currentYear) && yearChatCompleted[currentYear])
        {
            // 이미 완료된 연도면 리턴
            return;
        }

        switch (currentYear)
        {
            case 2025:
                // 2025년: 첫 대화 성공 시 완료
                yearChatCompleted[currentYear] = true;
                YearQuestManager.Instance?.OnChatCompleted();
                Debug.Log("[GPTChatManager] 2025년 - 시민과의 첫 대화 완료!");
                break;

            case 2030:
                // 2030년: 환경 관련 키워드 포함 시 완료
                string[] envKeywords = { "CO2", "환경", "배출", "친환경", "공원", "오염", "탄소" };
                string combinedText = (userMessage + " " + botMessage).ToLower();

                foreach (string keyword in envKeywords)
                {
                    if (combinedText.Contains(keyword.ToLower()))
                    {
                        yearChatCompleted[currentYear] = true;
                        YearQuestManager.Instance?.OnChatCompleted();
                        Debug.Log($"[GPTChatManager] 2030년 - 환경 정책 상담 완료! (키워드: {keyword})");
                        break;
                    }
                }
                break;

            case 2035:
                // 2035년: 도시 발전 관련 키워드 포함 시 완료
                string[] devKeywords = { "건설", "건물", "계획", "발전", "개발", "확장", "도시", "시설" };
                string combinedText2035 = (userMessage + " " + botMessage).ToLower();

                foreach (string keyword in devKeywords)
                {
                    if (combinedText2035.Contains(keyword.ToLower()))
                    {
                        yearChatCompleted[currentYear] = true;
                        YearQuestManager.Instance?.OnChatCompleted();
                        Debug.Log($"[GPTChatManager] 2035년 - 도시 발전 계획 논의 완료! (키워드: {keyword})");
                        break;
                    }
                }
                break;

            case 2040:
                // 2040년: 예산 상담 -> 관련 수익성 건물 키워드 등록하고, 상담 플래그만 세워둠 (건물 설치 시 QuestAutoCompleter가 처리)
                string[] budgetKeywords = { "예산", "돈", "수익", "재정", "비용", "경제", "수입", "자금" };
                string combinedText2040 = (userMessage + " " + botMessage).ToLower();
                var foundBudget = new System.Collections.Generic.List<string>();

                foreach (string keyword in budgetKeywords)
                {
                    if (combinedText2040.Contains(keyword.ToLower()))
                        foundBudget.Add(keyword);
                }

                if (foundBudget.Count > 0)
                {
                    var qa = UnityEngine.Object.FindObjectOfType<QuestAutoCompleter>();
                    if (qa != null)
                        // 예산 상담도 '조언 원문'을 저장
                        qa.RegisterChatAdvice(currentYear, botMessage);

                    // 상담 상태는 내부 플래그만 표시. YearQuestManager에 바로 알리지 않음.
                    yearChatCompleted[currentYear] = true;

                    Debug.Log($"[GPTChatManager] 2040년 - 예산 상담 감지 및 원문 등록(수익성 건물 설치 대기): {botMessage}");
                }
                break;
            
            case 2045:
                // 2045년: 봇의 조언과 관련된 건물 설치 체크는 QuestAutoCompleter에서 처리하도록 변경
                // 여기서는 조언이 포함된 대화인지만 확인하고, QuestAutoCompleter에 조언 키워드 등록 (대화만으로는 퀘스트 완료하지 않음)
                string[] adviceKeywords = { "공원", "친환경", "상업", "건설", "설치", "추천", "제안", "에너지", "충전", "탑재" };
                string botText = (botMessage ?? "").ToLower();
                var foundKeywords = new System.Collections.Generic.List<string>();

                foreach (string keyword in adviceKeywords)
                {
                    if (botText.Contains(keyword.ToLower()))
                    {
                        foundKeywords.Add(keyword);
                    }
                }

                if (foundKeywords.Count > 0)
                {
                    // GPT가 조언한 '원문' 전체(botMessage)를 QuestAutoCompleter에 등록하도록 변경
                    var qa = UnityEngine.Object.FindObjectOfType<QuestAutoCompleter>();
                    if (qa != null)
                        qa.RegisterChatAdvice(currentYear, botMessage);

                    Debug.Log($"[GPTChatManager] 2045년 - 조언 대화 감지 및 등록(원문 저장, 건물 설치 대기): {botMessage}");
                }
                break;
        }
    }

    public void ClearConversation()
    {
        conversationHistory.Clear();
        SetupSystemMessage();
        // 대화 기록 초기화 시 현재 연도의 채팅 상태만 리셋
        int currentYear = YearQuestManager.Instance?.GetCurrentYear() ?? 2025;
        if (yearChatCompleted.ContainsKey(currentYear))
        {
            yearChatCompleted[currentYear] = false;
        }
        Debug.Log("[시스템] 대화 기록이 초기화되었습니다.");
    }

    // 연도별 채팅 완료 상태 확인 -> 디버그용
    public bool IsChatCompletedForYear(int year)
    {
        return yearChatCompleted.ContainsKey(year) && yearChatCompleted[year];
    }
}