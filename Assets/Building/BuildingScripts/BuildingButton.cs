using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private string panelNameToClose = "BuildingPanel";

    private string panelPath = "WarningPanel";
    private GameObject warningPanel;      
    private Button confirmButton;         
    private GameObject buildingPrefab;

    void Awake()
    {
        if (warningPanel == null)
        {
            var canvas = GetComponentInParent<Canvas>(true);
            var t = canvas ? canvas.transform.Find(panelPath) : null;
            if (t) warningPanel = t.gameObject;
        }

        if (confirmButton == null && warningPanel != null)
            confirmButton = warningPanel.GetComponentInChildren<Button>(true);

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => warningPanel.SetActive(false));
        }

        // 시작은 숨김(선택)
        if (warningPanel != null) warningPanel.SetActive(false);
    }

    void Start()
    {
        string baseName = gameObject.name.Replace("Btn", "");
        string prefabName = baseName + "Prefab";
        string resourcePath = "Buildings/Prefabs/" + prefabName;

        buildingPrefab = Resources.Load<GameObject>(resourcePath);

        if (buildingPrefab == null)
        {
            Debug.LogError($"❌ 프리팹을 찾을 수 없습니다: Resources/{resourcePath}.prefab");
            return;
        }

        GetComponent<Button>().onClick.AddListener(OnButtonClick);

        // 확인 버튼 이벤트 연결
        if (confirmButton != null)
            confirmButton.onClick.AddListener(() => warningPanel.SetActive(false));
        
        if (warningPanel != null)
            warningPanel.SetActive(false);
    }

    void OnButtonClick()
    {
        BuildingData data = buildingPrefab.GetComponent<BuildingData>();
        GameManager gameManager = FindFirstObjectByType<GameManager>();

        if (data != null && gameManager != null)
        {
            if (gameManager.budget < data.cost)
            {
                Debug.Log($"❌ 예산 부족: 현재 예산 {gameManager.budget}, 필요 예산 {data.cost}");
                if (warningPanel != null)
                    warningPanel.SetActive(true);   
                return;
            }
        }

        if (TileClickInstaller.Instance != null)
        {
            TileClickInstaller.Instance.SetSelectedBuilding(buildingPrefab);
            Debug.Log($"✅ {buildingPrefab.name} 선택됨");
        }

        GameObject panel = GameObject.Find(panelNameToClose);
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
}