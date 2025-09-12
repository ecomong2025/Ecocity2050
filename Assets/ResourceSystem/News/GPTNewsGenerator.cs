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
    }

    void OnEnable()
    {
        GameManager.OnSatisfactionChanged += OnSatisfactionChanged;
    }

    void OnDisable()
    {
        GameManager.OnSatisfactionChanged -= OnSatisfactionChanged;
    }

    private void OnSatisfactionChanged(string newLevel)
    {
        if (newLevel == "나쁨" || newLevel == "매우 나쁨")
        {
            if (lastSatisfaction != newLevel)
            {
                lastSatisfaction = newLevel;
                StartCoroutine(RequestNews(newLevel));
            }
        }
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

    public void ShowDisasterNews(string disasterType)
    {
        StartCoroutine(RequestDisasterNews(disasterType));
    }
    
    IEnumerator RequestDisasterNews(string disasterType)
    {
        // 응답을 엄격히 제한하여 항상 '완전한 문장' 두 줄(제목/본문)로 나오게 함
        string prompt =
            "다음 규칙을 반드시 지켜 출력하세요.\n" +
            "- 출력은 '정확히 두 줄'입니다. 첫 줄은 뉴스 헤드라인(최대 18자), 둘째 줄은 본문(최대 30자)입니다.\n" +
            "- 제목과 본문 모두 한국어의 자연스러운 완전 문장으로 작성하세요.\n\n" +
            $"요청 정보: 재난종류='{disasterType}'" +
            "예시(참고용, 실제 출력은 정확히 두 줄):\n" +
            $"건물에서 {disasterType} 발생!.\n" +
            $"탄소배출량 증가로 인한 {disasterType} 발생으로 건물이 붕괴되었습니다.";

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

                // 뉴스 생성 성공 시 팝업 표시
                StartCoroutine(AnimateNewsPanel());

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
        string prompt =
            "다음 규칙을 반드시 지켜 출력하세요.\n" +
            "- 출력은 '정확히 두 줄'입니다. 첫 줄은 뉴스 헤드라인(최대 18자), 둘째 줄은 본문(최대 30자)입니다.\n" +
            "- 제목과 본문 모두 한국어의 자연스러운 완전 문장으로 작성하세요.\n\n" +
            $"요청 정보: 시민 만족도가 '{satisfaction}'로 하락했습니다. 탄소 배출이 원인입니다.\n" +
            "예시(참고용, 실제 출력은 정확히 두 줄):\n" +
            "탄소 배출 급증 시민 불만 증가!\n" +
            "탄소배출량 증가로 시민 만족도가 '{satisfaction}'로 하락했습니다.";

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

                // 뉴스 생성 성공 시 팝업 표시
                StartCoroutine(AnimateNewsPanel());
                yield return StartCoroutine(AnimateNewsPanel());
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
        Vector2 visiblePos = new Vector2(rect.anchoredPosition.x, -115); // 화면 내 위치

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
    }
}
