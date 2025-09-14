using UnityEngine;
using System.Collections;
public class DustManager : MonoBehaviour
{
    public GameObject dustPrefab;
    public Transform spawnPoint;
    public float interval = 60f;

    void Start()
    {
        if (spawnPoint == null && Camera.main != null)
            spawnPoint = Camera.main.transform;

        StartCoroutine(DustRoutine());
    }

    private IEnumerator DustRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);

            int currentYear = YearQuestManager.Instance.GetCurrentYear();
            if (currentYear <= 2030)
            {
                SpawnDust();
            }
        }
    }

    private void SpawnDust()
    {
        if (dustPrefab != null && spawnPoint != null)
        {
            GameObject obj = Instantiate(dustPrefab, spawnPoint.position, spawnPoint.rotation);
            // Particle System 자동으로 재생됨
        }
    }
}
