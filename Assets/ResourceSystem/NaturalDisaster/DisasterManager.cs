using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DisasterManager : MonoBehaviour
{
    public float normalDisasterInterval = 300f;  // 나쁨 → 5분
    public float severeDisasterInterval = 180f;  // 매우 나쁨 → 3분

    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("GameManager를 찾을 수 없습니다.");
            return;
        }

        StartCoroutine(DisasterRoutine());
    }

    IEnumerator DisasterRoutine()
    {
        while (true)
        {
            string status = gameManager.GetSatisfactionLevel();

            if (status == "매우 나쁨")
            {
                yield return new WaitForSeconds(severeDisasterInterval);
                TriggerDisaster();
            }
            else if (status == "나쁨")
            {
                yield return new WaitForSeconds(normalDisasterInterval);
                TriggerDisaster();
            }
            else
            {
                yield return new WaitForSeconds(30f);
            }
        }
    }

    void TriggerDisaster()
    {
        GameObject[] tiles = GameObject.FindGameObjectsWithTag("Tile");
        List<GameObject> tilesWithBuildings = new List<GameObject>();

        foreach (GameObject tile in tiles)
        {
            if (tile.transform.childCount > 0)
            {
                foreach (Transform child in tile.transform)
                {
                    if (child.GetComponent<BuildingData>() != null)
                    {
                        tilesWithBuildings.Add(child.gameObject);
                        break;
                    }
                }
            }
        }

        if (tilesWithBuildings.Count == 0)
        {
            Debug.Log("재난으로 제거할 건물이 없습니다.");
            return;
        }

        string[] disasterTypes = { "가뭄", "화재", "폭우", "태풍" };
        string selectedDisaster = disasterTypes[Random.Range(0, disasterTypes.Length)];

        int index = Random.Range(0, tilesWithBuildings.Count);
        GameObject buildingToDestroy = tilesWithBuildings[index];

        Debug.Log($"🚨 {selectedDisaster} 발생! {buildingToDestroy.name} 건물이 파괴됩니다...");
        
        StartCoroutine(BlinkAndDestroy(buildingToDestroy, 2f, 6));
    }

    IEnumerator BlinkAndDestroy(GameObject building, float duration, int blinkCount)
    {
        Renderer[] renderers = building.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < blinkCount; i++)
        {
            // 껐다가
            foreach (Renderer r in renderers)
                r.enabled = false;

            yield return new WaitForSeconds(duration / (blinkCount * 2));

            // 켰다가
            foreach (Renderer r in renderers)
                r.enabled = true;

            yield return new WaitForSeconds(duration / (blinkCount * 2));
        }

        Destroy(building);
    }
}