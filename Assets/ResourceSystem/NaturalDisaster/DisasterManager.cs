using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DisasterManager : MonoBehaviour
{
    public float normalDisasterInterval = 300f;  // 나쁨 → 5분 (300초)
    public float severeDisasterInterval = 180f;  // 매우 나쁨 → 3분 (180초)

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
                // 만족도가 괜찮으면 30초 간격으로 다시 검사
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

        int index = Random.Range(0, tilesWithBuildings.Count);
        GameObject buildingToDestroy = tilesWithBuildings[index];

        Destroy(buildingToDestroy);
        Debug.Log("🚨 재난 발생! 건물이 파괴됨 → " + buildingToDestroy.name);
    }
}