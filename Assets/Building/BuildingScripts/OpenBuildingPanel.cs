using UnityEngine;
using UnityEngine.UI;

public class OpenBuildingPanel : MonoBehaviour
{
    public Button buildingPanelButton;
    public GameObject buildingPanel;
    public GameObject gameUI;

    // ? 두 개 버튼만 연결
    public Button confirmInstallButton;
    public Button cancelInstallButton;

    void Start()
    {
        if (buildingPanelButton != null)
            buildingPanelButton.onClick.AddListener(OnOpenBuildingPanel);

        if (confirmInstallButton != null)
            confirmInstallButton.onClick.AddListener(ShowGameUI);

        if (cancelInstallButton != null)
            cancelInstallButton.onClick.AddListener(ShowGameUI);
    }

    void OnOpenBuildingPanel()
    {
        if (buildingPanel != null)
            buildingPanel.SetActive(true);

        if (gameUI != null)
            gameUI.SetActive(false);
    }

    public void ShowGameUI()
    {
        if (gameUI != null)
            gameUI.SetActive(true);
    }
}
