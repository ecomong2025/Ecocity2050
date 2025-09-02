using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameSceneLoader : MonoBehaviour
{
    public Slider loadingBar;      // 로딩바
    public GameObject startButton; // Start 버튼 오브젝트
    public float fakeLoadTime = 3f;
    public GameObject kakaoButton;

    private float timer = 0f;
    private bool isLoading = true;

    void Start()
    {
        kakaoButton.SetActive(false); //카카오버튼 숨김
        startButton.SetActive(false); // 처음엔 버튼 숨김
        loadingBar.value = 0f;        // 로딩바 초기화
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
                loadingBar.value = 1f;      // 꽉 채우기
                loadingBar.gameObject.SetActive(false); // 로딩바 숨김
                kakaoButton.SetActive(true); //카카오 버튼 보이기
            }
        }
    }

    //카카오 버튼 클릭 시 실행
    public void OnKakaoLogin()
    {
        KakaoStartManager.Instance.TryLogin();
    }

    public void OnKakaoLoginSuccess()
    {
        kakaoButton.SetActive(false);
        startButton.SetActive(true);
    }
    // Start 버튼 클릭 시 실행
    public void OnStartGame()
    {
        SceneManager.LoadScene("TutorialScene"); // 씬 이름 맞춰 수정
    }

    public void OnExit()
    {
        Application.Quit();
    }
}
