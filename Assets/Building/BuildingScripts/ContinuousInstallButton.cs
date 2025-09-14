using UnityEngine;
using UnityEngine.UI;

public class ContinuousInstallButton : MonoBehaviour
{
    public TileClickInstaller installer;

    void Awake()
    {
        GetComponent<Button>()?.onClick.AddListener(() =>
        {
            installer?.OnContinuousButtonClicked();
        });
    }
}
