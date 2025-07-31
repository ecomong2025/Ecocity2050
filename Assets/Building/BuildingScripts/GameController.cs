using UnityEngine;

public class GameUIController : MonoBehaviour
{
    public GameObject gameUI;

    public void ShowGameUI()
    {
        if (gameUI != null)
            gameUI.SetActive(true);
    }

    public void HideGameUI()
    {
        if (gameUI != null)
            gameUI.SetActive(false);
    }
}
