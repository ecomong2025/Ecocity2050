using UnityEngine;
using TMPro;

public class EndingSceneController : MonoBehaviour
{
    public ScenePayload payload;

    [Header("UI 참조")]
    public TMP_Text co2Text;           // CO₂ 출력용
    public TMP_Text satisfactionText;  // 시민 만족도 출력용
    public TMP_Text cityNameText;      // AI 도시 이름 출력용  👈 추가

    void Start()
    {
        co2Text.text = $"최종 CO₂: {payload.co2Tons:0.0}";
        satisfactionText.text = $"시민 만족도: {payload.citizenSatisfactionLabel}";

        // 도시 이름이 비어있으면 "생성 중..." 표시
        cityNameText.text = string.IsNullOrEmpty(payload.aiCityName)
            ? "도시 이름 생성 중..."
            : $"AI 도시 이름: {payload.aiCityName}";
    }
}
