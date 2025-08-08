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

    // 건물별 수입 코루틴 관리용 딕셔너리
    private Dictionary<Transform, Coroutine> incomeCoroutines = new Dictionary<Transform, Coroutine>();

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

        // 수입 코루틴을 관리
        if (incomePer5Min > 0 && buildingTransform != null)
        {
            Coroutine c = StartCoroutine(GenerateIncomePeriodically(incomePer5Min, maxIncomeAmount, buildingTransform));
            incomeCoroutines[buildingTransform] = c;
        }

        UpdateUI();
    }

    // 건물 파괴 시 수입 코루틴 중지
    public void StopIncomeForBuilding(Transform buildingTransform)
    {
        if (incomeCoroutines.ContainsKey(buildingTransform))
        {
            StopCoroutine(incomeCoroutines[buildingTransform]);
            incomeCoroutines.Remove(buildingTransform);
        }
    }

    IEnumerator IncreaseCO2OverTime(int perSecond, int maxAmount)
    {
        int accumulated = 0;
        while (accumulated < maxAmount)
        {
            yield return new WaitForSeconds(300f);
            int delta = Mathf.Min(perSecond, maxAmount - accumulated);
            co2 += delta;
            accumulated += delta;
            UpdateUI();
        }
    }

    IEnumerator GenerateIncomePeriodically(int amount, int maxIncome, Transform buildingTransform)
    {
        int accumulated = 0;

<<<<<<< HEAD
    while (accumulated < maxIncome)
    {
        yield return new WaitForSeconds(300f); // 예산 발생 시간
=======
        while (accumulated < maxIncome)
        {
            if (buildingTransform == null)
                yield break;
>>>>>>> dev/merge

            yield return new WaitForSeconds(300f); // 5분 간격

            if (buildingTransform == null)
                yield break;

            Renderer rend = buildingTransform.GetComponentInChildren<Renderer>();
            if (rend == null) yield break;

            int remaining = maxIncome - accumulated;
            int income = Mathf.Min(amount, remaining);
            accumulated += income;

            GameObject coin = Instantiate(coinUIPrefab);
            coin.GetComponent<CoinUIController>().incomeAmount = income;

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

    public string GetSatisfactionLevel()
    {
        if (co2 < 200) return "매우 좋음";
        else if (co2 < 400) return "좋음";
        else if (co2 < 700) return "보통";
        else if (co2 < 900) return "나쁨";
        else return "매우 나쁨";
    }

    public float GetSatisfactionValue()
    {
        if (co2 < 200) return 1f;
        else if (co2 < 400) return 0.8f;
        else if (co2 < 700) return 0.5f;
        else if (co2 < 900) return 0.3f;
        else return 0.1f;
    }
}
