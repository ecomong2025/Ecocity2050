using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int budget = 600;
    public int co2 = 0;

    public TMP_Text budgetText;
    public TMP_Text co2Text;
    public TMP_Text satisfactionText;

    public GameObject coinUIPrefab;
    public Canvas uiCanvas;

    public EmojiController emojiController;
    public CitizenGroupController citizenGroupController;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateUI();
    }

    public void ApplyBuildingCost(
        int cost,
        int instantCo2Change,
        int co2PerSecond = 0,
        int maxCO2Change = 0,
        int incomePer5Min = 0,
        Transform buildingTransform = null,
        int maxIncomeAmount = 0)
    {
        budget -= cost;
        co2 += instantCo2Change;
        co2 = Mathf.Max(0, co2);

        if (co2PerSecond > 0 && maxCO2Change > 0)
            StartCoroutine(IncreaseCO2OverTime(co2PerSecond, maxCO2Change));

        if (incomePer5Min > 0 && buildingTransform != null)
            StartCoroutine(GenerateIncomePeriodically(incomePer5Min, maxIncomeAmount, buildingTransform));

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

    IEnumerator GenerateIncomePeriodically(int amount, int maxIncome, Transform buildingTransform)
    {
        int accumulated = 0;

        while (accumulated < maxIncome)
        {
            yield return new WaitForSeconds(300f); // 5분 간격

            int remaining = maxIncome - accumulated;
            int income = Mathf.Min(amount, remaining);
            accumulated += income;

            GameObject coin = Instantiate(coinUIPrefab);
            coin.GetComponent<CoinUIController>().incomeAmount = income;

            Renderer rend = buildingTransform.GetComponentInChildren<Renderer>();
            float height = rend.bounds.size.y;
            Vector3 spawnPos = rend.bounds.center + new Vector3(0, height / 2f + 2.8f, 0);

            Vector3 cameraDir = (spawnPos - Camera.main.transform.position).normalized;
            spawnPos += cameraDir * 0.3f;

            coin.GetComponent<CoinUIController>().SetWorldPosition(spawnPos);
        }
    }

    float GetBuildingHeight(Transform buildingTransform)
    {
        Renderer rend = buildingTransform.GetComponentInChildren<Renderer>();
        if (rend != null)
            return rend.bounds.size.y + 0.5f;

        return 3f;
    }

    public void AddBudget(int amount)
    {
        budget += amount;
        UpdateUI();
    }

    void UpdateUI()
    {
        budgetText.text = $"{budget}";
        co2Text.text = $"{co2}";

        string satisfaction = GetSatisfactionLevel();
        satisfactionText.text = satisfaction;

        if (emojiController != null)
        {
            emojiController.ShowEmoji(satisfaction);
        }

        if (citizenGroupController != null)
        {
            citizenGroupController.UpdateSatisfaction(GetSatisfactionValue());
        }
    }

    // 만족도 문자열 반환
    public string GetSatisfactionLevel()
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
