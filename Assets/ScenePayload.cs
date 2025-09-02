using UnityEngine;

[CreateAssetMenu(menuName = "Game/ScenePayload")]
public class ScenePayload : ScriptableObject
{
    public float co2Tons;
    public string citizenSatisfactionLabel;
    public float citizenSatisfaction;
    public int budget;

    // 🔽 여기에 추가
    public string[] topTags; // 도시의 주요 건물 태그 (예: Factory, EcoPlant 등)

    public string aiCityName;
}
