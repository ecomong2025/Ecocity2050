using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneChangeButton : MonoBehaviour
{
    public string sceneName; // 전환할 씬 이름

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => {
            SceneManager.LoadScene(sceneName);
        });
    }
}
