using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private string panelNameToClose = "BuildingPanel";

    private GameObject buildingPrefab;

    void Start()
    {
        // 1. 버튼 이름에서 "Btn" 제거하고 "Prefab" 붙이기 (예: "ParkBtn" → "ParkPrefab")
        string baseName = gameObject.name.Replace("Btn", "");
        string prefabName = baseName + "Prefab";

        // 2. 프리팹 자동 로드 (Resources/Buildings/Prefabs/프리팹이름)
        string resourcePath = "Buildings/Prefabs/" + prefabName;
        buildingPrefab = Resources.Load<GameObject>(resourcePath);

        if (buildingPrefab == null)
        {
            Debug.LogError($"❌ 프리팹을 찾을 수 없습니다: Resources/{resourcePath}.prefab");
            return;
        }

        // 3. 버튼 클릭 이벤트 등록
        GetComponent<Button>().onClick.AddListener(OnButtonClick);
    }

    void OnButtonClick()
    {
        // ✅ BuildingData와 GameManager 가져오기
        BuildingData data = buildingPrefab.GetComponent<BuildingData>();
        GameManager gameManager = FindObjectOfType<GameManager>();

        if (data != null && gameManager != null)
        {
            // ✅ 예산 부족하면 선택 차단
            if (gameManager.budget < data.cost)
            {
                Debug.Log($"❌ 예산 부족: 현재 예산 {gameManager.budget}, 필요 예산 {data.cost}");
                return;
            }
        }

        // ✅ 예산이 충분하면 건물 선택
        if (TileClickInstaller.Instance != null)
        {
            TileClickInstaller.Instance.SetSelectedBuilding(buildingPrefab);
            Debug.Log($"✅ {buildingPrefab.name} 선택됨");
        }

        // 4. 패널 닫기
        GameObject panel = GameObject.Find(panelNameToClose);
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
}