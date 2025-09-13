using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // 씬 전환용


[Serializable]
public class KakaoUser
{
    public int id;
    public string username;
    public string email;
    public string first_name;
    public string last_name;
}

[Serializable]
public class KakaoToken
{
    public string access;
    public string refresh;
}

[Serializable]
public class KakaoLoginResponse
{
    public KakaoUser user;
    public string message;
    public KakaoToken token;
}

[Serializable]
public class UnityLoginStartResponse
{
    public string auth_url;
    public string state;
}

[Serializable]
public class UnitySessionResponse
{
    public string status; // "completed", "pending"
    public KakaoUser user;
    public string message;
}

public class KakaoLoginManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text userNameText;
    public Button loginButton;
    public Text statusText;

    [Header("Settings")]
    public string baseUrl = "http://127.0.0.1:8000";

    private string currentState;
    private Coroutine pollCoroutine;

    void Start()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(StartKakaoLogin);

        UpdateLoginUI(false);
    }

    public void StartKakaoLogin()
    {
        StartCoroutine(InitiateKakaoLogin());
    }

    IEnumerator InitiateKakaoLogin()
    {
        UpdateStatus("로그인 URL 생성 중...");

        string url = $"{baseUrl}/users/kakao/unity/login/";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log("Unity 로그인 시작 응답: " + jsonResponse);

                    UnityLoginStartResponse response = JsonUtility.FromJson<UnityLoginStartResponse>(jsonResponse);

                    if (response != null && !string.IsNullOrEmpty(response.auth_url))
                    {
                        currentState = response.state;
                        Application.OpenURL(response.auth_url);
                        UpdateStatus("브라우저에서 로그인을 완료해주세요...");

                        if (pollCoroutine != null)
                            StopCoroutine(pollCoroutine);

                        pollCoroutine = StartCoroutine(PollLoginStatus());
                    }
                    else
                    {
                        UpdateStatus("로그인 URL 생성 실패");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("로그인 시작 응답 파싱 에러: " + e.Message);
                    UpdateStatus("로그인 초기화 실패");
                }
            }
            else
            {
                Debug.LogError("로그인 시작 요청 실패: " + request.error);
                UpdateStatus($"서버 연결 실패: {request.error}");
            }
        }
    }

    IEnumerator PollLoginStatus()
    {
        float checkInterval = 2f;
        float maxWaitTime = 120f;
        float elapsedTime = 0f;

        while (elapsedTime < maxWaitTime && !string.IsNullOrEmpty(currentState))
        {
            yield return new WaitForSeconds(checkInterval);
            elapsedTime += checkInterval;

            yield return StartCoroutine(CheckSessionStatus());

            if (string.IsNullOrEmpty(currentState))
                yield break;
        }

        if (elapsedTime >= maxWaitTime)
        {
            UpdateStatus("로그인 시간 초과 (2분)");
            currentState = "";
        }
    }

    IEnumerator CheckSessionStatus()
    {
        if (string.IsNullOrEmpty(currentState))
            yield break;

        string url = $"{baseUrl}/users/kakao/unity/session/?state={currentState}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log("세션 상태 응답: " + jsonResponse);

                    UnitySessionResponse response = JsonUtility.FromJson<UnitySessionResponse>(jsonResponse);

                    if (response != null)
                    {
                        if (response.status == "completed" && response.user != null)
                        {
                            // UserDataManager에 저장
                            if (UserDataManager.Instance != null)
                                UserDataManager.Instance.SetUserData(response.user);

                            DisplayUserInfo(response.user);
                            UpdateStatus($"로그인 완료: {response.message}");
                            currentState = "";

                            if (pollCoroutine != null)
                            {
                                StopCoroutine(pollCoroutine);
                                pollCoroutine = null;
                            }
                        }
                        else if (response.status == "pending")
                        {
                            UpdateStatus("로그인 대기 중... (브라우저에서 로그인해주세요)");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("세션 상태 확인 에러: " + e.Message);
                }
            }
            else if (request.responseCode == 404)
            {
                UpdateStatus("세션 만료 또는 오류");
                currentState = "";

                if (pollCoroutine != null)
                {
                    StopCoroutine(pollCoroutine);
                    pollCoroutine = null;
                }
            }
            else
            {
                Debug.LogError("세션 확인 요청 실패: " + request.error);
            }
        }
    }

    void DisplayUserInfo(KakaoUser user)
    {
        if (userNameText != null)
        {
            string displayName = $"{user.first_name} {user.last_name}".Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = user.username;

            userNameText.text = displayName;
        }

        UpdateLoginUI(true);
    }

    void UpdateLoginUI(bool isLoggedIn)
    {
        if (loginButton != null)
        {
            Text buttonText = loginButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = isLoggedIn ? "로그아웃" : "카카오 로그인";

            loginButton.onClick.RemoveAllListeners();
            if (isLoggedIn)
                loginButton.onClick.AddListener(Logout);
            else
                loginButton.onClick.AddListener(StartKakaoLogin);

            if (!isLoggedIn && userNameText != null)
                userNameText.text = "로그인이 필요합니다";
        }
    }

    void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log("Status: " + message);
    }

    public void Logout()
    {
        if (pollCoroutine != null)
        {
            StopCoroutine(pollCoroutine);
            pollCoroutine = null;
        }

        currentState = "";

        if (UserDataManager.Instance != null)
            UserDataManager.Instance.ClearUserData();

        UpdateLoginUI(false);
        UpdateStatus("로그아웃 완료");
    }

    void OnDestroy()
    {
        if (pollCoroutine != null)
            StopCoroutine(pollCoroutine);
    }
}