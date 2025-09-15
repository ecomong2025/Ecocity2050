using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public struct HUDState
{
    public int budget;
    public int co2;
    public string satisfaction;
}

[System.Serializable]
public class BuildingInfo
{
    public string buildingName;
    public int cost;
    public int co2Impact;
    public int incomePerMinute;
    public Vector3 position;
    public GameObject buildingObject;


    public BuildingInfo(string name, int buildCost, int co2, int income, Vector3 pos, GameObject obj)
    {
        buildingName = name;
        cost = buildCost;
        co2Impact = co2;
        incomePerMinute = income;
        position = pos;
        buildingObject = obj;
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // 변경: 이벤트를 클래스 내부로 이동
    public static event Action<HUDState> OnHUDChanged;
    public static event Action<string> OnSatisfactionChanged; // 전달값: 새로운 만족도 문자열

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
    public TalkBubbleController bubbleController;

    public QuizlimitController quizLimitController;

    // 건물별 수입 코루틴 관리용 딕셔너리
    private Dictionary<Transform, Coroutine> incomeCoroutines = new Dictionary<Transform, Coroutine>();

    // 건물 정보 추적용 리스트
    private List<BuildingInfo> builtBuildings = new List<BuildingInfo>();

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateUI();
    }

    public bool IsPlacingBuilding { get; private set; } // 건물 배치 모드 여부
    public void StartPlacing()  => IsPlacingBuilding = true;   // 선택 직후
    public void CompletePlacing()=> IsPlacingBuilding = false;  // 설치 완료
    public void CancelPlacing()
    {
        IsPlacingBuilding = false;
        TileClickInstaller.Instance?.ClearSelection(); // 선택 해제용(아래 함수 추가)
    }

    // 건물 정보를 추가하는 새로운 메서드
    public void AddBuilding(string buildingName, int cost, int co2Impact, int incomePerMinute, Vector3 position, GameObject buildingObject)
    {
        BuildingInfo newBuilding = new BuildingInfo(buildingName, cost, co2Impact, incomePerMinute, position, buildingObject);
        builtBuildings.Add(newBuilding);
        Debug.Log($"[건물 추가] {buildingName} - 비용: {cost}, CO2: {co2Impact}, 수입: {incomePerMinute}/분");
    }

    // 건물 제거 시 리스트에서도 제거
    public void RemoveBuilding(GameObject buildingObject)
    {
        BuildingInfo buildingToRemove = builtBuildings.Find(b => b.buildingObject == buildingObject);
        if (buildingToRemove != null)
        {
            builtBuildings.Remove(buildingToRemove);
            Debug.Log($"[건물 제거] {buildingToRemove.buildingName}");
        }
    }

    // 건물 정보를 문자열로 반환하는 메서드
    public string GetBuildingsInfo()
    {
        if (builtBuildings.Count == 0)
        {
            return "설치된 건물이 없습니다.";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("설치된 건물 현황:");

        // 건물 종류별로 개수 계산
        Dictionary<string, int> buildingCounts = new Dictionary<string, int>();
        Dictionary<string, int> totalCO2 = new Dictionary<string, int>();
        Dictionary<string, int> totalIncome = new Dictionary<string, int>();

        foreach (var building in builtBuildings)
        {
            if (building.buildingObject != null) // 건물이 파괴되지 않은 경우만
            {
                if (buildingCounts.ContainsKey(building.buildingName))
                {
                    buildingCounts[building.buildingName]++;
                    totalCO2[building.buildingName] += building.co2Impact;
                    totalIncome[building.buildingName] += building.incomePerMinute;
                }
                else
                {
                    buildingCounts[building.buildingName] = 1;
                    totalCO2[building.buildingName] = building.co2Impact;
                    totalIncome[building.buildingName] = building.incomePerMinute;
                }
            }
        }

        foreach (var kvp in buildingCounts)
        {
            string buildingName = kvp.Key;
            int count = kvp.Value;
            int co2 = totalCO2[buildingName];
            int income = totalIncome[buildingName];

            sb.AppendLine($"- {buildingName}: {count}개 (CO2: {co2}, 수입: {income}/분)");
        }

        return sb.ToString();
    }

    // 건물 분석 정보를 제공하는 메서드
    public string GetBuildingAnalysis()
    {
        if (builtBuildings.Count == 0)
        {
            return "건물 분석: 아직 설치된 건물이 없습니다.";
        }

        int totalIncomeBuildings = 0;
        int totalEnvironmentalBuildings = 0;
        int totalIncome = 0;
        int totalCO2FromBuildings = 0;

        foreach (var building in builtBuildings)
        {
            if (building.buildingObject != null)
            {
                if (building.incomePerMinute > 0)
                {
                    totalIncomeBuildings++;
                    totalIncome += building.incomePerMinute;
                }

                if (building.co2Impact < 0) // CO2 감소 효과가 있는 건물
                {
                    totalEnvironmentalBuildings++;
                }

                totalCO2FromBuildings += building.co2Impact;
            }
        }

        System.Text.StringBuilder analysis = new System.Text.StringBuilder();
        analysis.AppendLine("건물 분석:");
        analysis.AppendLine($"- 수익 건물: {totalIncomeBuildings}개 (총 수입: {totalIncome}/분)");
        analysis.AppendLine($"- 환경 건물: {totalEnvironmentalBuildings}개");
        analysis.AppendLine($"- 건물로 인한 총 CO2 영향: {totalCO2FromBuildings}");

        return analysis.ToString();
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

        if (bubbleController != null)
            bubbleController.ShowBubble(co2);

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

    // 건물 파괴 시 수입 코루틴 중지 및 리스트에서 제거
    public void StopIncomeForBuilding(Transform buildingTransform)
    {
        if (incomeCoroutines.ContainsKey(buildingTransform))
        {
            StopCoroutine(incomeCoroutines[buildingTransform]);
            incomeCoroutines.Remove(buildingTransform);
        }

        // 건물 리스트에서도 제거
        if (buildingTransform != null)
        {
            RemoveBuilding(buildingTransform.gameObject);
        }
    }

    IEnumerator IncreaseCO2OverTime(int perSecond, int maxAmount)
    {
        int accumulated = 0;
        while (accumulated < maxAmount)
        {
            yield return new WaitForSeconds(10f); // 10초 간격
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

            yield return new WaitForSeconds(30f); // 30초 간격

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
        if (bubbleController != null)
            bubbleController.ShowBubble(co2);

        // ✅ 프리팹들에 방송
        OnHUDChanged?.Invoke(new HUDState {
        budget = budget,
        co2 = co2,
        satisfaction = satisfaction
        });

        // 만족도 변경 지점
        OnSatisfactionChanged?.Invoke(satisfaction);
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
        SFXPlayer.Instance.PlayClick();

        // 1️⃣ 퀴즈 제한 체크
        if (!quizManager.CanPlayQuiz())
        {
            // 제한 초과 시 바로 패널 띄우기
            quizLimitController.ShowLimitPanel();
            return; // 함수 종료, 퀴즈 UI는 열리지 않음
        }

        // 2️⃣ 제한 미달이면 기존 로직 실행
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