using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int budget = 400;
    public int co2 = 0;

    public TMP_Text budgetText;
    public TMP_Text co2Text;
    public TMP_Text satisfactionText;

    void Start()
    {
        UpdateUI();
    }

    public void ApplyBuildingCost(int cost, int co2Increase, int ignoredSatisfactionChange = 0)
    {
        budget -= cost;
        co2 += co2Increase;

        UpdateUI();
    }

    void UpdateUI()
    {
        budgetText.text = $"{budget}";
        co2Text.text = $"{co2}";
        satisfactionText.text = GetSatisfactionLevel();
    }

    string GetSatisfactionLevel()
    {
        if (co2 < 200)
            return "매우 좋음";
        else if (co2 < 400)
            return "좋음";
        else if (co2 < 700)
            return "보통";
        else if (co2 < 900)
            return "나쁨";
        else
            return "매우 나쁨";
    }
}