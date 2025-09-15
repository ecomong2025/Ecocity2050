using UnityEngine;

public class SkipBtnTutorial : MonoBehaviour
{
    [SerializeField] private GameObject tutorialPanel; // Inspector에서 TutorialPanel 연결

    public void OnSkip()
    {
        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.PlayClick();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
    }
}

