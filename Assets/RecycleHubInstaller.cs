using UnityEngine;

public class RecycleHubInstaller : MonoBehaviour
{
    [SerializeField] private GameObject recyclingHubPrefab; // 리사이클 허브 프리팹
    [SerializeField] private Transform parent;              // 설치될 부모 (맵/타일 루트 오브젝트)
    [SerializeField] private Vector3 spawnPosition;         // 설치 위치
    [SerializeField] private Vector3 spawnRotation;         // 회전 (Euler 각도)

    // 버튼의 OnClick 이벤트에 연결
    public void OnClick_Install()
    {
        if (!recyclingHubPrefab)
        {
            Debug.LogWarning("[RecycleHubInstaller] recyclingHubPrefab이 비었습니다.");
            return;
        }

        // 프리팹 생성 (회전 적용)
        var go = Instantiate(
            recyclingHubPrefab,
            spawnPosition,
            Quaternion.Euler(spawnRotation),
            parent
        );

        // 프리팹(혹은 인스턴스)에서 BuildingData 꺼내기
        var data = go.GetComponent<BuildingData>();
        if (data == null)
        {
            Debug.LogWarning("[RecycleHubInstaller] BuildingData 컴포넌트를 찾지 못했습니다.");
            return;
        }

        // YearQuestManager에 설치 알림 → 자동 퀘스트 판정(무배출 등)
        if (YearQuestManager.Instance != null)
        {
            YearQuestManager.Instance.OnBuildingInstalled(go, data);
        }
        else
        {
            Debug.LogWarning("[RecycleHubInstaller] YearQuestManager.Instance 가 없습니다.");
        }

        Debug.Log("[RecycleHubInstaller] 리사이클 허브 설치 완료!");
    }
}
