using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class EndingSceneController : MonoBehaviour
{
    [Header("입력 Payload")]
    public ScenePayload payload;

    [Header("UI 참조 (여러 개 가능)")]
    public List<TMP_Text> co2Texts = new List<TMP_Text>();                   // CO₂ 원본(ton 또는 % of max 등) 표기
    public List<TMP_Text> co2ImprovedTexts = new List<TMP_Text>();           // CO₂ 개선 퍼센트(낮을수록 좋음)
    public List<TMP_Text> satisfactionTexts = new List<TMP_Text>();          // 시민 만족도 라벨
    public List<TMP_Text> satisfactionImprovedTexts = new List<TMP_Text>();  // 시민 만족도 개선 퍼센트
    public List<TMP_Text> cityNameTexts = new List<TMP_Text>();              // 도시 이름

    // 최대 CO2(ton)
    const float CO2_MAX = 5000f;

    // 만족도 라벨 → 점수(0~1) 매핑
    static readonly Dictionary<string, float> SAT_SCORE = new Dictionary<string, float>
    {
        { "매우나쁨", 0.00f },
        { "나쁨",     0.25f },
        { "보통",     0.50f },
        { "좋음",     0.75f },
        { "매우좋음", 1.00f },
    };

    void Start()
    {
        if (payload == null)
        {
            Debug.LogError("[EndingSceneController] payload가 연결되지 않았습니다!");
            return;
        }

        // ─────────────────────────────────────────────────────────
        // CO2 계산
        // ─────────────────────────────────────────────────────────
        float co2Tons = Mathf.Max(0f, payload.co2Tons);
        float co2Clamped = Mathf.Min(co2Tons, CO2_MAX);
        float co2PercentOfMax = Mathf.Clamp01(co2Clamped / CO2_MAX) * 100f;     // 0%~100% (높을수록 나쁨)
        float co2ImprovedPercent = (1f - (co2Clamped / CO2_MAX)) * 100f;        // 0%~100% (높을수록 좋음)

        // 표기 포맷
        string co2ValueMsg = $"{co2Tons:0.0} t (max {CO2_MAX:0}) • {co2PercentOfMax:0.0}% of max";
        string co2ImprovedMsg = $"{co2ImprovedPercent:0.0}% 개선";

        // 바인딩
        foreach (var t in co2Texts) if (t != null) t.text = co2ValueMsg;
        foreach (var t in co2ImprovedTexts) if (t != null) t.text = co2ImprovedMsg;

        // ─────────────────────────────────────────────────────────
        // 시민 만족도 계산
        // ─────────────────────────────────────────────────────────
        string label = string.IsNullOrEmpty(payload.citizenSatisfactionLabel)
            ? "보통" : payload.citizenSatisfactionLabel;

        // 라벨이 매핑 테이블에 없으면 가장 가까운 기본값으로 처리
        float score01;
        if (!SAT_SCORE.TryGetValue(label, out score01))
        {
            // 알 수 없는 라벨인 경우 중간값(보통)으로 가정
            score01 = 0.5f;
            Debug.LogWarning($"[EndingSceneController] 알 수 없는 만족도 라벨 '{label}' → 0.5로 처리");
        }

        float satisfactionImprovedPercent = Mathf.Clamp01(score01) * 100f;      // 0%~100% (높을수록 좋음)

        string satisfactionMsg = $"{label}";
        string satisfactionImprovedMsg = $"{satisfactionImprovedPercent:0.0}% 개선";

        foreach (var t in satisfactionTexts) if (t != null) t.text = satisfactionMsg;
        foreach (var t in satisfactionImprovedTexts) if (t != null) t.text = satisfactionImprovedMsg;

        // ─────────────────────────────────────────────────────────
        // 도시 이름
        // ─────────────────────────────────────────────────────────
        string cityNameMessage = string.IsNullOrEmpty(payload.aiCityName)
            ? "도시 이름 생성 중..."
            : $"{payload.aiCityName}";

        foreach (var t in cityNameTexts) if (t != null) t.text = cityNameMessage;
    }
}
