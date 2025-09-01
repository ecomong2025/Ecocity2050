using UnityEngine;

[CreateAssetMenu(menuName = "Game/ScenePayload")]
public class ScenePayload : ScriptableObject
{
    public float co2Tons;

    // 시민 만족도 라벨 ("매우 좋음" 같은 문자열 저장용)
    public string citizenSatisfactionLabel;

    // 선택: 라벨을 숫자로 변환한 값 (0~100)
    public float citizenSatisfaction;

    // GPT API로 만든 도시 이름
    public string aiCityName;
}
