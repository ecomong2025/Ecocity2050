using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TileClickInstaller : MonoBehaviour
{
    public static TileClickInstaller Instance;

    [Header("UI Elements")]
    public GameObject warningPanel;
    public Button confirmButton;

    [Header("Building Install Panel")]
    public GameObject buildingInstallPanel;
    public Button confirmInstallButton;
    public Button cancelInstallButton;
    public Button rotateButton;

    private GameObject selectedBuildingPrefab;
    private GameObject previewInstance;       // 회전 중심이 될 빈 오브젝트
    private GameObject modelInstance;         // 실제 건물 모델
    private float previewRotation = 0f;
    private List<GameObject> currentTiles;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(CloseWarningPanel);

        if (confirmInstallButton != null)
            confirmInstallButton.onClick.AddListener(ConfirmInstall);

        if (cancelInstallButton != null)
            cancelInstallButton.onClick.AddListener(CancelInstall);

        if (rotateButton != null)
            rotateButton.onClick.AddListener(RotatePreview);

        buildingInstallPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && selectedBuildingPrefab != null && previewInstance == null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Tile"))
                {
                    GameObject baseTile = hit.collider.gameObject;
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

                    currentTiles = tilesToUse;

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

                    // 빈 부모 오브젝트 생성
                    previewInstance = new GameObject("BuildingPreviewParent");

                    // 실제 건물 모델을 자식으로 생성
                    modelInstance = Instantiate(selectedBuildingPrefab, previewInstance.transform);
                    modelInstance.name = "BuildingModel";
                    modelInstance.SetActive(false);

                    ResizeToFit(modelInstance, totalSize);

                    Renderer rend = modelInstance.GetComponentInChildren<Renderer>();
                    Vector3 offset = previewInstance.transform.position - rend.bounds.center;
                    Vector3 spawnPos = center + offset;
                    spawnPos.y += tileSize.y / 2f + rend.bounds.size.y / 2f;

                    previewInstance.transform.position = spawnPos;
                    modelInstance.SetActive(true);
                    previewRotation = 0f;

                    buildingInstallPanel.SetActive(true);
                }
            }
        }
    }

    public void CloseWarningPanel()
    {
        if (warningPanel != null)
            warningPanel.SetActive(false);
    }

    public void SetSelectedBuilding(GameObject prefab)
    {
        selectedBuildingPrefab = prefab;
    }

    void RotatePreview()
    {
        if (previewInstance == null || modelInstance == null || currentTiles == null) return;

        // 회전 각도 미리 계산
        float newRotation = (previewRotation + 90f) % 360f;

        // 현재 중심 타일 기준으로 다시 타일 탐색
        GameObject baseTile = currentTiles[0];
        BuildingData buildingData = modelInstance.GetComponent<BuildingData>();

        // 건물이 차지할 영역을 회전 상태에 따라 계산
        Vector2Int size = GetRotatedSize(buildingData.tileWidth, buildingData.tileHeight, newRotation);

        List<GameObject> newTiles = FindTilesAround(baseTile, size.x, size.y);
        if (newTiles == null || newTiles.Count != size.x * size.y)
        {
            Debug.Log("회전 후 설치 가능한 타일이 부족합니다.");
            return;
        }

        foreach (GameObject tile in newTiles)
        {
            if (tile.transform.childCount > 0)
            {
                Debug.Log("회전 후 설치 위치 중 일부에 건물이 있습니다.");
                return;
            }
        }

        // 회전 허용 → 적용
        previewRotation = newRotation;
        previewInstance.transform.rotation = Quaternion.Euler(0f, previewRotation, 0f);
        currentTiles = newTiles;

        // 위치 재계산
        Vector3 center = Vector3.zero;
        foreach (var tile in newTiles)
            center += tile.GetComponent<Renderer>().bounds.center;
        center /= newTiles.Count;

        Renderer rend = modelInstance.GetComponentInChildren<Renderer>();
        Vector3 offset = previewInstance.transform.position - rend.bounds.center;
        Vector3 spawnPos = center + offset;
        spawnPos.y += GetTileSize(baseTile).y / 2f + rend.bounds.size.y / 2f;

        previewInstance.transform.position = spawnPos;
    }


    void ConfirmInstall()
    {
        if (previewInstance == null || currentTiles == null) return;

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) return;

        BuildingData buildingData = modelInstance.GetComponent<BuildingData>();

        foreach (GameObject tile in currentTiles)
            previewInstance.transform.SetParent(tile.transform);

        gameManager.ApplyBuildingCost(
            buildingData.cost,
            buildingData.instantCO2Change,
            buildingData.co2PerSecond,
            buildingData.maxCO2Change,
            buildingData.incomePer5Minutes,
            previewInstance.transform,
            buildingData.maxIncomeAmount
        );

        ClearPreviewAndPanel();
    }

    void CancelInstall()
    {
        if (previewInstance != null)
            Destroy(previewInstance);

        ClearPreviewAndPanel();
    }

    void ClearPreviewAndPanel()
    {
        previewInstance = null;
        modelInstance = null;
        selectedBuildingPrefab = null;
        currentTiles = null;
        buildingInstallPanel.SetActive(false);
    }

    Vector2Int GetRotatedSize(int width, int height, float rotation)
    {
        if ((Mathf.RoundToInt(rotation) % 180) != 0)
            return new Vector2Int(height, width); // 90도나 270도
        else
            return new Vector2Int(width, height); // 0도나 180도
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
}
