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

    [Header("Animation")]
    public float animDuration = 0.25f;
    private Coroutine animCoroutine;
    private Vector3 panelOriginalScale = Vector3.one;

    void Start()
    {
        gptManager = GetComponent<GPTChatManager>();
        chatPanel.SetActive(false);
        panelOriginalScale = chatPanel.transform.localScale; // 원래 크기 저장
        chatButton.onClick.AddListener(OpenChat);
        closeButton.onClick.AddListener(CloseChat);
        sendButton.onClick.AddListener(SendMessage);
        inputField.onSubmit.AddListener(delegate { SendMessage(); });
    }

    public void OpenChat()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        SFXPlayer.Instance.PlayClick();
        chatPanel.SetActive(true);
        chatPanel.transform.localScale = Vector3.zero;
        animCoroutine = StartCoroutine(ScalePanel(chatPanel.transform, panelOriginalScale, animDuration));
        inputField.Select();
        inputField.ActivateInputField();
    }

    public void CloseChat()
    {
        SFXPlayer.Instance.PlayClick();
        chatPanel.SetActive(false);
    }

    IEnumerator CloseWithAnim()
    {
        yield return StartCoroutine(ScalePanel(chatPanel.transform, Vector3.zero, animDuration));
        chatPanel.SetActive(false);
        chatPanel.transform.localScale = panelOriginalScale; // 다시 원래 크기로 복구
    }

    IEnumerator ScalePanel(Transform panel, Vector3 target, float duration)
    {
        Vector3 start = panel.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            panel.localScale = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
        panel.localScale = target;
    }

    public void SendMessage()
    {
        SFXPlayer.Instance.PlayClick(); 
        string message = inputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;
        AddMessage(message, true);
        inputField.text = "";
        inputField.Select();
        inputField.ActivateInputField();
        if (gptManager != null)
        {
            gptManager.SendMessageToGPT(message, OnGPTResponse);
        }
    }

    public void AddMessage(string message, bool isUser)
    {
        GameObject prefab = isUser ? userMessagePrefab : botMessagePrefab;
        GameObject messageObj = Instantiate(prefab, contentParent);
        TMP_Text messageText = messageObj.GetComponentInChildren<TMP_Text>();
        if (messageText != null)
        {
            messageText.text = message;
        }
        messages.Add(messageObj);
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