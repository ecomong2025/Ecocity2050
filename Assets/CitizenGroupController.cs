using UnityEngine;

public class CitizenGroupController : MonoBehaviour
{
    public GameObject[] citizens; // 인스펙터에서 시민 10개 연결
    private int currentVisibleCount = -1;

    public void UpdateSatisfaction(float satisfaction)
    {
        // 만족도(0~1)에 따라 시민 수 결정 (최소 1명 보장)
        int targetVisibleCount = Mathf.Clamp(Mathf.RoundToInt(satisfaction * (citizens.Length - 1)) + 1, 1, citizens.Length);

        if (targetVisibleCount != currentVisibleCount)
        {
            currentVisibleCount = targetVisibleCount;

            for (int i = 0; i < citizens.Length; i++)
            {
                citizens[i].SetActive(i < currentVisibleCount);
            }
        }
    }
}
