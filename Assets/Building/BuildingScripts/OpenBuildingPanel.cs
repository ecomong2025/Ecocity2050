using UnityEngine;
using UnityEngine.UI;

public class BuildingUI: MonoBehaviour
{
    public Button buildingPanelButton;
    public GameObject buildingPanel;

    void Start()
    {
        buildingPanelButton.onClick.AddListener(OnButtonPressed);
    }

    void OnButtonPressed()
    {
        if (buildingPanel != null)
        {
            buildingPanel.SetActive(true);
        }
    }
}
