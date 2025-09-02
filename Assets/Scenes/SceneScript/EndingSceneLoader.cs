using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text.RegularExpressions;

public class EndingSceneLoader : MonoBehaviour
{
    [Header("Ending Scene Name")]
    public string endingSceneName = "EndingScene";

    [Header("UI 참조")]
    public TMP_Text co2ValueText;          // Co2ValueText
    public TMP_Text satisfactionText;      // SatisfactionText

    [Header("씬 간 데이터 공유")]
    public ScenePayload payload;           // Project 창에 만든 ScenePayload.asset 넣기

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha7)) // 숫자 7 키
        {
            LoadEndingScene();
        }
    }

    public void LoadEndingScene()
    {
        // 마지막 값 스냅샷 저장
        payload.co2Tons = ParseFloat(co2ValueText.text);

        // 라벨 그대로 저장
        payload.citizenSatisfactionLabel = satisfactionText.text.Trim();

        // 필요하다면 라벨 → 점수 변환
        payload.citizenSatisfaction = LabelToScore(payload.citizenSatisfactionLabel);

        payload.aiCityName = ""; // 엔딩씬에서 GPT API로 채우도록 남겨둠

        // 엔딩 씬 로드
        SceneManager.LoadScene(endingSceneName);
    }

    float ParseFloat(string s)
    {
        var cleaned = Regex.Replace(s, @"[^0-9\.\-]", "");
        float.TryParse(cleaned, out var v);
        return v;
    }

    float LabelToScore(string label)
    {
        // 간단 매핑 (원하는 대로 조정 가능)
        if (label.Contains("매우 좋음")) return 100f;
        if (label.Contains("좋음")) return 75f;
        if (label.Contains("보통")) return 50f;
        if (label.Contains("나쁨")) return 25f;
        if (label.Contains("매우 나쁨")) return 0f;
        return 50f; // 기본값
    }
}
