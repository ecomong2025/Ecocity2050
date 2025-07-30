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

    private string apiKey = " "; //추후 수정

    void Start()
    {
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

        // JSON 문자열 수동 생성
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

    //  JSON 문자열 escape 함수
    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
    }

    //  GPT 응답을 위한 JSON 파싱 클래스
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

    //  Unity의 JsonUtility가 배열을 잘 못 읽는 문제 대응
    private string FixJson(string json)
    {
        int idx = json.IndexOf("\"choices\":");
        if (idx < 0) return json;

        string fixedJson = "{\"choices\":" + json.Substring(idx + 10);
        return fixedJson;
    }
}
