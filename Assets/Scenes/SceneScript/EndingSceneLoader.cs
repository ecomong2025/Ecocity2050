using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingSceneLoader : MonoBehaviour
{
    public string endingSceneName = "EndingScene";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha7)) // ¼ýÀÚ 7 Å°
        {
            LoadEndingScene();
        }
    }

    public void LoadEndingScene()
    {
        SceneManager.LoadScene("EndingScene");
    }
}
