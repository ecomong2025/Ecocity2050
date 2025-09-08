using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private string panelNameToClose = "BuildingPanel";

    [Header("Warning Panel (Budget)")]
    [SerializeField] private GameObject warningPanel;             // 예산부족 경고 패널
    [SerializeField] private Button warningConfirmButton;         // 확인 버튼(옵션)
    [SerializeField] private string fallbackPanelPath = "WarningPanel";

    [Header("클릭 막고 싶은 UI 오브젝트들")]
    [Tooltip("WarningPanel 열릴 때 여기 등록된 UI들의 클릭과 레이캐스트를 잠금")]
    [SerializeField] private List<GameObject> blockTargets = new List<GameObject>();

    private readonly List<CanvasGroup> cachedGroups = new List<CanvasGroup>();
    private GameObject buildingPrefab;

    void Awake()
    {
        ResolveWarningPanel();
        WireConfirm();

        if (warningPanel) warningPanel.SetActive(false);

        // 🔹 패널이 어떤 경로로 꺼지든 자동으로 해제되도록 훅 부착
        if (warningPanel)
        {
            var hook = warningPanel.GetComponent<WarningPanelHook>();
            if (hook == null) hook = warningPanel.AddComponent<WarningPanelHook>();
            hook.owner = this;
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

        // 예산 부족이면 WarningPanel 열고 뒤 UI 잠금
        if (data && gameManager && gameManager.budget < data.cost)
        {
            Debug.Log($"❌ 예산 부족: 현재 {gameManager.budget}, 필요 {data.cost}");
            OpenWarningAndBlock();
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

    // ---------- Warning Panel 열기/닫기 + 뒤 UI 잠금 ----------
    void OpenWarningAndBlock()
    {
        if (!warningPanel)
        {
            Debug.LogError("WarningPanel 참조가 없습니다. 인스펙터에 연결하세요.");
            return;
        }

        // 뒤 UI 잠금
        cachedGroups.Clear();
        foreach (var go in blockTargets)
        {
            if (go == null) continue;
            // 혹시 WarningPanel을 blockTargets에 넣어놨다면 스킵
            if (go == warningPanel) continue;

            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();

            // 잠금
            cg.interactable = false;
            cg.blocksRaycasts = false;

            cachedGroups.Add(cg);
        }

        // 경고 패널 활성화 (패널은 당연히 조작 가능해야 함)
        warningPanel.SetActive(true);

        // 혹시 패널에 CanvasGroup이 있다면 조작 가능하게 보장
        var wcg = warningPanel.GetComponent<CanvasGroup>();
        if (wcg)
        {
            wcg.interactable = true;
            wcg.blocksRaycasts = true;
        }
    }

    void CloseWarningAndUnblock()
    {
        if (warningPanel) warningPanel.SetActive(false);

        // 뒤 UI 원복
        foreach (var cg in cachedGroups)
        {
            if (cg == null) continue;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        cachedGroups.Clear();
    }

    // ---------- 초기 참조/연결 유틸 ----------
    void ResolveWarningPanel()
    {
        if (warningPanel != null) return;

        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas && !string.IsNullOrEmpty(fallbackPanelPath))
        {
            var t = canvas.transform.Find(fallbackPanelPath);
            if (t) warningPanel = t.gameObject;
        }

#if UNITY_EDITOR
        if (!warningPanel)
        {
            foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (tr.hideFlags == HideFlags.None && tr.gameObject.scene.IsValid() && tr.name == fallbackPanelPath)
                { warningPanel = tr.gameObject; break; }
            }
        }
#else
        if (!warningPanel)
        {
            var go = GameObject.Find(fallbackPanelPath);
            if (go) warningPanel = go;
        }
#endif

        if (!warningPanel)
            Debug.LogWarning("[BuildingButton] WarningPanel을 찾지 못했습니다. 인스펙터에 직접 할당하세요.");
    }

    void WireConfirm()
    {
        if (!warningPanel) return;

        if (warningConfirmButton == null)
            warningConfirmButton = warningPanel.GetComponentInChildren<Button>(true);

        if (warningConfirmButton)
        {
            warningConfirmButton.onClick.RemoveAllListeners();
            warningConfirmButton.onClick.AddListener(() =>
            {
                SFXPlayer.Instance?.PlayClick();
                CloseWarningAndUnblock();
            });
        }
        else
        {
            Debug.LogWarning("[BuildingButton] WarningPanel 하위에서 Button을 찾지 못했습니다. 인스펙터에 연결하세요.");
        }
    }
}

// WarningPanel이 비활성화될 때 뒤 UI 잠금을 자동 해제
public class WarningPanelHook : MonoBehaviour
{
    public BuildingButton owner;

    void OnDisable()
    {
        // 패널이 다른 코드에 의해 꺼져도 blockTargets 해제 보장
        if (owner != null)
        {
            owner.SendMessage("CloseWarningAndUnblock", SendMessageOptions.DontRequireReceiver);
        }
    }
}