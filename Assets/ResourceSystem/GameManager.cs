using TMPro;
using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public int budget = 400;
    public int co2 = 0;

    public TMP_Text budgetText;
    public TMP_Text co2Text;
    public TMP_Text satisfactionText;

    public CitizenGroupController citizenGroupController; // 연결

    void Start()
    {
        UpdateUI();
    }

    public void ApplyBuildingCost(int cost, int instantCo2Change, int co2PerSecond = 0, int maxCO2Change = 0)
    {
        budget -= cost;
        co2 += instantCo2Change;
        co2 = Mathf.Max(0, co2); // CO2는 음수가 되지 않도록

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
            yield return new WaitForSeconds(5f);
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

        // CitizenGroupController에 만족도 전달
        if (citizenGroupController != null)
        {
            citizenGroupController.UpdateSatisfaction(GetSatisfactionValue());
        }
    }

    string GetSatisfactionLevel()
    {
        if (co2 < 200) return "매우 좋음";
        else if (co2 < 400) return "좋음";
        else if (co2 < 700) return "보통";
        else if (co2 < 900) return "나쁨";
        else return "매우 나쁨";
    }

    // 0.1 ~ 1.0 사이 만족도 반환
    public float GetSatisfactionValue()
    {
        if (co2 < 200) return 1f;
        else if (co2 < 400) return 0.8f;
        else if (co2 < 700) return 0.5f;
        else if (co2 < 900) return 0.3f;
        else return 0.1f;
    }
}
