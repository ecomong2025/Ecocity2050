using UnityEngine;

public class BuildingData : MonoBehaviour
{
    [Header("타일 차지 크기")]
    public int tileWidth = 1;
    public int tileHeight = 1;

    [Header("설치 관련 데이터")]
    public int cost;

    // 즉시 변화량 (예: 친환경 건물, 나무 등)
    public int instantCO2Change = 0;

    // 시간당 변화량 (예: 공장 등): 10/5초 → 2/초
    public int co2PerSecond = 0;

    public int maxCO2Change = 0; // 최대 변화량 제한 (증가형 건물에 적용)
}