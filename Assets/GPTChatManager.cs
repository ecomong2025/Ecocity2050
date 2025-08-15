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
    [SerializeField] private int maxTokens = 500;

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
당신은 도시 건설 게임의 조언자입니다. 플레이어에게 게임 내 상황에 맞는 구체적이고 실용적인 조언을 제공해주세요.

현재 게임 상황:
- 예산: {GameManager.Instance.budget}원
- CO2 배출량: {GameManager.Instance.co2}
- 시민 만족도: {GameManager.Instance.GetSatisfactionLevel()}

역할:
1. 플레이어의 질문에 대해 현재 게임 상황을 고려한 구체적인 조언 제공
2. CO2 배출량이 높을 때는 환경 개선 방안 제시 (공원, 친환경 건물 등)
3. 예산이 부족할 때는 수익 창출 방안 제시
4. 시민 만족도가 낮을 때는 개선 방안 제시
5. 간결하고 이해하기 쉬운 답변 제공 (200자 내외)

답변 규칙:
- 답변에 이모지는 절대 맍마사용하지 마세요
- 느낌표, 물음표 등의 일반적인 문장 부호는 사용 가능합니다
- 텍스트만으로 친근하고 도움이 되는 조언자처럼 말해주세요

답변 스타일: 친근하고 도움이 되는 조언자처럼 말해주세요.
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

        // 현재 게임 상태를 포함한 메시지 생성
        string contextMessage = $"[현재 상황] {GetCurrentGameState()}\n\n[질문] {userMessage}";

        GPTMessage userMsg = new GPTMessage
        {
            role = "user",
            content = contextMessage
        };

        conversationHistory.Add(userMsg);

        // 최근 10개 메시지만 유지 (API 비용 절약)
        if (conversationHistory.Count > 10)
        {
            conversationHistory.RemoveRange(1, conversationHistory.Count - 10); // 시스템 메시지는 유지
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

                    //  첫 번째 성공적인 채팅 완료 시 퀘스트 완료
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