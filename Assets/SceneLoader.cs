using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneChangeButton : MonoBehaviour
{
    public string sceneName; // ��ȯ�� �� �̸�

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => {
            SceneManager.LoadScene(sceneName);
        });
    }
}
