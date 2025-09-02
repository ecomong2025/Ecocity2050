using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameSceneLoader : MonoBehaviour
{
    public Slider loadingBar;
    public GameObject startButton;
    public float fakeLoadTime = 3f;
    public GameObject kakaoButton;

    private float timer = 0f;
    private bool isLoading = true;

    void Start()
    {
        kakaoButton.SetActive(false);
        startButton.SetActive(false);
        loadingBar.value = 0f;
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
                loadingBar.value = 1f;
                loadingBar.gameObject.SetActive(false);
                kakaoButton.SetActive(true);
            }
        }
    }

    public void OnKakaoLogin()
    {
        KakaoStartManager.Instance.TryLogin();
    }

    public void OnKakaoLoginSuccess()
    {
        kakaoButton.SetActive(false);
        startButton.SetActive(true);
    }

    public void OnStartGame()
    {
        SceneManager.LoadScene("TutorialScene");
    }

    public void OnExit()
    {
        Application.Quit();
    }
}
