using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class IntroManager : MonoBehaviour
{
    public GameObject loadingPanel;
    public GameObject mainMenuPanel;
    public Slider loadingBar;
    public float fakeLoadTime = 3f;

    private float timer = 0f;
    private bool isLoading = true;

    void Start()
    {
        loadingPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
    }

    void Update()
    {
        if (isLoading)
        {
            timer += Time.deltaTime;
            loadingBar.value = timer / fakeLoadTime;

            if (timer >= fakeLoadTime)
            {
                isLoading = false;
                loadingPanel.SetActive(false);
                mainMenuPanel.SetActive(true);
            }
        }
    }

    // ��ư �̺�Ʈ��
    public void OnStartGame()
    {
        SceneManager.LoadScene("GameScene"); // ���� �� �̸��� �°� ����
    }

    public void OnExit()
    {
        Application.Quit();
    }
}
