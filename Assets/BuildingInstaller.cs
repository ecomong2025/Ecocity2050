using UnityEngine;

public class TileClickInstaller : MonoBehaviour
{
    public GameObject selectedBuildingPrefab;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Tile"))
                {
                    // 1. 타일 정보
                    Vector3 tileCenter = hit.collider.bounds.center;
                    float tileHeight = hit.collider.bounds.size.y;
                    Vector3 tileSize = GetTileSize(hit.collider.gameObject);

                    // 2. 건물 생성 (비활성)
                    GameObject building = Instantiate(selectedBuildingPrefab);
                    building.SetActive(false);

                    // 3. 크기 먼저 조정
                    ResizeToFit(building, tileSize);

                    // 4. 다시 렌더러로 크기/위치 계산 (스케일 적용 이후 기준!)
                    Renderer rend = building.GetComponentInChildren<Renderer>();
                    Vector3 buildingCenter = rend.bounds.center;
                    float buildingHeight = rend.bounds.size.y;

                    // 5. 오프셋 계산: 건물 중심 → 프리팹 중심까지 거리
                    Vector3 offset = building.transform.position - buildingCenter;

                    // 6. 최종 위치 계산 (타일 위 + 높이 보정)
                    Vector3 spawnPos = tileCenter + offset;
                    spawnPos.y += tileHeight / 2f + buildingHeight / 2f;

                    building.transform.position = spawnPos;
                    building.SetActive(true);
                }

            }
        }
    }

    Vector3 GetTileSize(GameObject tile)
    {
        Renderer renderer = tile.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.size;
        }

        Collider collider = tile.GetComponent<Collider>();
        if (collider != null)
        {
            return collider.bounds.size;
        }

        return Vector3.one; // 기본값
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

        // 건물 전체 비율을 유지하면서 가장 작은 축 기준으로 스케일 통일 (선택사항)
        float minFactor = Mathf.Min(scaleFactor.x, scaleFactor.z);
        building.transform.localScale *= minFactor;
    }

    public void SetSelectedBuilding(GameObject prefab)
    {
        selectedBuildingPrefab = prefab;
    }
}
