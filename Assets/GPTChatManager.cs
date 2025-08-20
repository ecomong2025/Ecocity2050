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

[2025]
주거지: 주택 (예산 -50 / 탄소 +10/5초, 최대 +30)
수입원: 공장 (예산 -150, 수익 +30/5분, 최대 +300 / 탄소 +10/5초, 최대 +300)
친환경 공간: 공원 (예산 -100 / 즉시 -50, 이후 -10/5초, 최대 -200)
나무: 기본 나무 (예산 -10 / 즉시 -20)
교통수단: 일반 도로 (예산 -20 / 탄소 +10/5초, 최대 +30)

[2030]
주거지: 아파트 (예산 -80 / 탄소 +10/5초, 최대 +50)
수입원: 병원 (예산 -120, 수익 +20/5분, 최대 +120 / 탄소 +5/5초, 최대 +50)
친환경 공간: 재활용 센터 (예산 -120 / 즉시 -30, 이후 -10/5초, 최대 -100)
나무: 덤불 (예산 -10 / 즉시 -10)
교통수단: 자전거 도로 (예산 -20 / 즉시 -10)

[2035]
주거지: 에너지 절약형 주택 (예산 -70 / 탄소 +5/5초, 최대 +10)
수입원: 회사 (예산 -130, 수익 +30/5분, 최대 +240 / 탄소 +10/5초, 최대 +100)
친환경 공간: 공원2 (예산 -100 / 즉시 -50, 이후 -10/5초, 최대 -200)
나무: 나무2 (예산 -10 / 즉시 -20)
교통수단: 버스 정류장 (예산 -50 / 탄소 +2/5초, 최대 +10)

[2040]
주거지: 에너지 절약형 아파트 (예산 -100 / 탄소 +5/5초, 최대 +20)
수입원: 스마트 팩토리 (예산 -180, 수익 +30/5분, 최대 +300 / 탄소 +10/5초, 최대 +150)
친환경 공간: 태양광 발전소 (예산 -300 / 즉시 -50, 이후 -10/5초, 최대 -200)
나무: 꽃 있는 덤불 (예산 -10 / 즉시 -15)
교통수단: 지하철 입구 (예산 -80 / 탄소 +1/5초, 최대 +5)

[2045]
주거지: 에너지 절약형 아파트2 (예산 -110 / 탄소 +5/5초, 최대 +15)
수입원: 학교 (예산 -200, 수익 없음 / 탄소 +5/5초, 최대 +50)
친환경 공간: 풍력 발전소 (예산 -350 / 즉시 -50, 이후 -10/5초, 최대 -250)
나무: 벚꽃 나무 (예산 -10 / 즉시 -20)
교통수단: 전기차 충전소 (예산 -60 / 즉시 -20)

역할과 조언 방식:
1. 현재 설치된 건물들을 분석하여 도시 발전 상태 평가
2. 부족한 부분을 파악하고 우선순위별 개선 방안 제시
3. 예산 대비 효율적인 건물 건설 순서 추천
4. CO2와 수익의 균형을 고려한 전략적 조언
5. 설치된 건물의 시너지 효과를 고려한 다음 건물 추천

답변 규칙:
- 답변에 이모지는 절대 사용하지 마세요 대신 느낌표, 물음표 등은 자유롭게 사용 가능
- 구체적인 건물명과 수치를 포함하여 실용적인 조언 제공
- 160자 내로 간결하되 핵심적인 정보 포함

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
        int currentYear = YearQuestManager.Instance?.GetCurrentYear() ?? 2025;

        // 이미 해당 연도의 채팅 퀘스트를 완료했다면 리턴
        if (yearChatCompleted.ContainsKey(currentYear) && yearChatCompleted[currentYear])
        {
            Debug.Log($"[GPTChatManager] {currentYear}년 채팅 퀘스트 이미 완료됨");
            return;
        }

        bool questCompleted = false;

        switch (currentYear)
        {
            case 2025:
                // 2025년: 첫 대화 성공 시 완료
                questCompleted = true;
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
                        questCompleted = true;
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
                        questCompleted = true;
                        Debug.Log($"[GPTChatManager] 2035년 - 도시 발전 계획 논의 완료! (키워드: {keyword})");
                        break;
                    }
                }
                break;

            case 2040:
                // 2040년: 봇의 조언과 관련된 건물 설치 체크는 YearQuestManager에서 처리
                // 여기서는 조언이 포함된 대화인지만 확인
                string[] adviceKeywords = { "공원", "친환경", "상업", "건설", "설치", "추천", "제안" };
                string botText = botMessage.ToLower();

                foreach (string keyword in adviceKeywords)
                {
                    if (botText.Contains(keyword.ToLower()))
                    {
                        questCompleted = true;
                        Debug.Log($"[GPTChatManager] 2040년 - 조언 대화 완료! (키워드: {keyword})");
                        break;
                    }
                }
                break;

            case 2045:
                // 2045년: 예산 관리 관련 키워드 포함 시 완료
                string[] budgetKeywords = { "예산", "돈", "수익", "재정", "비용", "경제", "수입", "자금" };
                string combinedText2045 = (userMessage + " " + botMessage).ToLower();

                foreach (string keyword in budgetKeywords)
                {
                    if (combinedText2045.Contains(keyword.ToLower()))
                    {
                        questCompleted = true;
                        Debug.Log($"[GPTChatManager] 2045년 - 예산 관리 상담 완료! (키워드: {keyword})");
                        break;
                    }
                }
                break;
        }

        // 퀘스트 완료 처리
        if (questCompleted)
        {
            yearChatCompleted[currentYear] = true;
            YearQuestManager.Instance?.OnChatCompleted();
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