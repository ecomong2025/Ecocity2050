using UnityEngine;

public class BuildingData : MonoBehaviour
{
    [Header("Placement Overrides (optional)")]
    [Tooltip("프리뷰/설치 시 추가 스케일 보정 (기본 1)")]
    public float scaleOverride = 1f;

    [Tooltip("프리뷰/설치 시 위치 보정 (로컬 좌표 기준, 단위: meter)")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("타일 차지 크기")]
    public int tileWidth = 1;
    public int tileHeight = 1;

    [Header("설치 관련 데이터")]
    public int cost;

    // 수익 관련
    public int maxIncomeAmount = 0;
    public int incomePer5Minutes = 0;

    // 환경 관련
    public int instantCO2Change = 0;
    public int co2PerSecond = 0;
    public int maxCO2Change = 0;

    // === 카메라 관련 ===
    public enum PreferredView
    {
        Front,
        Back,
        Left,
        Right,
        Top
    }

    [Header("카메라 프리뷰 옵션")]
    [Tooltip("더블클릭 시 카메라가 어느 방향에서 건물을 바라볼지 설정")]
    public PreferredView preferredView = PreferredView.Front;

    [Tooltip("특정 건물 전용 카메라 거리 배율 (0 이하 = 기본값 사용)")]
    public float cameraDistanceFactorOverride = 0f;

    [Tooltip("특정 건물 전용 카메라 FOV (0 이하 = 기본값 사용)")]
    public float cameraFOVOverride = 0f;
}
