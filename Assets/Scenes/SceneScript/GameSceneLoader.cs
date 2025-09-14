using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameSceneLoader : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider loadingBar;
    public GameObject startButton;
    public GameObject continueButton;
    public GameObject tutorialButton;
    public GameObject kakaoButton;
    public GameObject nullInfo;

    [Header("Loading Settings")]
    public float fakeLoadTime = 3f;

    private float timer = 0f;
    private bool isLoading = true;
    private bool hasSavedData = false;

    void Start()
    {
        bool fromTutorial = PlayerPrefs.GetInt("FromTutorial", 0) == 1;
        PlayerPrefs.SetInt("FromTutorial", 0); // 한 번 쓰고 초기화

        if (fromTutorial)
        {
            // 튜토리얼에서 넘어온 경우 → 바로 버튼 UI 세팅
            SetupButtonsAfterLogin();
            return;
        }
        else
        {
            // 일반적인 로딩 시작
            StartLoading();
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

    private void StartLoading()
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

    private void SetupButtonsAfterLogin()
    {
        loadingBar.gameObject.SetActive(false);
        kakaoButton.SetActive(false);
        nullInfo.SetActive(false);

        startButton.SetActive(true);
        tutorialButton.SetActive(true);


        // 저장된 데이터 확인 후 이어하기 버튼 활성화 여부 결정
        CheckAndSetupContinueButton();

        isLoading = false;
    }

    /// <summary>
    /// 저장된 데이터를 확인하고 이어하기 버튼 설정
    /// </summary>
    private void CheckAndSetupContinueButton()
    {
        if (SaveGameManager.Instance == null)
        {
            continueButton.SetActive(false);
            return;
        }

        SaveGameManager.Instance.CheckSavedDataExists((success, exists) =>
        {
            if (success && exists)
            {
                continueButton.SetActive(true);
                hasSavedData = true;
                Debug.Log("저장된 게임 데이터 발견 - 이어하기 버튼 활성화");
            }
            else
            {
                continueButton.SetActive(true);
                hasSavedData = false;
                Debug.Log("저장된 게임 데이터 없음 - 이어하기 버튼 비활성화");
            }
        });
    }

    public void OnKakaoLogin()
    {
        SFXPlayer.Instance.PlayClick();

        // KakaoLoginManager 찾아서 로그인 시작
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        if (kakaoManager != null)
        {
            kakaoManager.StartKakaoLogin();
        }
        else
        {
            Debug.LogError("KakaoLoginManager를 찾을 수 없습니다!");
        }
    }

    public void OnKakaoLoginSuccess()
    {
        Debug.Log("카카오 로그인 성공! 버튼 설정 시작...");
        SetupButtonsAfterLogin();
    }

    public void OnStartGame()
    {
        SFXPlayer.Instance.PlayClick();

        // GameDataManager 초기화 (새 게임용)
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.InitializeNewGame();
        }

        // 새 게임 시작 플래그
        PlayerPrefs.SetInt("StartNewGame", 1);
        PlayerPrefs.DeleteKey("LoadFromSave"); // 이어하기 플래그 제거
        PlayerPrefs.Save();

        SceneManager.LoadScene("TutorialScene");
    }

    public void OnStartTutorial()
    {
        SFXPlayer.Instance.PlayClick();

        // 세팅에서 온 게 아니라는 것을 명시
        PlayerPrefs.DeleteKey("FromSettings");
        PlayerPrefs.DeleteKey("PreviousScene");
        PlayerPrefs.Save();

        SceneManager.LoadScene("TutorialScene");
    }

    public void OnContinue()
    {
        SFXPlayer.Instance.PlayClick();

        if (SaveGameManager.Instance == null)
        {
            Debug.LogError("⚠️ SaveGameManager가 없어 이어하기 불가");
            nullInfo.SetActive(true);
            return;
        }

        // 저장된 데이터 확인 → 있으면 불러오기, 없으면 NullInfo 활성화
        SaveGameManager.Instance.CheckSavedDataExists((success, exists) =>
        {
            Debug.Log($"저장된 데이터 확인 결과 - Success: {success}, Exists: {exists}");

            if (!success || !exists)
            {
                Debug.Log("저장된 데이터가 없습니다. NullInfo 활성화");
                nullInfo.SetActive(true);
                return;
            }

            // 저장된 데이터가 있음 → 불러오기
            LoadSavedGameAndContinue();
        });
    }

    private void LoadSavedGameAndContinue()
    {
        SaveGameManager.Instance.LoadGameData((success, payload, message) =>
        {
            if (success && payload != null)
            {
                // 불러온 데이터를 GameDataManager에 설정
                if (GameDataManager.Instance != null)
                {
                    GameDataManager.Instance.currentPayload = payload;
                }
                else
                {
                    // GameDataManager가 없으면 정적 변수에 저장
                    GameDataManager.LoadedPayload = payload;
                }

                PlayerPrefs.SetInt("LoadFromSave", 1);
                PlayerPrefs.DeleteKey("StartNewGame"); // 새 게임 플래그 제거
                PlayerPrefs.Save();

                Debug.Log($"게임 데이터 불러오기 성공: CO2={payload.co2Tons}, 예산={payload.budget}");

                // 게임 씬으로 이동
                SceneManager.LoadScene("GameScene"); // 실제 게임 씬 이름으로 변경
            }
            else
            {
                Debug.LogError($"게임 데이터 불러오기 실패: {message}");
                nullInfo.SetActive(true);
            }
        });
    }

    public void OnExit()
    {
        Application.Quit();
    }
}