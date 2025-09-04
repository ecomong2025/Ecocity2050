using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

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

    [Header("서버 엔드포인트")]
    [SerializeField] private string endpoint = "http://localhost:8000/name-city";

    [Header("UI")]
    public TMP_Text cityNameText;

    void Start()
    {
        StartCoroutine(FetchCoroutine());
    }

    IEnumerator FetchCoroutine()
    {
        cityNameText.text = "도시 이름 생성 중…";

        var req = new NameCityReq
        {
            co2Tons = payload.co2Tons,
            citizenSatisfaction = payload.citizenSatisfactionLabel,
            budget = payload.budget,
            topTags = payload.topTags
        };

        string json = JsonUtility.ToJson(req);

        var uwr = new UnityWebRequest(endpoint, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
        uwr.downloadHandler = new DownloadHandlerBuffer();
        uwr.SetRequestHeader("Content-Type", "application/json");

        yield return uwr.SendWebRequest();

        if (uwr.result == UnityWebRequest.Result.Success)
        {
            var res = JsonUtility.FromJson<NameCityRes>(uwr.downloadHandler.text);
            payload.aiCityName = res.cityName;
            cityNameText.text = $"AI 도시 이름: {payload.aiCityName}";
        }
        else
        {
            cityNameText.text = $"에러: {uwr.error}";
        }
    }
}
