using UnityEngine;

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
        if (selectedBuildingPrefab == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Tile"))
                {
                    GameObject tile = hit.collider.gameObject;

                    if (tile.transform.childCount > 0)
                    {
                        Debug.Log("건물이 이미 설치된 타일입니다.");
                        return;
                    }

                    BuildingData data = selectedBuildingPrefab.GetComponent<BuildingData>();
                    if (data != null)
                    {
                        GameManager gameManager = FindObjectOfType<GameManager>();
                        if (gameManager != null)
                        {
                            if (gameManager.budget < data.cost)
                            {
                                Debug.Log("예산이 부족하여 건물을 설치할 수 없습니다.");
                                return;
                            }

                            Vector3 tileCenter = hit.collider.bounds.center;
                            float tileHeight = hit.collider.bounds.size.y;
                            Vector3 tileSize = GetTileSize(tile);

                            GameObject building = Instantiate(selectedBuildingPrefab);
                            building.SetActive(false);

                            ResizeToFit(building, tileSize);

                            Renderer rend = building.GetComponentInChildren<Renderer>();
                            Vector3 buildingCenter = rend.bounds.center;
                            float buildingHeight = rend.bounds.size.y;

                            Vector3 offset = building.transform.position - buildingCenter;
                            Vector3 spawnPos = tileCenter + offset;
                            spawnPos.y += tileHeight / 2f + buildingHeight / 2f;

                            building.transform.SetParent(tile.transform);
                            building.transform.position = spawnPos;
                            building.SetActive(true);

                            gameManager.ApplyBuildingCost(
                                data.cost,
                                data.instantCO2Change,
                                data.co2PerSecond,
                                data.maxCO2Change
                            );
                        }
                    }

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

    public void SetSelectedBuilding(GameObject prefab)
    {
        selectedBuildingPrefab = prefab;
    }
}