using System.Linq;
using UnityEngine;

public class CityTagSummarizer : MonoBehaviour
{
    public ScenePayload payload;

    public void BuildSummaryBeforeEnding()
    {
        var all = FindObjectsOfType<BuildingData>(); // 각 건물에 tag가 있음

        payload.topTags = all
            .GroupBy(b => string.IsNullOrEmpty(b.name) ? (string.IsNullOrEmpty(GetTag(b)) ? "Unknown" : GetTag(b)) : GetTag(b))
            .Select(g => new { tag = g.Key, cnt = g.Count() })
            .OrderByDescending(x => x.cnt)
            .Take(3)
            .Select(x => x.tag)
            .ToArray();

        // 이미 너가 B안으로 저장하던 값도 같이 채워두면 좋아요(있다면)
        // payload.co2Tons, payload.citizenSatisfactionLabel, payload.budget ...
        // 이후 EndingScene으로 전환
    }

    string GetTag(BuildingData b) => b.tag; // 너희 필드명에 맞게 수정 (예: b.Type)
}
