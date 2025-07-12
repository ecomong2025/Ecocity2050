using UnityEngine;

public class TileController : MonoBehaviour
{
    public bool isUnlocked = false;
    public GameObject tileVisual;

    void Start()
    {
        UpdateVisibility();
    }

    public void UnlockTile()
    {
        isUnlocked = true;
        UpdateVisibility();
    }

    void UpdateVisibility()
    {
        tileVisual.SetActive(isUnlocked);
    }
}
