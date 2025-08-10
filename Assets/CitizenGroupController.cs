using UnityEngine;
using System.Collections.Generic;

public class CitizenGroupController : MonoBehaviour
{
    public GameObject[] citizens;
    private int currentVisibleCount = -1;

    // 빌딩 위치 추적을 위한 리스트
    private List<Vector3> buildingPositions = new List<Vector3>();

    void Start()
    {
        // 처음 시민 수를 2명으로 설정
        currentVisibleCount = 2;

        for (int i = 0; i < citizens.Length; i++)
        {
            citizens[i].SetActive(i < currentVisibleCount);
        }
    }

    public void UpdateSatisfaction(float satisfaction)
    {
        int targetVisibleCount = Mathf.Clamp(Mathf.RoundToInt(satisfaction * (citizens.Length - 1)) + 1, 1, citizens.Length);

        if (targetVisibleCount != currentVisibleCount)
        {
            currentVisibleCount = targetVisibleCount;

            for (int i = 0; i < citizens.Length; i++)
            {
                citizens[i].SetActive(i < currentVisibleCount);

                // 새로 활성화되는 시민의 경우 안전한 위치에 배치
                if (i < currentVisibleCount && !citizens[i].activeSelf)
                {
                    PlaceCitizenSafely(citizens[i]);
                }
            }
        }
    }

    // 첫 번째 빌딩 설치 시 시민 1명 추가
    public void AddCitizen(int count)
    {
        int newCount = Mathf.Min(currentVisibleCount + count, citizens.Length);

        for (int i = currentVisibleCount; i < newCount; i++)
        {
            citizens[i].SetActive(true);
            PlaceCitizenSafely(citizens[i]);
        }

        currentVisibleCount = newCount;
    }

    // 빌딩 설치 후 만족도에 따른 시민 수 업데이트
    public void UpdateCitizensByBuilding()
    {
        float satisfaction = GameManager.Instance.GetSatisfactionValue();

        // 만족도가 높을수록 시민이 늘어날 확률 증가
        if (satisfaction >= 0.8f && Random.Range(0f, 1f) < 0.7f) // 70% 확률
        {
            AddCitizen(1);
        }
        else if (satisfaction >= 0.5f && Random.Range(0f, 1f) < 0.4f) // 40% 확률
        {
            AddCitizen(1);
        }
        else if (satisfaction >= 0.3f && Random.Range(0f, 1f) < 0.2f) // 20% 확률
        {
            AddCitizen(1);
        }
    }

    // 빌딩 설치 시 해당 위치의 시민들을 다른 곳으로 이동
    public void MoveCitizensAwayFromBuilding(Vector3 buildingPosition)
    {
        buildingPositions.Add(buildingPosition);

        float moveRadius = 3f; // 빌딩으로부터 이동시킬 반경

        for (int i = 0; i < currentVisibleCount; i++)
        {
            if (citizens[i].activeInHierarchy)
            {
                float distance = Vector3.Distance(citizens[i].transform.position, buildingPosition);
                if (distance < moveRadius)
                {
                    // 시민을 안전한 위치로 이동
                    PlaceCitizenSafely(citizens[i]);
                }
            }
        }
    }

    // 시민을 안전한 위치에 배치 (빌딩과 겹치지 않는 곳)
    private void PlaceCitizenSafely(GameObject citizen)
    {
        Vector3 safePosition;
        int attempts = 0;
        int maxAttempts = 20;

        do
        {
            // 공원 영역 내에서 랜덤 위치 생성 (더 넓은 범위)
            safePosition = new Vector3(
                Random.Range(-8f, 8f),  // X 범위 확장
                citizen.transform.position.y,
                Random.Range(-1.8f, 1.8f)  // Z 범위는 기존과 동일
            );
            attempts++;
        }
        while (IsPositionNearBuilding(safePosition) && attempts < maxAttempts);

        citizen.transform.position = safePosition;

        // CitizenWanderer 컴포넌트가 있다면 새 위치에서 다시 시작하도록 설정
        CitizenWanderer wanderer = citizen.GetComponent<CitizenWanderer>();
        if (wanderer != null)
        {
            wanderer.ResetWandering();
        }
    }

    // 특정 위치가 빌딩 근처인지 확인
    private bool IsPositionNearBuilding(Vector3 position)
    {
        float minDistance = 2.5f; // 빌딩으로부터 최소 거리

        foreach (Vector3 buildingPos in buildingPositions)
        {
            if (Vector3.Distance(position, buildingPos) < minDistance)
            {
                return true;
            }
        }

        return false;
    }

    // 빌딩이 파괴될 때 호출할 메서드 (필요시)
    public void RemoveBuildingPosition(Vector3 buildingPosition)
    {
        buildingPositions.Remove(buildingPosition);
    }
}