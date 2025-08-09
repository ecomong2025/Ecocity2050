using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
public class ChatUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Button chatButton;
    public GameObject chatPanel;
    public Button closeButton;
    public ScrollRect scrollRect;
    public Transform contentParent;
    public TMP_InputField inputField;
    public Button sendButton;
    [Header("Message Prefabs")]
    public GameObject userMessagePrefab;
    public GameObject botMessagePrefab;
    [Header("Settings")]
    public float messageSpacing = 10f;
    private List<GameObject> messages = new List<GameObject>();
    private GPTChatManager gptManager;
    void Start()
    {
        // GPT 매니저 참조
        gptManager = GetComponent<GPTChatManager>();
        // 초기 설정
        chatPanel.SetActive(false);
        // 이벤트 연결
        chatButton.onClick.AddListener(OpenChat);
        closeButton.onClick.AddListener(CloseChat);
        sendButton.onClick.AddListener(SendMessage);
        // 엔터키로 전송
        inputField.onSubmit.AddListener(delegate { SendMessage(); });
    }
    public void OpenChat()
    {
        chatPanel.SetActive(true);
        inputField.Select();
        inputField.ActivateInputField();
    }
    public void CloseChat()
    {
        chatPanel.SetActive(false);
    }
    public void SendMessage()
    {
        string message = inputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;
        // 사용자 메시지 추가
        AddMessage(message, true);
        // 입력창 클리어
        inputField.text = "";
        inputField.Select();
        inputField.ActivateInputField();
        // GPT API 호출
        if (gptManager != null)
        {
            gptManager.SendMessageToGPT(message, OnGPTResponse);
        }
    }
    public void AddMessage(string message, bool isUser)
    {
        GameObject prefab = isUser ? userMessagePrefab : botMessagePrefab;
        GameObject messageObj = Instantiate(prefab, contentParent);
        // 메시지 텍스트 설정
        TMP_Text messageText = messageObj.GetComponentInChildren<TMP_Text>();
        if (messageText != null)
        {
            messageText.text = message;
        }
        messages.Add(messageObj);
        // 스크롤을 맨 아래로
        StartCoroutine(ScrollToBottom());
    }
    private void OnGPTResponse(string response)
    {
        AddMessage(response, false);
    }
    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = 0f;
    }
    public void ClearChat()
    {
        foreach (GameObject message in messages)
        {
            Destroy(message);
        }
        messages.Clear();
    }
}