using UnityEngine;
using System.Collections.Generic;

public class TileClickInstaller : MonoBehaviour
{
    public static TileClickInstaller Instance;

    private GameObject selectedBuildingPrefab;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("클릭됨");

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit debugHit))
            {
                Debug.Log("Raycast hit: " + debugHit.collider.name);
            }
            else
            {
                Debug.Log("Raycast 실패");
            }
        }
        if (selectedBuildingPrefab == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {

                if (hit.collider.CompareTag("Tile"))
                {
                    GameObject baseTile = hit.collider.gameObject;

                    // ✅ 설치 크기: TileData에서 가져옴
                    BuildingData buildingData = selectedBuildingPrefab.GetComponent<BuildingData>();
                    if (buildingData == null) return;

                    int width = buildingData.tileWidth;
                    int height = buildingData.tileHeight;


                    List<GameObject> tilesToUse = FindTilesAround(baseTile, width, height);
                    if (tilesToUse == null || tilesToUse.Count != width * height)
                    {
                        Debug.Log("설치할 수 있는 타일 공간이 부족합니다.");
                        return;
                    }

                    foreach (var tile in tilesToUse)
                    {
                        if (tile.transform.childCount > 0)
                        {
                            Debug.Log("설치할 위치 중 일부에 이미 건물이 있습니다.");
                            return;
                        }
                    }

                    BuildingData data = selectedBuildingPrefab.GetComponent<BuildingData>();
                    if (data == null) return;

                    GameManager gameManager = FindObjectOfType<GameManager>();
                    if (gameManager == null) return;

                    if (gameManager.budget < data.cost)
                    {
                        Debug.Log("예산 부족");
                        return;
                    }

                    // ✅ 중심 위치 계산
                    Vector3 center = Vector3.zero;
                    foreach (var tile in tilesToUse)
                        center += tile.GetComponent<Renderer>().bounds.center;
                    center /= tilesToUse.Count;

                    Vector3 tileSize = GetTileSize(baseTile);
                    Vector3 totalSize = new Vector3(
                        tileSize.x * width,
                        tileSize.y,
                        tileSize.z * height
                    );

                    GameObject building = Instantiate(selectedBuildingPrefab);
                    building.SetActive(false);

                    ResizeToFit(building, totalSize);

                    Renderer rend = building.GetComponentInChildren<Renderer>();
                    Vector3 offset = building.transform.position - rend.bounds.center;
                    Vector3 spawnPos = center + offset;
                    spawnPos.y += tileSize.y / 2f + rend.bounds.size.y / 2f;

                    building.transform.position = spawnPos;
                    building.transform.SetParent(baseTile.transform);
                    building.SetActive(true);

                    gameManager.ApplyBuildingCost(
                        data.cost,
                        data.instantCO2Change,
                        data.co2PerSecond,
                        data.maxCO2Change
                    );

                    selectedBuildingPrefab = null;
                }
            }
        }
    }

    Vector3 GetTileSize(GameObject tile)
    {
        Renderer renderer = tile.GetComponent<Renderer>();
        if (renderer != null) return renderer.bounds.size;

        Collider collider = tile.GetComponent<Collider>();
        if (collider != null) return collider.bounds.size;

        return Vector3.one;
    }

    void ResizeToFit(GameObject building, Vector3 targetSize)
    {
        Renderer buildingRenderer = building.GetComponentInChildren<Renderer>();
        if (buildingRenderer == null) return;

        Vector3 buildingSize = buildingRenderer.bounds.size;

        Vector3 scaleFactor = new Vector3(
            targetSize.x / buildingSize.x,
            targetSize.y / buildingSize.y,
            targetSize.z / buildingSize.z
        );

        float minFactor = Mathf.Min(scaleFactor.x, scaleFactor.z);
        building.transform.localScale *= minFactor;
    }

    // ✅ 주변 타일 찾기 (간단한 거리 기반)
    List<GameObject> FindTilesAround(GameObject baseTile, int width, int height)
    {
        List<GameObject> result = new List<GameObject>();
        Vector3 basePos = baseTile.transform.position;
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");

        float tileSize = GetTileSize(baseTile).x;
        float tolerance = tileSize * 0.5f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 targetPos = basePos + new Vector3(x * tileSize, 0, z * tileSize);

                GameObject closest = null;
                float minDist = float.MaxValue;

                foreach (GameObject tile in allTiles)
                {
                    float dist = Vector3.Distance(tile.transform.position, targetPos);
                    if (dist < tolerance && dist < minDist)
                    {
                        closest = tile;
                        minDist = dist;
                    }
                }

                if (closest != null) result.Add(closest);
            }
        }

        return result;
    }

    public void SetSelectedBuilding(GameObject prefab)
    {
        selectedBuildingPrefab = prefab;
    }
}
