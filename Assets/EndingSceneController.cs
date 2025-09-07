using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class EndingSceneController : MonoBehaviour
{
    public ScenePayload payload;

    [Header("UI 참조 (여러 개 가능)")]
    public List<TMP_Text> co2Texts = new List<TMP_Text>();          // CO₂ 출력용 여러 개
    public List<TMP_Text> satisfactionTexts = new List<TMP_Text>(); // 시민 만족도 출력용 여러 개
    public List<TMP_Text> cityNameTexts = new List<TMP_Text>();     // 도시 이름 출력용 여러 개

    void Start()
    {
        // === 안전 체크 ===
        if (payload == null)
        {
            Debug.LogError("[EndingSceneController] payload가 연결되지 않았습니다!");
            return;
        }

        // CO₂ 값 출력
        string co2Message = $"최종 CO₂: {payload.co2Tons:0.0}";
        foreach (var t in co2Texts)
            if (t != null) t.text = co2Message;

        // 시민 만족도 출력
        string satisfactionMessage = $"시민 만족도: {payload.citizenSatisfactionLabel}";
        foreach (var t in satisfactionTexts)
            if (t != null) t.text = satisfactionMessage;

        // 도시 이름 출력
        string cityNameMessage = string.IsNullOrEmpty(payload.aiCityName)
            ? "도시 이름 생성 중..."
            : $"AI 도시 이름: {payload.aiCityName}";
        foreach (var t in cityNameTexts)
            if (t != null) t.text = cityNameMessage;
    }
}
