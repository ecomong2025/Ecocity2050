using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private string panelNameToClose = "BuildingPanel";

    private GameObject buildingPrefab;

    void Start()
    {
        // 1. 버튼 이름에서 "Btn" 제거 (접미사 제거)
        string prefabName = gameObject.name.Replace("Btn", "");

        // 2. 프리팹 자동 로드 (Resources/Buildings/프리팹이름)
        buildingPrefab = Resources.Load<GameObject>("Buildings/" + prefabName);

        if (buildingPrefab == null)
        {
            Debug.LogError($"❌ Resources/Buildings/{prefabName}.prefab 프리팹을 찾을 수 없습니다.");
            return;
        }

        // 3. 버튼 클릭 이벤트 등록
        GetComponent<Button>().onClick.AddListener(OnButtonClick);
    }

    void OnButtonClick()
    {
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
