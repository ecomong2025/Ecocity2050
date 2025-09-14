using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

[System.Serializable]
public class SaveGameRequest
{
    public string userId;
    public float co2Tons;
    public string citizenSatisfaction;
    public int budget;
    public string[] topTags;
    public string aiCityName;
}

[System.Serializable]
public class LoadGameResponse
{
    public float co2Tons;
    public string citizenSatisfaction;
    public int budget;
    public string[] topTags;
    public string aiCityName;
    public string lastSaved;
}

[System.Serializable]
public class CheckSavedDataResponse
{
    public bool exists;
    public string error;
}

[System.Serializable]
public class SaveGameResponse
{
    public string message;
    public bool created;
    public string error;
}

public class SaveGameManager : MonoBehaviour
{
    [Header("서버 엔드포인트")]
    [SerializeField] private string baseUrl = "http://127.0.0.1:8000";

    public static SaveGameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 게임 데이터를 서버에 저장
    /// </summary>
    public void SaveGameData(ScenePayload payload, System.Action<bool, string> callback = null)
    {
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        string userId = kakaoManager?.GetCurrentUserId() ?? "";

        if (string.IsNullOrEmpty(userId))
        {
            callback?.Invoke(false, "로그인이 필요합니다.");
            return;
        }

        StartCoroutine(SaveGameDataCoroutine(userId, payload, callback));
    }

    IEnumerator SaveGameDataCoroutine(string userId, ScenePayload payload, System.Action<bool, string> callback)
    {
        var requestData = new SaveGameRequest
        {
            userId = userId,
            co2Tons = payload.co2Tons,
            citizenSatisfaction = payload.citizenSatisfactionLabel,
            budget = payload.budget,
            topTags = payload.topTags,
            aiCityName = payload.aiCityName
        };

        string json = JsonUtility.ToJson(requestData);
        string url = $"{baseUrl}/save-game/";

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 && request.responseCode < 300)
            {
                callback?.Invoke(true, "게임 데이터가 저장되었습니다.");
            }
            else
            {
                string errorMsg = $"저장 실패: {request.responseCode} - {request.error}";
                callback?.Invoke(false, errorMsg);
            }
        }
    }

    /// <summary>
    /// 저장된 게임 데이터를 불러오기
    /// </summary>
    public void LoadGameData(System.Action<bool, ScenePayload, string> callback)
    {
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        string userId = kakaoManager?.GetCurrentUserId() ?? "";

        if (string.IsNullOrEmpty(userId))
        {
            callback?.Invoke(false, null, "로그인이 필요합니다.");
            return;
        }

        StartCoroutine(LoadGameDataCoroutine(userId, callback));
    }

    IEnumerator LoadGameDataCoroutine(string userId, System.Action<bool, ScenePayload, string> callback)
    {
        string url = $"{baseUrl}/load-game/?userId={UnityWebRequest.EscapeURL(userId)}";

        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 && request.responseCode < 300)
            {
                try
                {
                    var response = JsonUtility.FromJson<LoadGameResponse>(request.downloadHandler.text);

                    // ScenePayload 생성
                    var payload = ScriptableObject.CreateInstance<ScenePayload>();
                    payload.co2Tons = response.co2Tons;
                    payload.citizenSatisfactionLabel = response.citizenSatisfaction;
                    payload.budget = response.budget;
                    payload.topTags = response.topTags;
                    payload.aiCityName = response.aiCityName;

                    callback?.Invoke(true, payload, "데이터를 성공적으로 불러왔습니다.");
                }
                catch (System.Exception e)
                {
                    callback?.Invoke(false, null, $"데이터 파싱 실패: {e.Message}");
                }
            }
            else
            {
                string errorMsg = $"불러오기 실패: {request.responseCode} - {request.error}";
                callback?.Invoke(false, null, errorMsg);
            }
        }
    }

    /// <summary>
    /// 저장된 데이터가 있는지 확인
    /// </summary>
    public void CheckSavedDataExists(System.Action<bool, bool> callback)
    {
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        string userId = kakaoManager?.GetCurrentUserId() ?? "";

        if (string.IsNullOrEmpty(userId))
        {
            callback?.Invoke(false, false);
            return;
        }

        StartCoroutine(CheckSavedDataCoroutine(userId, callback));
    }

    IEnumerator CheckSavedDataCoroutine(string userId, System.Action<bool, bool> callback)
    {
        string url = $"{baseUrl}/check-saved-data/?userId={UnityWebRequest.EscapeURL(userId)}";

        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 && request.responseCode < 300)
            {
                try
                {
                    var response = JsonUtility.FromJson<CheckSavedDataResponse>(request.downloadHandler.text);
                    callback?.Invoke(true, response.exists);
                }
                catch
                {
                    callback?.Invoke(false, false);
                }
            }
            else
            {
                callback?.Invoke(false, false);
            }
        }
    }

    /// <summary>
    /// 헬퍼 메서드: 현재 사용자 ID 반환
    /// </summary>
    public string GetCurrentUserId()
    {
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        return kakaoManager?.GetCurrentUserId() ?? "";
    }

    /// <summary>
    /// 헬퍼 메서드: 로그인 상태 확인
    /// </summary>
    public bool IsUserLoggedIn()
    {
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        return kakaoManager != null && kakaoManager.IsLoggedIn();
    }
}