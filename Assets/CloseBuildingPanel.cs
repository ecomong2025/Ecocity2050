using UnityEngine;
using UnityEngine.UI;

public class CloseBuildingPanel : MonoBehaviour
{
    public Button buildingPanelButton;
    public GameObject buildingPanel;

    void Start()
    {
        buildingPanelButton.onClick.AddListener(OffButtonPressed);
    }

    void OffButtonPressed()
    {
        if (buildingPanel != null)
        {
            buildingPanel.SetActive(false);
        }
    }
}
