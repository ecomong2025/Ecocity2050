using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameSceneLoader : MonoBehaviour
{
    public Slider loadingBar;
    public GameObject startButton;
    public GameObject continueButton;
    public GameObject tutorialButton;
    public float fakeLoadTime = 3f;
    public GameObject kakaoButton;
    public GameObject nullInfo;

    private float timer = 0f;
    private bool isLoading = true;

    void Start()
    {
        bool fromTutorial = PlayerPrefs.GetInt("FromTutorial", 0) == 1;
        PlayerPrefs.SetInt("FromTutorial", 0); // 한 번 쓰고 초기화

        if (fromTutorial)
        {
            // 튜토리얼에서 넘어온 경우 → 바로 버튼 UI 세팅
            loadingBar.gameObject.SetActive(false);
            kakaoButton.SetActive(false);
            nullInfo.SetActive(false);
            startButton.SetActive(true);
            continueButton.SetActive(true);
            tutorialButton.SetActive(true);
            isLoading = false;
            return;
        }
        else
        {
            kakaoButton.SetActive(false);
            nullInfo.SetActive(false);
            startButton.SetActive(false);
            continueButton.SetActive(false);
            tutorialButton.SetActive(false);
            loadingBar.value = 0f;
            isLoading = true;
            timer = 0f;
        }
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
        continueButton.SetActive(true);
        tutorialButton.SetActive(true);
    }

    public void OnStartGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void OnStartTutorial()
    {
        SceneManager.LoadScene("TutorialScene");
    }
    public void OnContinue()
    {
        nullInfo.SetActive(true);
        
    }

    public void OnStartbtn()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void OnExit()
    {
        Application.Quit();
    }
}
