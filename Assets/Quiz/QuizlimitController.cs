using UnityEngine;
using UnityEngine.UI;

public class QuizlimitController : MonoBehaviour
{
    public GameObject quizLimitPanel;
    public GameObject quizPanel;
    public GameObject gamePanel;
    public Button okButton;

    private void Start()
    {
        quizLimitPanel.SetActive(false);
        okButton.onClick.AddListener(OnOkClicked);
    }

    public void ShowLimitPanel()
    {
        quizLimitPanel.SetActive(true);
        quizPanel.SetActive(false);

    }

    private void OnOkClicked()
    {
        SFXPlayer.Instance.PlayClick();
        quizLimitPanel.SetActive(false);
        gamePanel.SetActive(true);
    }
}
