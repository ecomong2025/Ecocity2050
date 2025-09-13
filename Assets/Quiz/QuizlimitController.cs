using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class QuizlimitController : MonoBehaviour
{
    public GameObject quizLimitPanel;
    public GameObject gamePanel;
    public Button okButton;

    [Header("클릭 막고 싶은 UI 오브젝트들")]
    public List<GameObject> blockTargets = new List<GameObject>();

    // 내부에서 자동으로 CanvasGroup 관리
    private List<CanvasGroup> cachedGroups = new List<CanvasGroup>();

    private void Start()
    {
        quizLimitPanel.SetActive(false);
        okButton.onClick.AddListener(OnOkClicked);
    }

    public void ShowLimitPanel()
    {
        if (gamePanel != null && !gamePanel.activeSelf)
            gamePanel.SetActive(true);

        if (quizLimitPanel != null)
            quizLimitPanel.SetActive(true);

        // 🔹 blockTargets에 있는 GameObject 차단
        cachedGroups.Clear();
        foreach (var go in blockTargets)
        {
            if (go == null) continue;

            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();

            cg.interactable = false;
            cg.blocksRaycasts = false;
            cachedGroups.Add(cg);
        }
    }

    private void OnOkClicked()
    {
        SFXPlayer.Instance.PlayClick();
        quizLimitPanel.SetActive(false);
        gamePanel.SetActive(true);

        // 🔹 다시 원래대로 복구
        foreach (var cg in cachedGroups)
        {
            if (cg == null) continue;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        cachedGroups.Clear();
    }
}
