using UnityEngine;

public class CitizenGroupController : MonoBehaviour
{
    public GameObject[] citizens; 
    private int currentVisibleCount = -1;

    void Start()
    {
        // Ã³À½ ½Ã¹Î 
        currentVisibleCount = 5;

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
            }
        }
    }
}
