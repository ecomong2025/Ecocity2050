using UnityEngine;
using UnityEngine.SceneManagement;

public class SkipBtn : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "GameScene"; // 이동할 씬 이름

    public void OnSkip()
    {
        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.PlayClick();
        SceneManager.LoadScene(nextSceneName);
    }
}
