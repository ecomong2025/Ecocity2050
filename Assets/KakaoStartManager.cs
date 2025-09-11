using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class KakaoStartManager : MonoBehaviour
{
    public static KakaoStartManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TryLogin()
    {
        StartCoroutine(GetKakaoAuthUrl());
    }

    private IEnumerator GetKakaoAuthUrl()
    {
        string backendUrl = "http://127.0.0.1:8000/users/kakao/unity/login/";

        using (UnityWebRequest www = UnityWebRequest.Get(backendUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("카카오 로그인 URL 요청 실패: " + www.error);
            }
            else
            {
                try
                {
                    KakaoLoginResponse response = JsonUtility.FromJson<KakaoLoginResponse>(www.downloadHandler.text);
                    string kakaoAuthUrl = response.auth_url;

                    Application.OpenURL(kakaoAuthUrl);
                }
                catch (Exception e)
                {
                    Debug.LogError("카카오 로그인 URL 파싱 실패: " + e.Message);
                }
            }
        }
    }

    [Serializable]
    public class KakaoLoginResponse
    {
        public string auth_url;
    }
}
