using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 튜토리얼 씬에서 사용할 컨트롤러
/// 세팅에서 온 경우와 일반 게임 시작의 경우를 구분하여 처리
/// </summary>
public class TutorialSceneController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button backButton; // 뒤로가기 버튼
    [SerializeField] private Button continueButton; // 게임 시작/계속하기 버튼
    [SerializeField] private GameObject backButtonObject; // 뒤로가기 버튼 오브젝트 (활성화/비활성화용)

    private bool isFromSettings = false;
    private string previousScene = "";

    void Start()
    {
        // 세팅에서 온 건지 확인
        isFromSettings = PlayerPrefs.GetInt("FromSettings", 0) == 1;
        previousScene = PlayerPrefs.GetString("PreviousScene", "GameScene");

        SetupUI();
        SetupButtons();
    }

    void SetupUI()
    {
        // 세팅에서 온 경우에만 뒤로가기 버튼 표시
        if (backButtonObject != null)
        {
            backButtonObject.SetActive(isFromSettings);
        }

        // 버튼 텍스트 변경 (옵션)
        if (continueButton != null)
        {
            Text buttonText = continueButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = isFromSettings ? "계속하기" : "게임 시작";
            }
        }
    }


    void SetupButtons()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(GoBackToPreviousScene);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    // Add this method to handle the missing 'OnContinueClicked' reference
    private void OnContinueClicked()
    {
        OnTutorialComplete();
    }

    /// <summary>
    /// 튜토리얼 완료 후 호출할 공개 메서드
    /// 기존 튜토리얼 UI에서 이 메서드를 호출하면 됩니다
    /// </summary>
    public void OnTutorialComplete()
    {
        if (isFromSettings)
        {
            // 세팅에서 온 경우: 이전 씬으로 돌아가기
            GoBackToPreviousScene();
        }
        else
        {
            // 일반적인 튜토리얼 완료 후: 게임 씬으로 이동
            PlayerPrefs.SetInt("FromTutorial", 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene("GameScene"); // 또는 실제 게임 씬 이름
        }
    }

    /// <summary>
    /// 이전 씬으로 돌아가기
    /// </summary>
    private void GoBackToPreviousScene()
    {
        // FromSettings 플래그 제거
        PlayerPrefs.DeleteKey("FromSettings");
        PlayerPrefs.DeleteKey("PreviousScene");
        PlayerPrefs.Save();

        // 이전 씬으로 돌아가기
        try
        {
            SceneManager.LoadScene(previousScene);
        }
        catch
        {
            Debug.LogError($"이전 씬 '{previousScene}'으로 돌아갈 수 없습니다. GameScene으로 이동합니다.");
            SceneManager.LoadScene("GameScene");
        }
    }

    /// <summary>
    /// ESC 키나 뒤로가기 키 처리 (세팅에서 온 경우만)
    /// </summary>
    void Update()
    {
        // ESC 키 또는 Android 뒤로가기 키 처리
        if (Input.GetKeyDown(KeyCode.Escape) && isFromSettings)
        {
            GoBackToPreviousScene();
        }
    }
}