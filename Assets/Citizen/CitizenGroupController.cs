using UnityEngine;
using System.Collections;

public class CitizenGroupController : MonoBehaviour
{
    [Header("Citizen Settings")]
    public GameObject[] Citizens;

    [Header("Gradual Spawn Settings")]
    public float citizenSpawnInterval = 300f; // 시민 추가 간격 (초)
    public float minSatisfactionForGrowth = 0.6f; // 성장을 위한 최소 만족도
    public float maxSatisfactionDecayDelay = 120f; // 만족도 하락시 시민 감소 지연

    [Header("Building Spawn Settings")]
    public float buildingSpawnDelay = 3f; // 건물 설치 후 시민 생성 지연시간 (초)

    private int currentVisibleCount = -1;
    private int targetVisibleCount = 1;
    private float lastUpdateTime = 0f;
    private float lastSatisfactionChangeTime = 0f;
    private bool isGraduallyChanging = false;

    void Start()
    {
        Debug.Log($"[CitizenGroupController] Start 호출됨");
        Debug.Log($"[CitizenGroupController] citizens 배열 길이: {(Citizens != null ? Citizens.Length : 0)}");

        if (Citizens == null)
        {
            Debug.LogError("[CitizenGroupController] citizens 배열이 null입니다!");
            return;
        }

        if (Citizens.Length == 0)
        {
            Debug.LogWarning("[CitizenGroupController] citizens 배열이 비어있습니다!");
            return;
        }

        // 처음에는 1명으로 시작
        currentVisibleCount = 1;
        targetVisibleCount = 1;
        lastUpdateTime = Time.time;

        Debug.Log($"[CitizenGroupController] 초기 시민 수 설정: {currentVisibleCount}");

        UpdateCitizenVisibility();
    }

    void Update()
    {
        // 점진적 변화가 필요하고 충분한 시간이 지났으면 시민 수 조정
        if (isGraduallyChanging && Time.time - lastUpdateTime >= citizenSpawnInterval)
        {
            GraduallyChangeCitizenCount();
        }
    }

    // 건물이 설치되었을 때 호출하는 메소드
    public void OnBuildingInstalled(Vector3 buildingPosition)
    {
        StartCoroutine(SpawnCitizenAtBuilding(buildingPosition));
    }

    // 3초 후에 건물 위치에 시민 생성
    private IEnumerator SpawnCitizenAtBuilding(Vector3 buildingPosition)
    {
        yield return new WaitForSeconds(buildingSpawnDelay);

        // 비활성화된 시민 중 첫 번째를 찾아서 해당 위치에 활성화
        for (int i = currentVisibleCount; i < Citizens.Length; i++)
        {
            if (Citizens[i] != null && !Citizens[i].activeInHierarchy)
            {
                // 시민을 건물 위치로 이동
                Citizens[i].transform.position = buildingPosition + Vector3.up * 0.5f; // 건물 위에 약간 띄워서 배치
                Citizens[i].SetActive(true);

                currentVisibleCount++;
                targetVisibleCount = currentVisibleCount; // 목표치도 업데이트

                // CitizenWanderer에게 새 건물 알림
                CitizenWanderer wanderer = Citizens[i].GetComponent<CitizenWanderer>();
                if (wanderer != null)
                {
                    wanderer.OnNewBuildingInstalled();
                }

                Debug.Log($"[CitizenGroupController] 건물 위치에 시민 {i} 생성! 위치: {buildingPosition}");
                break;
            }
        }
    }

    public void UpdateSatisfaction(float satisfaction)
    {
        if (Citizens == null || Citizens.Length == 0) return;

        // 새로운 목표 시민 수 계산
        int newTargetCount;

        if (satisfaction >= 0.9f) // 매우 높음
        {
            newTargetCount = Citizens.Length; // 최대
        }
        else if (satisfaction >= 0.7f) // 높음
        {
            newTargetCount = Mathf.RoundToInt(Citizens.Length * 0.8f);
        }
        else if (satisfaction >= 0.5f) // 보통 
        {
            newTargetCount = Mathf.RoundToInt(Citizens.Length * 0.5f);
        }
        else if (satisfaction >= 0.3f) // 낮음
        {
            newTargetCount = Mathf.RoundToInt(Citizens.Length * 0.3f);
        }
        else // 매우 낮음
        {
            newTargetCount = 1; // 최소 1명은 유지
        }

        newTargetCount = Mathf.Clamp(newTargetCount, 1, Citizens.Length);

        Debug.Log($"[CitizenGroupController] 만족도: {satisfaction:F2} -> 목표 시민 수: {newTargetCount} (현재: {currentVisibleCount})");

        if (newTargetCount != targetVisibleCount)
        {
            targetVisibleCount = newTargetCount;
            lastSatisfactionChangeTime = Time.time;

            // 증가하는 경우와 감소하는 경우 구분
            if (targetVisibleCount > currentVisibleCount)
            {
                // 만족도가 충분히 높을 때만 즉시 점진적 증가 시작
                if (satisfaction >= minSatisfactionForGrowth)
                {
                    isGraduallyChanging = true;
                    lastUpdateTime = Time.time; // 즉시 시작할 수 있도록
                }
            }
            else if (targetVisibleCount < currentVisibleCount)
            {
                // 감소하는 경우 약간의 지연 후 시작
                isGraduallyChanging = true;
                lastUpdateTime = Time.time + maxSatisfactionDecayDelay;
            }
        }
    }

    void GraduallyChangeCitizenCount()
    {
        if (currentVisibleCount == targetVisibleCount)
        {
            isGraduallyChanging = false;
            return;
        }

        if (targetVisibleCount > currentVisibleCount)
        {
            // 시민 수 증가 (랜덤 위치에 생성)
            currentVisibleCount = Mathf.Min(currentVisibleCount + 1, targetVisibleCount);
            Debug.Log($"[CitizenGroupController] 시민 추가! 현재: {currentVisibleCount}/{Citizens.Length}");
        }
        else
        {
            // 시민 수 감소
            currentVisibleCount = Mathf.Max(currentVisibleCount - 1, targetVisibleCount);
            Debug.Log($"[CitizenGroupController] 시민 감소. 현재: {currentVisibleCount}/{Citizens.Length}");
        }

        UpdateCitizenVisibility();
        lastUpdateTime = Time.time;

        // 목표에 도달했으면 점진적 변화 중단
        if (currentVisibleCount == targetVisibleCount)
        {
            isGraduallyChanging = false;
            Debug.Log($"[CitizenGroupController] 목표 시민 수 도달: {currentVisibleCount}");
        }
    }

    void UpdateCitizenVisibility()
    {
        for (int i = 0; i < Citizens.Length; i++)
        {
            if (Citizens[i] != null)
            {
                bool shouldActivate = i < currentVisibleCount;

                // 새로 활성화되는 시민에게 알림
                if (shouldActivate && !Citizens[i].activeInHierarchy)
                {
                    Citizens[i].SetActive(true);

                    // 새 건물이 생겼을 때 알림 (CitizenWanderer가 있다면)
                    CitizenWanderer wanderer = Citizens[i].GetComponent<CitizenWanderer>();
                    if (wanderer != null)
                    {
                        wanderer.OnNewBuildingInstalled();
                    }

                    Debug.Log($"[CitizenGroupController] 시민 {i} ({Citizens[i].name}) 활성화");
                }
                else if (!shouldActivate && Citizens[i].activeInHierarchy)
                {
                    Citizens[i].SetActive(false);
                    Debug.Log($"[CitizenGroupController] 시민 {i} ({Citizens[i].name}) 비활성화");
                }
            }
            else
            {
                Debug.LogWarning($"[CitizenGroupController] citizens[{i}]이 null입니다!");
            }
        }
    }

    // 현재 활성화된 시민 수 반환
    public int GetActiveCitizenCount()
    {
        return currentVisibleCount;
    }

    // 목표 시민 수 반환
    public int GetTargetCitizenCount()
    {
        return targetVisibleCount;
    }

    // 즉시 모든 시민 활성화 (디버그용)
    [ContextMenu("Activate All Citizens")]
    public void ActivateAllCitizens()
    {
        targetVisibleCount = Citizens.Length;
        currentVisibleCount = Citizens.Length;
        isGraduallyChanging = false;
        UpdateCitizenVisibility();
    }

    // 시민 수를 1명으로 리셋 (디버그용)
    [ContextMenu("Reset to One Citizen")]
    public void ResetToOneCitizen()
    {
        targetVisibleCount = 1;
        currentVisibleCount = 1;
        isGraduallyChanging = false;
        UpdateCitizenVisibility();
    }
}