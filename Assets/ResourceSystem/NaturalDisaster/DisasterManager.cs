using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DisasterManager : MonoBehaviour
{
    [Header("SFX")]
    [SerializeField] private AudioClip collapseSfx;
    [SerializeField, Range(0f, 1f)] private float collapseVolume = 1f;

    public float normalDisasterInterval = 300f;  // 나쁨 → 5분
    public float severeDisasterInterval = 180f;  // 매우 나쁨 → 3분

    private GameManager gameManager;

    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
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
            BuildingData buildingData = FindBuildingDataInChildren(tile.transform);
            if (buildingData != null)
            {
                tilesWithBuildings.Add(buildingData.gameObject);
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

        // 재난 발생 시 수입 코루틴 중지
        GameManager.Instance.StopIncomeForBuilding(buildingToDestroy.transform);

        // 뉴스 패널에 재난 뉴스 출력
        GPTNewsGenerator.Instance.ShowDisasterNews(selectedDisaster);

        // ✅ 효과음 재생
        PlayCollapseSfx();

        StartCoroutine(BlinkAndDestroy(buildingToDestroy, 2f, 6));
    }

    IEnumerator BlinkAndDestroy(GameObject building, float duration, int blinkCount)
    {
        Renderer[] renderers = building.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < blinkCount; i++)
        {
            foreach (Renderer r in renderers)
                r.enabled = false;

            yield return new WaitForSeconds(duration / (blinkCount * 2));

            foreach (Renderer r in renderers)
                r.enabled = true;

            yield return new WaitForSeconds(duration / (blinkCount * 2));
        }

        Destroy(building);
    }

    void PlayCollapseSfx()
    {
        if (collapseSfx == null) return;

        var sfxPlayer = GameObject.Find("SFXPlayer");
        if (sfxPlayer != null)
        {
            var src = sfxPlayer.GetComponent<AudioSource>();
            if (src != null) src.PlayOneShot(collapseSfx, collapseVolume);
        }
        else
        {
            // 폴백: 카메라 위치에서 재생
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(collapseSfx, cam ? cam.transform.position : Vector3.zero, collapseVolume);
        }
    }

    BuildingData FindBuildingDataInChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            BuildingData data = child.GetComponent<BuildingData>();
            if (data != null)
                return data;

            // 자식의 자식도 검사
            BuildingData nested = FindBuildingDataInChildren(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}