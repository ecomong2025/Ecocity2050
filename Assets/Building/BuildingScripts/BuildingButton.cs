using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private string panelNameToClose = "BuildingPanel";
    [SerializeField] private WarningPanelController warningController; // ★ 여기로 대체!

    private GameObject buildingPrefab;

    void Awake()
    {
        if (!warningController)
        {
            // 같은 Canvas/계층에서 찾아보기(선택)
            warningController = FindFirstObjectByType<WarningPanelController>();
        }
    }

    void Start()
    {
        string baseName = gameObject.name.Replace("Btn", "");
        string prefabName = baseName + "Prefab";
        string resourcePath = "Buildings/Prefabs/" + prefabName;

        buildingPrefab = Resources.Load<GameObject>(resourcePath);
        if (!buildingPrefab)
        {
            Debug.LogError($"❌ 프리팹을 찾을 수 없습니다: Resources/{resourcePath}.prefab");
            return;
        }

        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnButtonClick);
    }

    void OnButtonClick()
    {
        SFXPlayer.Instance?.PlayClick();

        var data = buildingPrefab.GetComponent<BuildingData>();
        var gameManager = FindFirstObjectByType<GameManager>();

        // 예산 부족 → 컨트롤러에 Open만 요청
        if (data && gameManager && gameManager.budget < data.cost)
        {
            Debug.Log($"❌ 예산 부족: 현재 {gameManager.budget}, 필요 {data.cost}");
            if (warningController) warningController.Open();
            else Debug.LogError("WarningPanelController 참조가 없습니다. 인스펙터에 연결하세요.");
            return;
        }

        // 정상 선택
        if (TileClickInstaller.Instance)
        {
            TileClickInstaller.Instance.SetSelectedBuilding(buildingPrefab);
            Debug.Log($"✅ {buildingPrefab.name} 선택됨");
        }

        var panel = GameObject.Find(panelNameToClose);
        if (panel) panel.SetActive(false);
    }
}