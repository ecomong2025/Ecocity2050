using UnityEngine;

public class CloseBuildingPanel : MonoBehaviour
{
    [SerializeField] private GameObject targetPanel;

    public void Close()
    {
        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("targetPanel이 설정되지 않았습니다!");
        }
    }
}
