using TMPro;
using UnityEngine;

public class ResourceHUD : MonoBehaviour
{
    [Header("UI Refs (프리팹 내부 연결)")]
    [SerializeField] private TMP_Text budgetText;
    [SerializeField] private TMP_Text co2Text;
    [SerializeField] private TMP_Text satisfactionText;
    [SerializeField] private EmojiController emojiController; // 프리팹 안의 EmojiController 연결

    void OnEnable()
    {
        GameManager.OnHUDChanged += Apply;
        // 씬에 GameManager가 이미 켜져 있으면 현재 상태 한 번 그리기
        if (GameManager.Instance != null)
        {
            Apply(new HUDState {
                budget = GameManager.Instance.budget,
                co2 = GameManager.Instance.co2,
                satisfaction = GameManager.Instance.GetSatisfactionLevel()
            });
        }
    }

    void OnDisable()
    {
        GameManager.OnHUDChanged -= Apply;
    }

    private void Apply(HUDState s)
    {
        if (budgetText)       budgetText.text = $"{s.budget}";
        if (co2Text)          co2Text.text    = $"{s.co2}";
        if (satisfactionText) satisfactionText.text = s.satisfaction;
        if (emojiController)  emojiController.ShowEmoji(s.satisfaction);
    }
}