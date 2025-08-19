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
    [SerializeField] private int maxTokens = 800; // 토큰 수를 늘려서 더 자세한 조언 가능

    // 채팅을 한 번 이상 했는지 확인하는 플래그
    private bool hasChatted = false;

    private System.Collections.Generic.List<GPTMessage> conversationHistory =
        new System.Collections.Generic.List<GPTMessage>();

    void Start()
    {
        LoadAPIKey();
        SetupSystemMessage();
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
        string gameState = GetCurrentGameState();
        string systemMessage = $@"
당신은 도시 건설 게임의 전문 조언자입니다. 플레이어에게 현재 게임 상황을 종합적으로 분석하여 구체적이고 실용적인 조언을 제공해주세요.

현재 게임 상황:
- 예산: {GameManager.Instance.budget}원
- CO2 배출량: {GameManager.Instance.co2}
- 시민 만족도: {GameManager.Instance.GetSatisfactionLevel()}

{GetBuildingStatus()}

역할과 조언 방식:
1. 현재 설치된 건물들을 분석하여 도시 발전 상태 평가
2. 부족한 부분을 파악하고 우선순위별 개선 방안 제시
3. 예산 대비 효율적인 건물 건설 순서 추천
4. CO2와 수익의 균형을 고려한 전략적 조언
5. 설치된 건물의 시너지 효과를 고려한 다음 건물 추천

상황별 조언 기준:
- 수익 건물 부족 시: 상업 건물이나 수익 시설 우선 추천
- 환경 문제 심각 시: 공원, 친환경 건물 우선 추천
- 건물 불균형 시: 균형 잡힌 도시 개발 방향 제시
- 예산 부족 시: 저비용 고효율 건물 추천

답변 규칙:
- 답변에 이모지는 절대 사용하지 마세요
- 느낌표, 물음표 등의 일반적인 문장 부호는 사용 가능합니다
- 구체적인 건물명과 수치를 포함하여 실용적인 조언 제공
- 250자 내외로 간결하되 핵심적인 정보 포함

답변 스타일: 전문적이면서도 친근한 도시계획 전문가처럼 조언해주세요.
";

        GPTMessage systemMsg = new GPTMessage
        {
            role = "system",
            content = systemMessage
        };
        conversationHistory.Add(systemMsg);
    }

    private string GetCurrentGameState()
    {
        if (GameManager.Instance == null) return "게임 데이터를 불러올 수 없습니다.";

        return $"예산: {GameManager.Instance.budget}원, CO2: {GameManager.Instance.co2}, 만족도: {GameManager.Instance.GetSatisfactionLevel()}";
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

        // 콘솔에 사용자 메시지 출력
        Debug.Log($"[사용자 메시지] {userMessage}");

        // 현재 게임 상태와 건물 정보를 포함한 메시지 생성
        string gameState = GetCurrentGameState();
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

        // 최근 8개 메시지만 유지 (시스템 메시지 포함, API 비용 절약)
        if (conversationHistory.Count > 8)
        {
            // 시스템 메시지는 유지하고 오래된 대화만 제거
            var systemMsg = conversationHistory[0];
            conversationHistory.RemoveRange(1, conversationHistory.Count - 8);
            conversationHistory.Insert(0, systemMsg);
        }

        StartCoroutine(CallGPTAPI(onResponse));
    }

    IEnumerator CallGPTAPI(System.Action<string> onResponse)
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
                Debug.Log($"[GPT 응답] {errorMessage}");
                onResponse?.Invoke(errorMessage);
            }
            else
            {
                try
                {
                    GPTResponse response = JsonUtility.FromJson<GPTResponse>(webRequest.downloadHandler.text);
                    string botMessage = response.choices[0].message.content.Trim();

                    // 콘솔에 GPT 응답 출력
                    Debug.Log($"[GPT 응답] {botMessage}");

                    // 응답을 대화 기록에 추가
                    GPTMessage botMsg = new GPTMessage
                    {
                        role = "assistant",
                        content = botMessage
                    };
                    conversationHistory.Add(botMsg);

                    // 첫 번째 성공적인 채팅 완료 시 퀘스트 완료
                    if (!hasChatted)
                    {
                        hasChatted = true;
                        if (YearQuestManager.Instance != null)
                        {
                            YearQuestManager.Instance.OnChatCompleted();
                        }
                    }

                    onResponse?.Invoke(botMessage);
                }
                catch (System.Exception e)
                {
                    string parseErrorMessage = "응답을 처리하는 중 오류가 발생했습니다.";
                    Debug.LogError($"JSON Parse Error: {e.Message}");
                    Debug.Log($"[GPT 응답] {parseErrorMessage}");
                    onResponse?.Invoke(parseErrorMessage);
                }
            }
        }
    }

    public void ClearConversation()
    {
        conversationHistory.Clear();
        SetupSystemMessage();
        // 대화 기록 초기화 시 채팅 플래그도 초기화 
        hasChatted = false;
        Debug.Log("[시스템] 대화 기록이 초기화되었습니다.");
    }
}