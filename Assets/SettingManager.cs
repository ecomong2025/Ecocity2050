using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SettingManager : MonoBehaviour
{
    [Header("Setting Panel")]
    [SerializeField] private GameObject settingPanel;

    [Header("Tutorial Panel")]
    [SerializeField] private GameObject tutorialPanel;

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

        if (bgmAudioSource != null)
            bgmAudioSource.volume = isBGMOn ? 1f : 0f;
        if (sfxAudioSource != null)
            sfxAudioSource.volume = isSFXOn ? 1f : 0f;
        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.SetVolume(isSFXOn ? 1f : 0f);

        if (settingPanel != null) settingPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false); // 시작시 꺼두기
    }

    void SetupButtons()
    {
        if (settingOpenButton != null) settingOpenButton.onClick.AddListener(OpenSetting);
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
        if (bgmAudioSource != null) bgmAudioSource.volume = isOn ? 1f : 0f;
        PlayerPrefs.SetInt("BGM", isOn ? 1 : 0);
        PlayerPrefs.Save();
        UpdateBGMButtons();
    }

    public void SetSFX(bool isOn)
    {
        isSFXOn = isOn;
        if (sfxAudioSource != null) sfxAudioSource.volume = isOn ? 1f : 0f;
        if (SFXPlayer.Instance != null) SFXPlayer.Instance.SetVolume(isOn ? 1f : 0f);
        PlayerPrefs.SetInt("SFX", isOn ? 1 : 0);
        PlayerPrefs.Save();
        UpdateSFXButtons();
    }

    // ===== 튜토리얼 열기 =====
    public void OpenTutorial()
    {
        if (SFXPlayer.Instance != null) SFXPlayer.Instance.PlayClick();
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
    }

    // ===== 로그아웃 (생략 가능, 기존 로직 그대로) =====
    public void Logout()
    {
        Debug.Log("로그아웃 버튼 클릭됨");
        if (SFXPlayer.Instance != null) SFXPlayer.Instance.PlayClick();
        // 실제 로그아웃 로직 추가
    }

    // ===== 패널 열기/닫기 =====
    public void OpenSetting()
    {
        if (settingPanel == null) return;
        if (animCo != null) StopCoroutine(animCo);
        settingPanel.SetActive(true);
        animCo = StartCoroutine(AnimatePanel(true));
        if (SFXPlayer.Instance != null) SFXPlayer.Instance.PlayClick();
    }

    public void CloseSetting()
    {
        if (settingPanel == null) return;
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(AnimatePanel(false));
        if (SFXPlayer.Instance != null) SFXPlayer.Instance.PlayClick();
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
            float e = (1f - Mathf.Cos(u * Mathf.PI)) * 0.5f;
            rt.localScale = Vector3.Lerp(start, end, e);
            yield return null;
        }
        rt.localScale = end;
        if (!open) settingPanel.SetActive(false);
        animCo = null;
    }

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
