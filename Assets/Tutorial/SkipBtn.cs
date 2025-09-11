using UnityEngine;
using UnityEngine.SceneManagement;

public class SkipBtn : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "GameScene"; // 이동할 씬 이름

    public void OnSkip()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
