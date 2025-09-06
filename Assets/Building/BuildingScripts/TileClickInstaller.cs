using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TileClickInstaller : MonoBehaviour
{
    // === Drag Select ===
    [Header("Drag Select")]
    public LayerMask tileLayerMask = ~0;      // Tile 레이어만 켜두면 좋아요
    public Material highlightMat;             // URP/Unlit Transparent(초록), 없으면 lineMat 써도 됨
    public Color highlightValid = new Color(0.2f, 1f, 0.4f, 0.35f);
    public Color highlightInvalid = new Color(1f, 0.25f, 0.25f, 0.35f);
    public float highlightYOffset = 0.01f;
    public Material lineMat;

    bool isDragging;
    GameObject dragStartTile;
    List<GameObject> highlightedTiles = new();
    List<GameObject> highlightQuads = new();

    static readonly int BaseColorID2 = Shader.PropertyToID("_BaseColor"); // 하이라이트용
    MaterialPropertyBlock _mpbHighlight;

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

        _mpbHighlight = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (selectedBuildingPrefab == null) return;

        // 드래그 시작
        if (Input.GetMouseButtonDown(0) && TryGetTileUnderMouse(out var tile))
        {
            isDragging = true;
            dragStartTile = tile;
            ClearHighlight();
        }

        // 드래그 중
        if (isDragging)
        {
            if (TryGetTileUnderMouse(out var tileB))
            {
                var bd = selectedBuildingPrefab.GetComponent<BuildingData>() ??
                         selectedBuildingPrefab.GetComponentInChildren<BuildingData>();
                if (bd == null) return;

                var size = GetRotatedSize(bd.tileWidth, bd.tileHeight, previewRotation);

                // 그리드 축을 자동 추정해서 직사각형 수집
                var rectTiles = FindTilesRectangleOnGrid(dragStartTile, size.x, size.y, tileB);

                bool valid = rectTiles != null &&
                             rectTiles.Count == size.x * size.y &&
                             AllTilesFree(rectTiles);

                HighlightTiles(rectTiles, valid);

                buildingInstallPanel.SetActive(true);
                if (confirmInstallButton) confirmInstallButton.interactable = valid;

                // 드래그 확정(마우스 업)
                if (Input.GetMouseButtonUp(0))
                {
                    isDragging = false;
                    if (valid)
                    {
                        currentTiles = rectTiles;
                        SpawnPreviewOverSelection(selectedBuildingPrefab);
                        ClearHighlight();
                    }
                    else ClearHighlight();
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                ClearHighlight();
            }
        }

        // 회전 단축키(optional)
        if (previewInstance != null && Input.GetKeyDown(KeyCode.R))
            RotatePreview();
    }

    // ─────────────────────────────────────────────────────────────
    // 실제 그리드 축(u, v)을 추정해 width×height 타일을 모은다.
    List<GameObject> FindTilesRectangleOnGrid(GameObject baseTile, int width, int height, GameObject dragTile)
    {
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        Vector3 basePos = baseTile.transform.position;

        // 1) 가까운 이웃 벡터 모으기
        var neigh = new List<Vector3>();
        foreach (var t in allTiles)
        {
            if (t == baseTile) continue;
            Vector3 v = t.transform.position - basePos; v.y = 0;
            if (v.sqrMagnitude > 0.0001f) neigh.Add(v);
        }
        if (neigh.Count == 0) return null;
        neigh.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));

        // 2) u축/stepU, v축/stepV
        Vector3 u = neigh[0].normalized;
        float stepU = Mathf.Sqrt(neigh[0].sqrMagnitude);

        Vector3 vAxis = Vector3.zero;
        float stepV = stepU;
        for (int i = 1; i < neigh.Count; i++)
        {
            var n = neigh[i].normalized;
            float parallel = Mathf.Abs(Vector3.Dot(n, u));
            if (parallel < 0.5f) { vAxis = n; stepV = Mathf.Sqrt(neigh[i].sqrMagnitude); break; }
        }
        if (vAxis == Vector3.zero)
        {
            vAxis = Vector3.ProjectOnPlane(baseTile.transform.forward, Vector3.up).normalized;
            if (vAxis.sqrMagnitude < 0.5f) vAxis = Vector3.forward;
        }

        // 3) 드래그 방향으로 축 부호 결정
        Vector3 dragDir = dragTile.transform.position - basePos; dragDir.y = 0;
        int signU = Vector3.Dot(dragDir, u) >= 0 ? 1 : -1;
        int signV = Vector3.Dot(dragDir, vAxis) >= 0 ? 1 : -1;

        float tolerance = 0.45f * Mathf.Min(stepU, stepV);

        // 4) 타일 채우기
        var result = new List<GameObject>(width * height);
        for (int iu = 0; iu < width; iu++)
        {
            for (int iv = 0; iv < height; iv++)
            {
                Vector3 target = basePos
                               + (signU * iu) * u * stepU
                               + (signV * iv) * vAxis * stepV;

                GameObject closest = null; float minDist = float.MaxValue;
                foreach (var t in allTiles)
                {
                    float d = Vector3.Distance(t.transform.position, target);
                    if (d < tolerance && d < minDist) { minDist = d; closest = t; }
                }
                if (!closest) return null;
                result.Add(closest);
            }
        }
        return result;
    }
    // ─────────────────────────────────────────────────────────────

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

        buildingInstallPanel.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;
        if (rotateButton) rotateButton.interactable = true;

        GameManager.Instance?.StartPlacing();
        SFXPlayer.Instance.PlayClick();
    }

    void RotatePreview()
    {
        SFXPlayer.Instance.PlayClick();
        previewRotation = (previewRotation + 90f) % 360f;

        if (previewInstance == null || modelInstance == null || currentTiles == null) return;

        // 회전 후엔 다시 현재 선택 영역을 기준으로 프리뷰만 회전
        previewInstance.transform.rotation = Quaternion.Euler(0f, previewRotation, 0f);
    }

    void SpawnPreviewOverSelection(GameObject prefab)
    {
        if (previewInstance != null) Destroy(previewInstance);
        modelInstance = null;

        // 1) 선택 타일들의 합쳐진 바운즈
        Bounds selB = currentTiles[0].GetComponent<Renderer>().bounds;
        for (int i = 1; i < currentTiles.Count; i++)
            selB.Encapsulate(currentTiles[i].GetComponent<Renderer>().bounds);

        Vector3 center = selB.center;
        Vector3 totalSize = new Vector3(
            selB.size.x * footprintPadding,
            selB.size.y,
            selB.size.z * footprintPadding
        );

        // 2) 부모/모델 생성
        previewInstance = new GameObject("BuildingPreviewParent");
        previewInstance.transform.SetPositionAndRotation(
            new Vector3(center.x, selB.max.y, center.z),          // 부모는 중심(XZ) + 타일 윗면 높이(Y)
            Quaternion.Euler(0f, previewRotation, 0f)
        );

        modelInstance = Instantiate(prefab, previewInstance.transform);
        modelInstance.name = "BuildingModel";
        modelInstance.SetActive(false);

        if (!TryGetModelBounds(modelInstance, out Bounds modelBounds))
        {
            Destroy(previewInstance); previewInstance = null; modelInstance = null;
            Debug.LogError("[Installer] 프리팹에서 렌더러/콜라이더/메시를 찾지 못했습니다.");
            return;
        }

        // 3) 절대 스케일로 발자국에 맞춤
        ResizeToFit(modelInstance, totalSize, modelBounds);

        // 4) 모델의 Bounds 중앙을 선택영역 중심(XZ)으로, 바닥을 타일 윗면(Y)으로 '정렬'
        AlignPreviewToSelection(selB);

        modelInstance.SetActive(true);
        buildingInstallPanel.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;
    }

    void AlignPreviewToSelection(Bounds selectionBounds)
    {
        // 현재(스케일/회전 반영)된 모델의 월드 바운즈
        if (!TryGetModelBounds(modelInstance, out Bounds b)) return;

        // XZ는 중심을 selection 중심에, Y는 바닥을 selection 윗면에 맞춘다
        Vector3 deltaWorld = new Vector3(
            selectionBounds.center.x - b.center.x,   // X
            selectionBounds.max.y - b.min.y,      // Y (바닥 맞대기)
            selectionBounds.center.z - b.center.z    // Z
        );

        modelInstance.transform.position += deltaWorld;
    }

    void ConfirmInstall()
    {
        if (previewInstance == null || currentTiles == null) return;

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) return;

        BuildingData buildingData = modelInstance.GetComponent<BuildingData>() ??
                                    modelInstance.GetComponentInChildren<BuildingData>();
        if (buildingData == null) return;

        // 태그 설정
        if (previewInstance.tag != "Building")
        {
            try { previewInstance.tag = "Building"; }
            catch (UnityException e)
            {
                Debug.LogWarning($"[TileClickInstaller] 'Building' 태그 설정 실패: {e.Message}");
            }
        }

        // 대표 타일 1개만 부모로
        previewInstance.transform.SetParent(currentTiles[0].transform, true);

        // 모든 타일에 점유 마커 생성
        foreach (var tile in currentTiles)
            if (tile.transform.Find(occupiedMarkerName) == null)
                new GameObject(occupiedMarkerName).transform.SetParent(tile.transform, false);

        // (선택) 설치된 오브젝트에 풋프린트 기록
        var footprint = previewInstance.AddComponent<BuildingFootprint>();
        footprint.Init(currentTiles, occupiedMarkerName);

        // 게임 매니저 반영 (프로젝트 로직에 맞게 유지)
        int totalCO2Impact = buildingData.instantCO2Change;
        if (buildingData.co2PerSecond != 0) totalCO2Impact += buildingData.maxCO2Change;

        int incomePerMinute = (buildingData.incomePer5Minutes > 0)
            ? buildingData.incomePer5Minutes / 5 : 0;

        gameManager.AddBuilding(
            selectedBuildingPrefab.name.Replace("Prefab", ""),
            buildingData.cost,
            totalCO2Impact,
            incomePerMinute,
            previewInstance.transform.position,
            previewInstance
        );

        gameManager.ApplyBuildingCost(
            buildingData.cost,
            buildingData.instantCO2Change,
            buildingData.co2PerSecond,
            buildingData.maxCO2Change,
            buildingData.incomePer5Minutes,
            buildingData.transform,
            buildingData.maxIncomeAmount
        );

        YearQuestManager.Instance?.OnBuildingInstalled(selectedBuildingPrefab, buildingData);
        NotifyCitizensOfNewBuilding();

        var citizenController = FindObjectOfType<CitizenGroupController>();
        citizenController?.OnBuildingInstalled(previewInstance.transform.position);

        GameManager.Instance?.CompletePlacing();
        ClearPreviewAndPanel();
        SFXPlayer.Instance.PlayClick();
    }

    void NotifyCitizensOfNewBuilding()
    {
        foreach (var c in FindObjectsOfType<CitizenWanderer>())
            c.OnNewBuildingInstalled();
    }

    void CancelInstall()
    {
        if (previewInstance != null) Destroy(previewInstance);
        GameManager.Instance?.CancelPlacing();
        SFXPlayer.Instance.PlayClick();
        ClearPreviewAndPanel();
    }

    void ClearPreviewAndPanel()
    {
        previewInstance = null;
        modelInstance = null;
        // selectedBuildingPrefab = null;  // 선택 유지
        currentTiles = null;
        buildingInstallPanel.SetActive(false);
    }

    public void ClearSelection()
    {
        if (previewInstance != null) Destroy(previewInstance);
        selectedBuildingPrefab = null;
        ClearPreviewAndPanel();
    }

    Vector2Int GetRotatedSize(int width, int height, float rotation)
    {
        return (Mathf.RoundToInt(rotation) % 180 != 0)
            ? new Vector2Int(height, width)   // 90/270
            : new Vector2Int(width, height);  // 0/180
    }

    Vector3 GetTileSize(GameObject tile)
    {
        var r = tile.GetComponent<Renderer>();
        if (r != null) return r.bounds.size;
        var c = tile.GetComponent<Collider>();
        if (c != null) return c.bounds.size;
        return Vector3.one;
    }

    // === 크기/바운즈 유틸 ===
    // 절대 스케일로 맞추도록 수정(누적 곱 방지)
    void ResizeToFit(GameObject building, Vector3 targetSize, Bounds _unused)
    {
        var t = building.transform;
        t.localScale = Vector3.one; // 리셋

        if (!TryGetModelBounds(building, out Bounds baseB)) return;

        Vector3 size = baseB.size;
        if (size.x <= 0f || size.z <= 0f) return;

        float s = Mathf.Min(targetSize.x / size.x, targetSize.z / size.z);
        t.localScale = new Vector3(s, s, s);
    }

    bool TryGetTileUnderMouse(out GameObject tile)
    {
        tile = null;
        var cam = Camera.main;
        if (!cam) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000f, tileLayerMask))
        {
            if (hit.collider.CompareTag("Tile"))
            {
                tile = hit.collider.gameObject;
                return true;
            }
        }
        return false;
    }

    bool AllTilesFree(List<GameObject> tiles)
    {
        foreach (var t in tiles)
            if (t.transform.Find(occupiedMarkerName) != null) return false;
        return true;
    }

    // (남겨둔 버전) 세계 X/Z 기준 직사각형 — 회전 그리드에서는 사용 비추
    List<GameObject> FindTilesRectangle(GameObject baseTile, int width, int height, int dirX, int dirZ)
    {
        if (dirX == 0 && dirZ == 0) dirX = 1;

        var result = new List<GameObject>(width * height);
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        float tileSize = GetTileSize(baseTile).x;
        float tolerance = tileSize * 0.5f;

        Vector3 origin = baseTile.transform.position;
        Vector3 u = (dirX != 0) ? new Vector3(dirX, 0, 0) : new Vector3(0, 0, dirZ);
        Vector3 v = (dirX != 0) ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);

        for (int iu = 0; iu < width; iu++)
            for (int iv = 0; iv < height; iv++)
            {
                Vector3 target = origin + (u * iu + v * iv) * tileSize;

                GameObject closest = null; float minDist = float.MaxValue;
                foreach (var t in allTiles)
                {
                    float d = Vector3.Distance(t.transform.position, target);
                    if (d < tolerance && d < minDist) { minDist = d; closest = t; }
                }
                if (!closest) return null;
                result.Add(closest);
            }
        return result;
    }

    // 하이라이트 표시/해제
    void HighlightTiles(List<GameObject> tiles, bool valid)
    {
        ClearHighlight();
        if (tiles == null) return;

        EnsureHighlightPool(tiles.Count);

        Color col = valid ? highlightValid : highlightInvalid;
        _mpbHighlight.Clear();
        _mpbHighlight.SetColor(BaseColorID2, col);

        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            var q = highlightQuads[i];
            var b = t.GetComponent<Renderer>().bounds;

            q.transform.position = new Vector3(b.center.x, b.center.y + highlightYOffset, b.center.z);
            q.transform.rotation = Quaternion.Euler(90, 0, 0);
            q.transform.localScale = new Vector3(b.size.x, b.size.z, 1);

            var mr = q.GetComponent<MeshRenderer>();
            mr.SetPropertyBlock(_mpbHighlight);
            q.SetActive(true);
            highlightedTiles.Add(t);
        }
    }

    void ClearHighlight()
    {
        foreach (var q in highlightQuads) if (q) q.SetActive(false);
        highlightedTiles.Clear();
    }

    void EnsureHighlightPool(int count)
    {
        while (highlightQuads.Count < count)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "TileHighlight";
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = highlightMat ? highlightMat : lineMat;
            q.SetActive(false);
            highlightQuads.Add(q);
        }
    }

    bool TryGetModelBounds(GameObject go, out Bounds bounds)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        var colliders = go.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            return true;
        }

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

    // ====== (회전 전 방식) 타일 찾기: RotatePreview에서만 쓰면 됨
    List<GameObject> FindTilesAround(GameObject baseTile, int width, int height)
    {
        List<GameObject> result = new List<GameObject>();
        Vector3 basePos = baseTile.transform.position;
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");

        float tileSize = GetTileSize(baseTile).x;
        float tolerance = tileSize * 0.5f;

        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
            {
                Vector3 targetPos = basePos + new Vector3(x * tileSize, 0, z * tileSize);

                GameObject closest = null;
                float minDist = float.MaxValue;
                foreach (GameObject tile in allTiles)
                {
                    float dist = Vector3.Distance(tile.transform.position, targetPos);
                    if (dist < tolerance && dist < minDist) { closest = tile; minDist = dist; }
                }
                if (closest == null) return null;
                result.Add(closest);
            }
        return result;
    }
}
