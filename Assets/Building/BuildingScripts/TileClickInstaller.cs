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

    // ====== 설정 ======
    [Header("Placement Settings")]
    [Tooltip("타일 점유를 표시할 마커 이름")]
    public string occupiedMarkerName = "__OCCUPIED__";
    [Tooltip("타일 크기에 맞출 때 살짝 줄이는 비율 (겹침/깜빡임 방지)")]
    [Range(0.85f, 1.0f)] public float footprintPadding = 0.98f;

    private GameObject selectedBuildingPrefab;
    private GameObject previewInstance;   // 회전 중심이 될 빈 오브젝트
    private GameObject modelInstance;     // 실제 건물 모델
    private float previewRotation = 0f;
    private List<GameObject> currentTiles;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(CloseWarningPanel);
        if (confirmInstallButton != null) confirmInstallButton.onClick.AddListener(ConfirmInstall);
        if (cancelInstallButton != null) cancelInstallButton.onClick.AddListener(CancelInstall);
        if (rotateButton != null) rotateButton.onClick.AddListener(RotatePreview);
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

                    BuildingData buildingData =
                        selectedBuildingPrefab.GetComponent<BuildingData>() ??
                        selectedBuildingPrefab.GetComponentInChildren<BuildingData>();
                    if (buildingData == null) return;

                    int width = buildingData.tileWidth;
                    int height = buildingData.tileHeight;

                    List<GameObject> tilesToUse = FindTilesAround(baseTile, width, height);
                    if (tilesToUse == null || tilesToUse.Count != width * height)
                    {
                        Debug.Log("설치할 수 있는 타일 공간이 부족합니다.");
                        return;
                    }

                    // ✅ childCount 대신 점유 마커로 판정
                    foreach (var tile in tilesToUse)
                    {
                        if (tile.transform.Find(occupiedMarkerName) != null)
                        {
                            Debug.Log("설치할 위치 중 일부에 이미 건물이 있습니다.");
                            return;
                        }
                    }

                    currentTiles = tilesToUse;

                    // 중심 계산
                    Vector3 center = Vector3.zero;
                    foreach (var tile in tilesToUse)
                        center += tile.GetComponent<Renderer>().bounds.center;
                    center /= tilesToUse.Count;

                    // 타일 1칸 사이즈와 총 크기
                    Vector3 tileSize = GetTileSize(baseTile);
                    Vector3 totalSize = new Vector3(
                        tileSize.x * width * footprintPadding,
                        tileSize.y,
                        tileSize.z * height * footprintPadding
                    );

                    // 프리뷰 부모
                    previewInstance = new GameObject("BuildingPreviewParent");

                    // 모델 생성
                    modelInstance = Instantiate(selectedBuildingPrefab, previewInstance.transform);
                    modelInstance.name = "BuildingModel";
                    modelInstance.SetActive(false);

                    // 모델 bounds (비활성 자식 포함 + 폴백)
                    if (!TryGetModelBounds(modelInstance, out Bounds modelBounds))
                    {
                        Debug.LogError("[Installer] 프리팹에서 Renderer/Collider/Mesh를 찾지 못했습니다.");
                        Destroy(previewInstance); previewInstance = null; modelInstance = null;
                        return;
                    }

                    // 스케일 맞추기 (XZ를 타일 풋프린트에 맞춤)
                    ResizeToFit(modelInstance, totalSize, modelBounds);

                    // 스케일 반영된 bounds 재계산
                    TryGetModelBounds(modelInstance, out modelBounds);

                    // 위치 계산
                    Vector3 offset = previewInstance.transform.position - modelBounds.center;
                    Vector3 spawnPos = center + offset;
                    spawnPos.y += tileSize.y / 2f + modelBounds.size.y / 2f;

                    if (selectedBuildingPrefab.name.ToLower().Contains("road"))
                        spawnPos.y += 0.01f;

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
        if (warningPanel != null) warningPanel.SetActive(false);
        SFXPlayer.Instance.PlayClick();
    }

    public void SetSelectedBuilding(GameObject prefab)
    {
        if (previewInstance != null) CancelInstall();

        selectedBuildingPrefab = prefab;
        Debug.Log($"선택된 건물: {prefab.name}");

        // 👉 선택 즉시 패널 켜기
        buildingInstallPanel.SetActive(true);

        // 버튼은 기본 켜두기 (설치 누르면 타일 검사에서 걸러짐)
        if (confirmInstallButton) confirmInstallButton.interactable = true;
        if (rotateButton) rotateButton.interactable = true;

        GameManager.Instance?.StartPlacing();
        SFXPlayer.Instance.PlayClick();
    }



    void RotatePreview()
    {
        SFXPlayer.Instance.PlayClick();
        if (previewInstance == null || modelInstance == null || currentTiles == null) return;

        float newRotation = (previewRotation + 90f) % 360f;

        GameObject baseTile = currentTiles[0];
        BuildingData buildingData = modelInstance.GetComponent<BuildingData>() ??
                                    modelInstance.GetComponentInChildren<BuildingData>();
        if (buildingData == null) return;

        Vector2Int size = GetRotatedSize(buildingData.tileWidth, buildingData.tileHeight, newRotation);

        List<GameObject> newTiles = FindTilesAround(baseTile, size.x, size.y);
        if (newTiles == null || newTiles.Count != size.x * size.y)
        {
            Debug.Log("회전 후 설치 가능한 타일이 부족합니다.");
            return;
        }

        foreach (GameObject tile in newTiles)
        {
            if (tile.transform.Find(occupiedMarkerName) != null)
            {
                Debug.Log("회전 후 설치 위치 중 일부에 건물이 있습니다.");
                return;
            }
        }

        previewRotation = newRotation;
        previewInstance.transform.rotation = Quaternion.Euler(0f, previewRotation, 0f);
        currentTiles = newTiles;

        // 위치 재계산
        Vector3 center = Vector3.zero;
        foreach (var tile in newTiles)
            center += tile.GetComponent<Renderer>().bounds.center;
        center /= newTiles.Count;

        if (!TryGetModelBounds(modelInstance, out Bounds modelBoundsAfter)) return;

        Vector3 offset = previewInstance.transform.position - modelBoundsAfter.center;
        Vector3 spawnPos = center + offset;
        spawnPos.y += GetTileSize(baseTile).y / 2f + modelBoundsAfter.size.y / 2f;

        previewInstance.transform.position = spawnPos;
    }

    void ConfirmInstall()
    {
        
        if (previewInstance == null || currentTiles == null) return;

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) return;

        BuildingData buildingData = modelInstance.GetComponent<BuildingData>() ??
                                    modelInstance.GetComponentInChildren<BuildingData>();
        if (buildingData == null) return;

        // ✅ 건물에 "Building" 태그 추가 (시민 이동 AI용)
        // 태그 설정 전에 안전하게 확인
        if (previewInstance.tag != "Building")
        {
            try
            {
                previewInstance.tag = "Building";
                Debug.Log($"건물에 'Building' 태그 설정 완료: {previewInstance.name}");
            }
            catch (UnityException e)
            {
                Debug.LogWarning($"[TileClickInstaller] 'Building' 태그 설정 실패: {e.Message}");
                Debug.LogWarning("Unity 에디터에서 Edit > Project Settings > Tags and Layers에서 'Building' 태그를 추가해주세요.");
            }
        }

        // ✅ 대표 타일 1개만 부모로 (여러 번 SetParent 하던 문제 제거)
        previewInstance.transform.SetParent(currentTiles[0].transform, true);

        // ✅ 모든 타일에 점유 마커 생성
        foreach (var tile in currentTiles)
        {
            if (tile.transform.Find(occupiedMarkerName) == null)
            {
                var occ = new GameObject(occupiedMarkerName);
                occ.transform.SetParent(tile.transform, false);
            }
        }
        // ✅ 추가: 설치된 "실제 건물"에 풋프린트 기록
        var footprint = buildingData.gameObject.AddComponent<BuildingFootprint>();
        footprint.Init(currentTiles, occupiedMarkerName);

        //  건물 정보를 GameManager에 추가 (GPT가 인식할 수 있도록)
        int totalCO2Impact = buildingData.instantCO2Change;

        // co2PerSecond가 0이 아닐 때만 추가 계산
        if (buildingData.co2PerSecond != 0)
        {
            totalCO2Impact += buildingData.maxCO2Change;
        }

        int incomePerMinute = 0;
        if (buildingData.incomePer5Minutes > 0)
        {
            incomePerMinute = buildingData.incomePer5Minutes / 5; // 5분당 -> 1분당 수입으로 변환
        }

        gameManager.AddBuilding(
            selectedBuildingPrefab.name.Replace("Prefab", ""), // 건물 이름
            buildingData.cost,
            totalCO2Impact, // 수정된 CO2 영향 계산
            incomePerMinute, // 수정된 수입 계산
            previewInstance.transform.position,
            previewInstance
        );

        // 비용/효과 적용 (기존 코드)
        gameManager.ApplyBuildingCost(
            buildingData.cost,
            buildingData.instantCO2Change,
            buildingData.co2PerSecond,
            buildingData.maxCO2Change,
            buildingData.incomePer5Minutes,
            buildingData.transform,
            buildingData.maxIncomeAmount
        );

        //퀘스트 자동 체크 알림
        YearQuestManager.Instance?.OnBuildingInstalled(selectedBuildingPrefab, buildingData);

        //시민들에게 새 건물 알림 (기존 시민들이 새 건물을 찾을 수 있도록)
        NotifyCitizensOfNewBuilding();

        //시민 컨트롤러에게 새 건물 설치 알림 (새 시민 생성을 위해)
        CitizenGroupController citizenController = FindObjectOfType<CitizenGroupController>();
        if (citizenController != null)
        {
            citizenController.OnBuildingInstalled(previewInstance.transform.position);
            Debug.Log($"[TileClickInstaller] 시민 컨트롤러에 새 건물 알림 전송: {previewInstance.transform.position}");
        }
        else
        {
            Debug.LogWarning("[TileClickInstaller] CitizenGroupController를 찾을 수 없습니다!");
        }

        // ✅ 설치 완료 → 설치중 해제 (CancelPlacing() 말고 CompletePlacing())
        GameManager.Instance?.CompletePlacing();

        ClearPreviewAndPanel();
        SFXPlayer.Instance.PlayClick();
    }

    // 새 건물 설치 시 기존 시민들에게 알림
    void NotifyCitizensOfNewBuilding()
    {
        CitizenWanderer[] allCitizens = FindObjectsOfType<CitizenWanderer>();
        foreach (var citizen in allCitizens)
        {
            // 시민의 OnNewBuildingInstalled 메서드 호출 (다음에 추가할 예정)
            citizen.OnNewBuildingInstalled();
        }
    }

    void CancelInstall()
    {
        if (previewInstance != null) Destroy(previewInstance);
        // ✅ 설치 취소 → 설치중 해제
        GameManager.Instance?.CancelPlacing();
        SFXPlayer.Instance.PlayClick();
        ClearPreviewAndPanel();
    }

    void ClearPreviewAndPanel()
    {
        previewInstance = null;
        modelInstance = null;
        selectedBuildingPrefab = null; // ✅ 이 라인을 제거하여 선택된 건물 정보 유지
        currentTiles = null;
        buildingInstallPanel.SetActive(false);
    }

    public void ClearSelection()
    {
        // 선택만 해제(프리뷰/패널 정리)
        if (previewInstance != null) Destroy(previewInstance);
        selectedBuildingPrefab = null;
        ClearPreviewAndPanel();
    }

    Vector2Int GetRotatedSize(int width, int height, float rotation)
    {
        if ((Mathf.RoundToInt(rotation) % 180) != 0)
            return new Vector2Int(height, width); // 90/270
        else
            return new Vector2Int(width, height); // 0/180
    }

    Vector3 GetTileSize(GameObject tile)
    {
        Renderer renderer = tile.GetComponent<Renderer>();
        if (renderer != null) return renderer.bounds.size;

        Collider collider = tile.GetComponent<Collider>();
        if (collider != null) return collider.bounds.size;

        return Vector3.one;
    }

    // ====== 크기/바운즈 유틸 ======
    void ResizeToFit(GameObject building, Vector3 targetSize, Bounds currentBounds)
    {
        Vector3 size = currentBounds.size;
        if (size.x <= 0f || size.z <= 0f) return;

        Vector3 scaleFactor = new Vector3(
            targetSize.x / size.x,
            targetSize.y > 0f ? targetSize.y / size.y : 1f,
            targetSize.z / size.z
        );

        float minFactor = Mathf.Min(scaleFactor.x, scaleFactor.z);
        building.transform.localScale *= minFactor;
    }

    bool TryGetModelBounds(GameObject go, out Bounds bounds)
    {
        // 1) Renderer(비활성 포함)
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        // 2) Collider 폴백
        var colliders = go.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            return true;
        }

        // 3) MeshFilter 폴백
        var mfs = go.GetComponentsInChildren<MeshFilter>(true);
        if (mfs != null && mfs.Length > 0)
        {
            Bounds b = TransformMeshBounds(mfs[0]);
            for (int i = 1; i < mfs.Length; i++) b.Encapsulate(TransformMeshBounds(mfs[i]));
            bounds = b;
            return true;
        }

        bounds = new Bounds(go.transform.position, Vector3.zero);
        return false;

        Bounds TransformMeshBounds(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            var local = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            Vector3 min = mf.transform.TransformPoint(local.min);
            Vector3 max = mf.transform.TransformPoint(local.max);
            Bounds wb = new Bounds(min, Vector3.zero);
            wb.Encapsulate(max);
            return wb;
        }
    }

    // ====== 타일 찾기 ======
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

                if (closest == null) return null; // 하나라도 못 찾으면 실패
                result.Add(closest);
            }
        }

        return result;
    }
}