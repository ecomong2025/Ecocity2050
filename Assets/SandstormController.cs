using UnityEngine;
using System.Collections;

public class SandstormController : MonoBehaviour
{
    [Header("Assign Prefab (Project View)")]
    public GameObject sandstormPrefab;    // 모래바람 프리팹 (Project 창에 있는 것)

    public YearQuestManager yearQuestManager;

    public float interval = 60f;          // 1분 간격
    public float duration = 10f;          // 모래바람 유지 시간

    private void Start()
    {
        if (yearQuestManager == null)
            yearQuestManager = YearQuestManager.Instance;

        StartCoroutine(SandstormRoutine());
    }

    IEnumerator SandstormRoutine()
    {
        while (true)
        {
            // 현재 연도 확인
            if (yearQuestManager != null && yearQuestManager.GetCurrentYear() <= 2040)
            {
                // 프리팹을 중앙(Vector3.zero)에 생성
                GameObject instance = Instantiate(sandstormPrefab, Vector3.zero, Quaternion.identity);

                // 10초 유지
                yield return new WaitForSeconds(duration);

                // 삭제
                Destroy(instance);

                // 1분 대기
                yield return new WaitForSeconds(interval);
            }
            else
            {
                // 2030년 초과하면 종료
                yield break;
            }
        }
    }
}
