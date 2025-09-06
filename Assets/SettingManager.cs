using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingManager : MonoBehaviour
{
    [Header("Setting Panel")]
    [SerializeField] private GameObject settingPanel;

    [Header("Audio Buttons")]
    [SerializeField] private Button bgmOnButton;
    [SerializeField] private Button bgmOffButton;
    [SerializeField] private Button sfxOnButton;
    [SerializeField] private Button sfxOffButton;

    [Header("Other Buttons")]
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button settingOpenButton; // 설정 열기 버튼 추가

    [Header("Audio Sources (Optional)")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;

    // 설정 값들을 저장할 변수
    private bool isBGMOn = true;
    private bool isSFXOn = true;

    // 싱글톤 패턴 (선택사항)
    public static SettingManager Instance { get; private set; }

    void Awake()
    {
        // 싱글톤 설정
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
        // PlayerPrefs에서 이전 설정 불러오기
        LoadSettings();

        // 버튼 이벤트 연결
        SetupButtons();

        // 초기 버튼 상태 설정
        UpdateButtonStates();

        // 설정 창은 처음에 비활성화
        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    void SetupButtons()
    {
        // 설정 열기 버튼
        if (settingOpenButton != null)
            settingOpenButton.onClick.AddListener(OpenSetting);

        // BGM 버튼들
        if (bgmOnButton != null)
            bgmOnButton.onClick.AddListener(() => SetBGM(true));
        if (bgmOffButton != null)
            bgmOffButton.onClick.AddListener(() => SetBGM(false));

        // SFX 버튼들
        if (sfxOnButton != null)
            sfxOnButton.onClick.AddListener(() => SetSFX(true));
        if (sfxOffButton != null)
            sfxOffButton.onClick.AddListener(() => SetSFX(false));

        // 기타 버튼들
        if (tutorialButton != null)
            tutorialButton.onClick.AddListener(OpenTutorial);
        if (logoutButton != null)
            logoutButton.onClick.AddListener(Logout);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseSetting);
    }

    // BGM 설정
    public void SetBGM(bool isOn)
    {
        isBGMOn = isOn;

        // AudioSource가 연결되어 있다면 볼륨 조절
        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = isOn ? 1f : 0f;
        }

        // 설정 저장
        PlayerPrefs.SetInt("BGM", isOn ? 1 : 0);
        PlayerPrefs.Save();

        // 버튼 상태 업데이트
        UpdateBGMButtons();

        Debug.Log($"BGM 설정: {(isOn ? "ON" : "OFF")}");
    }

    // SFX 설정
    public void SetSFX(bool isOn)
    {
        isSFXOn = isOn;

        // AudioSource가 연결되어 있다면 볼륨 조절
        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = isOn ? 1f : 0f;
        }

        // 설정 저장
        PlayerPrefs.SetInt("SFX", isOn ? 1 : 0);
        PlayerPrefs.Save();

        // 버튼 상태 업데이트
        UpdateSFXButtons();

        Debug.Log($"SFX 설정: {(isOn ? "ON" : "OFF")}");
    }


    public void OpenTutorial()
    {
        Debug.Log("튜토리얼 씬으로 이동");

        string tutorialSceneName = "TutorialScene"; 

        // 씬이 Build Settings에 있는지 확인
        try
        {
            SceneManager.LoadScene(tutorialSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"튜토리얼 씬을 찾을 수 없습니다: {tutorialSceneName}");
            Debug.LogError($"에러 내용: {e.Message}");
            Debug.LogError("Build Settings에 씬이 추가되어 있는지 확인하세요!");
        }
    }

    // 로그아웃 (일단 빈 함수로 두기)
    public void Logout()
    {
        // 로그아웃 로직을 여기에 추가
        Debug.Log("로그아웃 버튼 클릭됨");
    }

    // 설정 창 열기
    public void OpenSetting()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            Debug.Log("설정 창 열기");
        }
        else
        {
            Debug.LogWarning("Setting Panel이 연결되지 않았습니다!");
        }
    }

    // 설정 창 닫기
    public void CloseSetting()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
            Debug.Log("설정 창 닫기");
        }
    }

    // 이전 설정 불러오기
    void LoadSettings()
    {
        isBGMOn = PlayerPrefs.GetInt("BGM", 1) == 1;
        isSFXOn = PlayerPrefs.GetInt("SFX", 1) == 1;

        Debug.Log($"설정 불러오기 - BGM: {isBGMOn}, SFX: {isSFXOn}");
    }

    // 모든 버튼 상태 업데이트
    void UpdateButtonStates()
    {
        UpdateBGMButtons();
        UpdateSFXButtons();
    }

    // BGM 버튼 상태 업데이트
    void UpdateBGMButtons()
    {
        if (bgmOnButton != null && bgmOffButton != null)
        {
            // ON 버튼 상태 (BGM이 켜져있으면 ON 버튼은 눌린 상태로 보이게)
            bgmOnButton.interactable = !isBGMOn;
            bgmOffButton.interactable = isBGMOn;

            // 색상으로 구분하기
            UpdateButtonColor(bgmOnButton, isBGMOn);
            UpdateButtonColor(bgmOffButton, !isBGMOn);
        }
        else
        {
            Debug.LogWarning("BGM 버튼들이 연결되지 않았습니다!");
        }
    }

    // SFX 버튼 상태 업데이트
    void UpdateSFXButtons()
    {
        if (sfxOnButton != null && sfxOffButton != null)
        {
            sfxOnButton.interactable = !isSFXOn;
            sfxOffButton.interactable = isSFXOn;

            // 색상으로 구분하기
            UpdateButtonColor(sfxOnButton, isSFXOn);
            UpdateButtonColor(sfxOffButton, !isSFXOn);
        }
        else
        {
            Debug.LogWarning("SFX 버튼들이 연결되지 않았습니다!");
        }
    }

    // 버튼 색상 업데이트 헬퍼 함수
    void UpdateButtonColor(Button button, bool isActive)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        colors.normalColor = isActive ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 1f);
        button.colors = colors;
    }

    // 외부에서 현재 설정을 확인할 수 있는 함수들
    public bool IsBGMOn() => isBGMOn;
    public bool IsSFXOn() => isSFXOn;
}