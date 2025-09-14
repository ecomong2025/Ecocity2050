using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

[System.Serializable]
public class NameCityReq
{
    public float co2Tons;
    public string citizenSatisfaction;
    public int budget;
    public string[] topTags;
}

[System.Serializable]
public class NameCityRes
{
    public string cityName;
}

public class CityNameFetcher : MonoBehaviour
{
    [Header("데이터 소스")]
    public ScenePayload payload;   // GameScene에서 채워둔 ScenePayload.asset

    [Header("서버 엔드포인트 (Django는 슬래시 필수)")]
    // 로컬 테스트면 "http://127.0.0.1:8000/name-city/" 로 바꿔서 테스트하세요.
    [SerializeField] private string endpoint = "http://127.0.0.1:8000/name-city/";

    [Header("UI")]
    public TMP_Text cityNameText;

    void Start()
    {
        StartCoroutine(FetchCoroutine());
    }

    IEnumerator FetchCoroutine()
    {
        cityNameText.text = "도시 이름 생성 중…";

        var reqObj = new NameCityReq
        {
            co2Tons = payload.co2Tons,
            citizenSatisfaction = payload.citizenSatisfactionLabel,
            budget = payload.budget,
            topTags = payload.topTags
        };

        string json = JsonUtility.ToJson(reqObj);

        using (var uwr = new UnityWebRequest(EnsureTrailingSlash(endpoint), "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.SetRequestHeader("Accept", "application/json");
            uwr.timeout = 30;

            yield return uwr.SendWebRequest();

            // 200~299만 성공으로 간주
            if (uwr.result == UnityWebRequest.Result.Success && uwr.responseCode >= 200 && uwr.responseCode < 300)
            {
                var text = uwr.downloadHandler.text;
                NameCityRes res = null;
                try { res = JsonUtility.FromJson<NameCityRes>(text); }
                catch { /* 파싱 실패 시 아래 처리 */ }

                if (res != null && !string.IsNullOrEmpty(res.cityName))
                {
                    payload.aiCityName = res.cityName;
                    cityNameText.text = $"AI 도시 이름: {payload.aiCityName}";
                }
                else
                {
                    cityNameText.text = $"파싱 실패\n응답: {text}";
                }
            }
            else
            {
                // 서버가 에러 본문을 줄 때 내용을 같이 보여주자
                var body = uwr.downloadHandler != null ? uwr.downloadHandler.text : "";
                cityNameText.text = $"에러 {uwr.responseCode} / {uwr.result}\n{uwr.error}\n{body}";
            }
        }
    }

    // Django는 보통 슬래시가 필요하므로 방지용
    private string EnsureTrailingSlash(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        return url.EndsWith("/") ? url : (url + "/");
    }
}
