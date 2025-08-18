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

    public GameObject gamePanel; //게임 화면 전체
    public GameObject quizMainPanel; // 퀴즈 전체 UI

    public GameObject coinUIPrefab;
    public Canvas uiCanvas;

    public EmojiController emojiController;
    public CitizenGroupController citizenGroupController;

    public QuizManager quizManager;

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
            yield return new WaitForSeconds(1f); // 5초 간격
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
            if (buildingTransform == null) yield break;

            yield return new WaitForSeconds(5f); // 5초 간격 (5분이면 300f)

            if (buildingTransform == null) yield break;

            // 1) 모든 Renderer 통합 Bounds 계산
            Renderer[] rends = buildingTransform.GetComponentsInChildren<Renderer>();
            Bounds combined;
            if (rends != null && rends.Length > 0)
            {
                combined = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    combined.Encapsulate(rends[i].bounds);
            }
            else
            {
                // 렌더러가 없으면 대체 기준
                combined = new Bounds(buildingTransform.position, Vector3.one * 2f);
            }

            // 2) 옥상 바로 위를 기준점으로 사용
            Vector3 basePos = new Vector3(combined.center.x, combined.max.y, combined.center.z);

            // 3) 카메라 쪽으로 살짝 밀기 (가림 방지)
            Vector3 spawnPos = basePos + Vector3.up * 0.3f; // ✅ 옥상에서 살짝 띄우기
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = (spawnPos - cam.transform.position).normalized;
                spawnPos += dir * 0.3f;
            }

            // 4) 코인 생성/설정
            int remaining = maxIncome - accumulated;
            int income = Mathf.Min(amount, remaining);
            accumulated += income;

            GameObject coin = Instantiate(coinUIPrefab);
            coin.GetComponent<CoinUIController>().incomeAmount = income;
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

    //퀴즈 관련 버튼 연결 
    public void OpenQuiz()
    {
        gamePanel.SetActive(false);
        quizManager.ResetQuizUI();
        quizMainPanel.SetActive(true);
    }

    public void CloseQuiz()
    {
        gamePanel.SetActive(true);
        quizMainPanel.SetActive(false);

    }
}
