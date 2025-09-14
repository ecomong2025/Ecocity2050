using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SettingManager : MonoBehaviour
{
    [Header("Setting Panel")]
    [SerializeField] private GameObject settingPanel;

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.2f;
    [SerializeField] private Vector3 scaleClosed = new Vector3(0.8f, 0.8f, 1f);
    [SerializeField] private Vector3 scaleOpened = Vector3.one;

    [Header("Audio Buttons")]
    [SerializeField] private Button bgmOnButton;
    [SerializeField] private Button bgmOffButton;
    [SerializeField] private Button sfxOnButton;
    [SerializeField] private Button sfxOffButton;

    [Header("Other Buttons")]
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button settingOpenButton;

    [Header("Audio Sources (Optional)")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;

    private bool isBGMOn = true;
    private bool isSFXOn = true;
    private string previousScene; // 이전 씬 정보 저장

    public static SettingManager Instance { get; private set; }

    private Coroutine animCo;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        LoadSettings();
        SetupButtons();
        UpdateButtonStates();

        // 🔹 씬이 시작될 때, PlayerPrefs에서 불러온 설정대로 오디오 상태 적용
        if (bgmAudioSource != null)
            bgmAudioSource.volume = isBGMOn ? 1f : 0f;

        if (sfxAudioSource != null)
            sfxAudioSource.volume = isSFXOn ? 1f : 0f;

        // 🔹 전역 SFXPlayer까지 반영
        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.SetVolume(isSFXOn ? 1f : 0f);

        if (settingPanel != null)
            settingPanel.SetActive(false);

        // 현재 씬 이름 저장
        previousScene = SceneManager.GetActiveScene().name;
    }

    void SetupButtons()
    {
        if (settingOpenButton != null)
            settingOpenButton.onClick.AddListener(OpenSetting);

        if (bgmOnButton != null) bgmOnButton.onClick.AddListener(() => SetBGM(true));
        if (bgmOffButton != null) bgmOffButton.onClick.AddListener(() => SetBGM(false));
        if (sfxOnButton != null) sfxOnButton.onClick.AddListener(() => SetSFX(true));
        if (sfxOffButton != null) sfxOffButton.onClick.AddListener(() => SetSFX(false));

        if (tutorialButton != null) tutorialButton.onClick.AddListener(OpenTutorial);
        if (logoutButton != null) logoutButton.onClick.AddListener(Logout);
        if (closeButton != null) closeButton.onClick.AddListener(CloseSetting);
    }

    // ===== 오디오 설정 =====
    public void SetBGM(bool isOn)
    {
        isBGMOn = isOn;

        // 🔹 토글 시에만 BGM 변경
        if (bgmAudioSource != null)
            bgmAudioSource.volume = isOn ? 1f : 0f;

        PlayerPrefs.SetInt("BGM", isOn ? 1 : 0);
        PlayerPrefs.Save();
        UpdateBGMButtons();
    }

    public void SetSFX(bool isOn)
    {
        isSFXOn = isOn;

        // 🔹 토글 시에만 SFX 변경
        if (sfxAudioSource != null)
            sfxAudioSource.volume = isOn ? 1f : 0f;

        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.SetVolume(isOn ? 1f : 0f);

        PlayerPrefs.SetInt("SFX", isOn ? 1 : 0);
        PlayerPrefs.Save();
        UpdateSFXButtons();
    }

    public void OpenTutorial()
    {
        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.PlayClick();

        // 현재 씬이 게임 씬인 경우에만 게임 데이터 자동 저장
        if (previousScene == "GameScene" && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.AutoSave();
        }

        // 튜토리얼로 이동할 때 이전 씬 정보를 저장
        PlayerPrefs.SetString("PreviousScene", previousScene);
        PlayerPrefs.SetInt("FromSettings", 1); // 세팅에서 온다는 플래그
        PlayerPrefs.Save();

        string tutorialSceneName = "TutorialScene";
        try
        {
            SceneManager.LoadScene(tutorialSceneName);
        }
        catch
        {
            Debug.LogError("튜토리얼 씬을 찾을 수 없습니다.");
        }
    }

    public void Logout()
    {
        Debug.Log("로그아웃 버튼 클릭됨");

        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.PlayClick();

        StartCoroutine(LogoutProcess());
    }

    private IEnumerator LogoutProcess()
    {
        // 1. 게임 데이터 저장 (로그인된 상태에서만)
        if (GameDataManager.Instance != null && GameDataManager.Instance.IsUserLoggedIn())
        {
            Debug.Log("로그아웃 전 게임 데이터 저장 중...");

            bool saveCompleted = false;
            string saveResult = "";

            GameDataManager.Instance.SaveCurrentGame((success, message) =>
            {
                saveCompleted = true;
                saveResult = message;

                if (success)
                {
                    Debug.Log("로그아웃 전 데이터 저장 성공: " + message);
                }
                else
                {
                    Debug.LogWarning("로그아웃 전 데이터 저장 실패: " + message);
                }
            });

            // 저장 완료 대기 (최대 5초)
            float waitTime = 0f;
            while (!saveCompleted && waitTime < 5f)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
        }

        // 2. 로컬에 JSON으로 현재 게임 데이터 백업 저장
        if (GameDataManager.Instance != null && GameDataManager.Instance.currentPayload != null)
        {
            try
            {
                string jsonData = JsonUtility.ToJson(GameDataManager.Instance.currentPayload, true);
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"GameBackup_{timestamp}.json";

                // Application.persistentDataPath에 저장
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
                System.IO.File.WriteAllText(filePath, jsonData);

                Debug.Log($"게임 데이터 로컬 백업 완료: {filePath}");

                // 백업 파일 정보를 PlayerPrefs에 저장
                PlayerPrefs.SetString("LastBackupFile", fileName);
                PlayerPrefs.SetString("LastBackupPath", filePath);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"로컬 백업 저장 실패: {e.Message}");
            }
        }

        // 3. 카카오 로그아웃 처리
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        if (kakaoManager != null)
        {
            kakaoManager.Logout();
        }

        // 4. 현재 세션의 GameDataManager 데이터만 초기화 (저장은 유지)
        if (GameDataManager.Instance != null)
        {
            Debug.Log("현재 세션 데이터 초기화 (저장된 데이터는 유지)");
            GameDataManager.Instance.currentPayload = null;
        }

        // 5. 씬 전환 관련 PlayerPrefs 초기화
        PlayerPrefs.DeleteKey("FromTutorial");
        PlayerPrefs.DeleteKey("FromSettings");
        PlayerPrefs.DeleteKey("PreviousScene");
        PlayerPrefs.DeleteKey("LoadFromSave");
        PlayerPrefs.DeleteKey("StartNewGame");
        PlayerPrefs.Save();

        // 6. 인트로 씬으로 이동
        Debug.Log("인트로 씬으로 이동합니다.");

        // 인트로 씬 이름을 실제 씬 이름으로 변경해주세요
        string introSceneName = "IntroScene"; // 또는 실제 인트로 씬 이름

        try
        {
            SceneManager.LoadScene(introSceneName);
        }
        catch
        {
            Debug.LogError($"인트로 씬 '{introSceneName}'을 찾을 수 없습니다. GameSceneLoader 씬으로 이동합니다.");
            SceneManager.LoadScene("GameSceneLoader"); // 대체 씬
        }
    }

    // ===== 패널 열기/닫기 =====
    public void OpenSetting()
    {
        if (settingPanel == null) return;
        if (animCo != null) StopCoroutine(animCo);
        settingPanel.SetActive(true);
        animCo = StartCoroutine(AnimatePanel(true));

        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.PlayClick();

        // 현재 씬 정보 업데이트
        previousScene = SceneManager.GetActiveScene().name;
    }

    public void CloseSetting()
    {
        if (settingPanel == null) return;
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(AnimatePanel(false));

        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.PlayClick();
    }

    IEnumerator AnimatePanel(bool open)
    {
        RectTransform rt = settingPanel.GetComponent<RectTransform>();
        Vector3 start = open ? scaleClosed : scaleOpened;
        Vector3 end = open ? scaleOpened : scaleClosed;

        rt.localScale = start;
        float t = 0f;

        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / animDuration);
            // 부드러운 가감속
            float e = (1f - Mathf.Cos(u * Mathf.PI)) * 0.5f;
            rt.localScale = Vector3.Lerp(start, end, e);
            yield return null;
        }

        rt.localScale = end;

        if (!open) settingPanel.SetActive(false);
        animCo = null;
    }

    // ===== 설정 불러오기 & 버튼 상태 =====
    void LoadSettings()
    {
        isBGMOn = PlayerPrefs.GetInt("BGM", 1) == 1;
        isSFXOn = PlayerPrefs.GetInt("SFX", 1) == 1;
    }

    void UpdateButtonStates()
    {
        UpdateBGMButtons();
        UpdateSFXButtons();
    }

    void UpdateBGMButtons()
    {
        if (bgmOnButton != null && bgmOffButton != null)
        {
            bgmOnButton.interactable = !isBGMOn;
            bgmOffButton.interactable = isBGMOn;
            UpdateButtonColor(bgmOnButton, isBGMOn);
            UpdateButtonColor(bgmOffButton, !isBGMOn);
        }
    }

    void UpdateSFXButtons()
    {
        if (sfxOnButton != null && sfxOffButton != null)
        {
            sfxOnButton.interactable = !isSFXOn;
            sfxOffButton.interactable = isSFXOn;
            UpdateButtonColor(sfxOnButton, isSFXOn);
            UpdateButtonColor(sfxOffButton, !isSFXOn);
        }
    }

    void UpdateButtonColor(Button button, bool isActive)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        colors.normalColor = isActive ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
        button.colors = colors;
    }

    public bool IsBGMOn() => isBGMOn;
    public bool IsSFXOn() => isSFXOn;
}