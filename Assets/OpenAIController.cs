using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using System;

public class OpenAIController : MonoBehaviour
{
    public TMP_Text textField;
    public TMP_InputField inputField;
    public Button okBtn;

    private string apiKey;

    void Start()
    {
        apiKey = LoadAPIKeyFromResources();  // 🔑 키 로드
        okBtn.onClick.AddListener(OnSubmit);
    }

    void OnSubmit()
    {
        string userInput = inputField.text;
        if (!string.IsNullOrWhiteSpace(userInput))
        {
            StartCoroutine(SendToOpenAI(userInput));
        }
    }

    IEnumerator SendToOpenAI(string userInput)
    {
        string apiUrl = "https://api.openai.com/v1/chat/completions";

        string jsonBody = @"{
            ""model"": ""gpt-3.5-turbo"",
            ""messages"": [
                { ""role"": ""user"", ""content"": """ + EscapeJson(userInput) + @""" }
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
                textField.text = parsed.choices[0].message.content.Trim();
            }
            else
            {
                textField.text = $"Error: {request.responseCode}\n{request.downloadHandler.text}";
            }
        }
    }

    private string LoadAPIKeyFromResources()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("api_key");
        if (jsonFile != null)
        {
            APIKeyWrapper wrapper = JsonUtility.FromJson<APIKeyWrapper>(jsonFile.text);
            return wrapper.apiKey;
        }

        Debug.LogWarning("Resources 폴더 내 api_key.json 파일을 찾을 수 없습니다.");
        return "";
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

    [Serializable]
    public class APIKeyWrapper
    {
        public string apiKey;
    }

    private string FixJson(string json)
    {
        int idx = json.IndexOf("\"choices\":");
        if (idx < 0) return json;
        string fixedJson = "{\"choices\":" + json.Substring(idx + 10);
        return fixedJson;
    }
}
