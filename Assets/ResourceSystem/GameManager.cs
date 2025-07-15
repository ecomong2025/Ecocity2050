using TMPro;
using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public int budget = 600;
    public int co2 = 0;

    public TMP_Text budgetText;
    public TMP_Text co2Text;
    public TMP_Text satisfactionText;

    void Start()
    {
        UpdateUI();
    }

    public void ApplyBuildingCost(int cost, int instantCo2Change, int co2PerSecond = 0, int maxCO2Change = 0)
    {
        budget -= cost;
        co2 += instantCo2Change;
        co2 = Mathf.Max(0, co2); // CO2는 음수 불가

        // 점진적 CO₂ 증가 코루틴 시작
        if (co2PerSecond > 0 && maxCO2Change > 0)
        {
            StartCoroutine(IncreaseCO2OverTime(co2PerSecond, maxCO2Change));
        }

        UpdateUI();
    }

    IEnumerator IncreaseCO2OverTime(int perSecond, int maxAmount)
    {
        int accumulated = 0;
        while (accumulated < maxAmount)
        {
            yield return new WaitForSeconds(5f); // 5초마다
            int delta = Mathf.Min(perSecond, maxAmount - accumulated);
            co2 += delta;
            accumulated += delta;
            UpdateUI();
        }
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