using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Text;
using System;
using System.IO;

public class GPTNewsGenerator : MonoBehaviour
{
    [Header("뉴스 UI")]
    public TMP_Text titleText;
    public TMP_Text contentText;
    public GameObject newsPanel;

    private string apiKey;
    private string lastSatisfaction = "";

    public static GPTNewsGenerator Instance; // 싱글톤 추가

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        LoadAPIKey();
        StartCoroutine(CheckSatisfactionRoutine());
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
                Debug.Log("[뉴스] API 키가 성공적으로 로드되었습니다.");
            }
            catch (Exception e)
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

    IEnumerator CheckSatisfactionRoutine()
    {
        while (true)
        {
            string currentSatisfaction = GameManager.Instance.GetSatisfactionLevel();
            if ((currentSatisfaction == "나쁨" || currentSatisfaction == "매우 나쁨") && lastSatisfaction != currentSatisfaction)
            {
                lastSatisfaction = currentSatisfaction;
                yield return StartCoroutine(RequestNews(currentSatisfaction));
                yield return StartCoroutine(AnimateNewsPanel()); // 애니메이션으로 뉴스 패널 표시
            }
            else if (currentSatisfaction != "나쁨" && currentSatisfaction != "매우 나쁨")
            {
                lastSatisfaction = currentSatisfaction;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public void ShowDisasterNews(string disasterType, string buildingName)
    {
        StartCoroutine(RequestDisasterNews(disasterType, buildingName));
    }

    IEnumerator RequestDisasterNews(string disasterType, string buildingName)
    {
        string prompt = $"재난 종류는 '{disasterType}'이고, 붕괴된 건물 이름은 '{buildingName}'입니다. 이 상황을 알리는 뉴스 제목(18자 이내)과 내용(30자 이내)을 각각 줄바꿈으로 구분해서 출력해줘.";
        string apiUrl = "https://api.openai.com/v1/chat/completions";

        string jsonBody = @"{
            ""model"": ""gpt-3.5-turbo"",
            ""messages"": [
                { ""role"": ""user"", ""content"": """ + EscapeJson(prompt) + @""" }
            ],
            ""max_tokens"": 100
        }";

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string result = request.downloadHandler.text;
                OpenAIResponse parsed = JsonUtility.FromJson<OpenAIResponse>(FixJson(result));
                string newsText = parsed.choices[0].message.content.Trim();
                string[] lines = newsText.Split('\n');
                string title = lines.Length > 0 ? lines[0].Trim() : "";
                string body = lines.Length > 1 ? lines[1].Trim() : "";

                if (title.Length > 18) title = title.Substring(0, 18);
                if (body.Length > 30) body = body.Substring(0, 30);

                if (titleText != null) titleText.text = title;
                if (contentText != null) contentText.text = body;

                yield return StartCoroutine(AnimateNewsPanel()); // 애니메이션으로 뉴스 패널 표시
            }
            else
            {
                Debug.LogError($"뉴스 생성 실패: {request.responseCode}\n{request.downloadHandler.text}");
            }
        }
    }

    IEnumerator RequestNews(string satisfaction)
    {
        string prompt = $"탄소 배출량 증가로 시민 만족도가 '{satisfaction}'로 하락했습니다. 이 상황에 맞는 뉴스 제목(18자 이내)과 내용(30자 이내)을 생성해줘. 제목과 내용을 각각 줄바꿈으로 구분해서 출력해줘.";
        string apiUrl = "https://api.openai.com/v1/chat/completions";

        string jsonBody = @"{
            ""model"": ""gpt-3.5-turbo"",
            ""messages"": [
                { ""role"": ""user"", ""content"": """ + EscapeJson(prompt) + @""" }
            ],
            ""max_tokens"": 100
        }";

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string result = request.downloadHandler.text;
                OpenAIResponse parsed = JsonUtility.FromJson<OpenAIResponse>(FixJson(result));
                string newsText = parsed.choices[0].message.content.Trim();
                string[] lines = newsText.Split('\n');
                string title = lines.Length > 0 ? lines[0].Trim() : "";
                string body = lines.Length > 1 ? lines[1].Trim() : "";

                if (title.Length > 18) title = title.Substring(0, 18);
                if (body.Length > 30) body = body.Substring(0, 30);

                if (titleText != null) titleText.text = title;
                if (contentText != null) contentText.text = body;
            }
            else
            {
                Debug.LogError($"뉴스 생성 실패: {request.responseCode}\n{request.downloadHandler.text}");
            }
        }
    }

    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
    }

    [Serializable]
    public class OpenAIResponse
    {
        public Choice[] choices;

        [Serializable]
        public class Choice
        {
            public Message message;
        }

        [Serializable]
        public class Message
        {
            public string role;
            public string content;
        }
    }

    private string FixJson(string json)
    {
        int idx = json.IndexOf("\"choices\":");
        if (idx < 0) return json;
        string fixedJson = "{\"choices\":" + json.Substring(idx + 10);
        return fixedJson;
    }

    // 뉴스 패널 애니메이션 코루틴 추가
    IEnumerator AnimateNewsPanel(float showTime = 5f, float animTime = 0.5f)
    {
        RectTransform rect = newsPanel.GetComponent<RectTransform>();
        Vector2 hiddenPos = new Vector2(rect.anchoredPosition.x, 120);   // 화면 위쪽(숫자 조정)
        Vector2 visiblePos = new Vector2(rect.anchoredPosition.x, -120); // 화면 내 위치

        // 시작 위치: 숨김
        rect.anchoredPosition = hiddenPos;
        newsPanel.SetActive(true);

        // 내려오는 애니메이션
        float t = 0;
        while (t < animTime)
        {
            rect.anchoredPosition = Vector2.Lerp(hiddenPos, visiblePos, t / animTime);
            t += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = visiblePos;

        // 일정 시간 대기
        yield return new WaitForSeconds(showTime);

        // 올라가는 애니메이션
        t = 0;
        while (t < animTime)
        {
            rect.anchoredPosition = Vector2.Lerp(visiblePos, hiddenPos, t / animTime);
            t += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = hiddenPos;
        newsPanel.SetActive(false);
    }
}
