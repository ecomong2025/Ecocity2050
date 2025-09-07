using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TileClickInstaller : MonoBehaviour
{
    // ====== 설정 ======
    [Header("Placement Settings")]
    [Tooltip("타일 점유를 표시할 마커 이름")]
    public string occupiedMarkerName = "__OCCUPIED__";

    // 1.00 = 딱 맞춤, <1.0 = 살짝 작게, >1.0 = 살짝 크게(‘보강’)
    [Range(0.90f, 1.10f)] public float footprintPadding = 1.00f;

    // true면 X/Z를 각각 타일 직사각형에 정확히 맞춰 채움(비율 왜곡 허용)
    // false면 균등 스케일(비율 유지)
    public bool fillBothAxes = true;

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


    private GameObject selectedBuildingPrefab;
    private GameObject previewInstance;   // 회전 중심이 될 빈 오브젝트(부모)
    private GameObject modelInstance;     // 실제 건물 모델(자식)
    private float previewRotation = 0f;   // 0/90/180/270
    private List<GameObject> currentTiles;

    // —— 회전/재선택을 위한 그리드 정보 캐시 ——
    Vector3 _gridU, _gridV; // 그리드 축(정규화)
    float _stepU, _stepV;   // 축 간격(월드 단위)
    int _signU, _signV;     // 드래그 방향 부호
    GameObject _pivotTile;  // 드래그 시작 타일(피벗)
    GameObject _dirTile;    // 드래그 도중 마우스 아래 타일(방향 기준)

    void Awake()
    {
        if (Instance == null) Instance = this; else Destroy(gameObject);
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
                var bd = selectedBuildingPrefab.GetComponent<BuildingData>() ?? selectedBuildingPrefab.GetComponentInChildren<BuildingData>();
                if (bd == null) return;

                var size = GetRotatedSize(bd.tileWidth, bd.tileHeight, previewRotation);

                var rectTiles = FindTilesRectangleOnGrid(
                    dragStartTile, size.x, size.y, tileB,
                    out _gridU, out _gridV, out _stepU, out _stepV, out _signU, out _signV
                );

                _pivotTile = dragStartTile; // 회전 시 재사용
                _dirTile = tileB;

                bool valid = rectTiles != null && rectTiles.Count == size.x * size.y && AllTilesFree(rectTiles);

                HighlightTiles(rectTiles, valid);

                if (buildingInstallPanel) buildingInstallPanel.SetActive(true);
                if (confirmInstallButton) confirmInstallButton.interactable = valid;

                if (Input.GetMouseButtonUp(0))
                {
                    isDragging = false;
                    if (valid)
                    {
                        currentTiles = rectTiles;
                        SpawnPreviewOverSelection(selectedBuildingPrefab);
                        // ✅ 하이라이트 유지 (설치 확정 전까지 보이게)
                    }
                    else
                    {
                        ClearHighlight();                 // 불가일 때만 숨김
                    }
                }

            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                ClearHighlight();
            }
        }

        // 회전 단축키
        if (previewInstance != null)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                // 기존 R 키 → +90도 회전
                RotatePreview();
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                previewRotation = 0f;
                SpawnPreviewOverSelection(selectedBuildingPrefab);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                previewRotation = 90f;
                SpawnPreviewOverSelection(selectedBuildingPrefab);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                previewRotation = 180f;
                SpawnPreviewOverSelection(selectedBuildingPrefab);
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                previewRotation = 270f;
                SpawnPreviewOverSelection(selectedBuildingPrefab);
            }
        }

    }

    // ─────────────────────────────────────────────────────────────
    // 실제 그리드 축(u, v)을 추정해 width×height 타일을 모은다 (그리드 정보 out 제공)
    List<GameObject> FindTilesRectangleOnGrid(
        GameObject baseTile, int width, int height, GameObject dragTile,
        out Vector3 u, out Vector3 vAxis, out float stepU, out float stepV, out int signU, out int signV)
    {
        u = vAxis = Vector3.zero; stepU = stepV = 0f; signU = signV = 1;

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
        Vector3 uNorm = neigh[0].normalized; // 가장 가까운 이웃 방향
        float stepUVal = Mathf.Sqrt(neigh[0].sqrMagnitude);

        Vector3 vNorm = Vector3.zero; float stepVVal = stepUVal;
        for (int i = 1; i < neigh.Count; i++)
        {
            var n = neigh[i].normalized;
            float parallel = Mathf.Abs(Vector3.Dot(n, uNorm));
            if (parallel < 0.5f) { vNorm = n; stepVVal = Mathf.Sqrt(neigh[i].sqrMagnitude); break; }
        }
        if (vNorm == Vector3.zero)
        {
            vNorm = Vector3.ProjectOnPlane(baseTile.transform.forward, Vector3.up).normalized;
            if (vNorm.sqrMagnitude < 0.5f) vNorm = Vector3.forward;
        }

        // 3) 드래그 방향으로 축 부호 결정
        Vector3 dragDir = dragTile.transform.position - basePos; dragDir.y = 0;
        int signUVal = Vector3.Dot(dragDir, uNorm) >= 0 ? 1 : -1;
        int signVVal = Vector3.Dot(dragDir, vNorm) >= 0 ? 1 : -1;

        float tolerance = 0.45f * Mathf.Min(stepUVal, stepVVal);

        // 4) 타일 채우기
        var result = new List<GameObject>(width * height);
        for (int iu = 0; iu < width; iu++)
        {
            for (int iv = 0; iv < height; iv++)
            {
                Vector3 target = basePos
                               + (signUVal * iu) * uNorm * stepUVal
                               + (signVVal * iv) * vNorm * stepVVal;

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

        // out 값 채우기
        u = uNorm; vAxis = vNorm; stepU = stepUVal; stepV = stepVVal; signU = signUVal; signV = signVVal;
        return result;
    }
    // ─────────────────────────────────────────────────────────────

    public void CloseWarningPanel()
    {
        if (warningPanel != null) warningPanel.SetActive(false);
        SFXPlayer.Instance?.PlayClick();
    }

    public void SetSelectedBuilding(GameObject prefab)
    {
        if (previewInstance != null) CancelInstall();

        selectedBuildingPrefab = prefab;
        Debug.Log($"선택된 건물: {prefab.name}");

        if (buildingInstallPanel) buildingInstallPanel.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;
        if (rotateButton) rotateButton.interactable = true;

        GameManager.Instance?.StartPlacing();
        SFXPlayer.Instance?.PlayClick();
    }

    void RotatePreview()
    {
        SFXPlayer.Instance?.PlayClick();
        previewRotation = (previewRotation + 90f) % 360f;

        var bd = selectedBuildingPrefab?.GetComponent<BuildingData>() ??
                 selectedBuildingPrefab?.GetComponentInChildren<BuildingData>();
        if (bd == null) return;

        var size = GetRotatedSize(bd.tileWidth, bd.tileHeight, previewRotation);

        if (_pivotTile != null && _dirTile != null)
        {
            var rectTiles = FindTilesRectangleOnGrid(
                _pivotTile, size.x, size.y, _dirTile,
                out _gridU, out _gridV, out _stepU, out _stepV, out _signU, out _signV
            );

            if (rectTiles != null && rectTiles.Count == size.x * size.y && AllTilesFree(rectTiles))
            {
                currentTiles = rectTiles;
                SpawnPreviewOverSelection(selectedBuildingPrefab); // 내부에서 긴변 자동정렬 + 정확 스냅
                return;
            }
        }

        // 폴백: 프리뷰만 회전
        if (previewInstance != null)
            previewInstance.transform.rotation = Quaternion.Euler(0f, previewRotation, 0f);
    }


    void SpawnPreviewOverSelection(GameObject prefab)
    {
        if (previewInstance != null) Destroy(previewInstance);
        modelInstance = null;

        var bd = prefab.GetComponent<BuildingData>() ?? prefab.GetComponentInChildren<BuildingData>();

        // A) 선택 타일들의 합쳐진 바운즈(정렬/높이 기준)
        Bounds selB = currentTiles[0].GetComponent<Renderer>().bounds;
        for (int i = 1; i < currentTiles.Count; i++)
            selB.Encapsulate(currentTiles[i].GetComponent<Renderer>().bounds);

        // B) 그리드 길이(축 방향 실제 길이)
        float lenU = (_stepU > 0f) ? _stepU * Mathf.Max(1, Mathf.RoundToInt(selB.size.x / _stepU)) : selB.size.x;
        float lenV = (_stepV > 0f) ? _stepV * Mathf.Max(1, Mathf.RoundToInt(selB.size.z / _stepV)) : selB.size.z;

        // C) 현재 회전 기준 타일 폭/높이
        var sizeTiles = GetRotatedSize(bd.tileWidth, bd.tileHeight, previewRotation);

        // D) 비대칭(예: 2x1)일 때 긴 변(2)이 반드시 긴 축(lenU/lenV)에 가도록 자동 보정
        int desiredRot = Mathf.RoundToInt(Mathf.Repeat(previewRotation, 360f));
        bool modelLongX = sizeTiles.x >= sizeTiles.y;
        bool gridLongU = lenU >= lenV;
        if (bd.tileWidth != bd.tileHeight && modelLongX != gridLongU)
        {
            desiredRot = (desiredRot + 90) % 360;
            sizeTiles = GetRotatedSize(bd.tileWidth, bd.tileHeight, desiredRot);
        }

        // E) 목표 크기(“타일 개수 × 그리드 스텝”)에 패딩 적용
        Vector3 targetSize = (_stepU > 0f && _stepV > 0f)
            ? new Vector3(sizeTiles.x * _stepU * footprintPadding,
                          selB.size.y,
                          sizeTiles.y * _stepV * footprintPadding)
            : new Vector3(selB.size.x * footprintPadding, selB.size.y, selB.size.z * footprintPadding); // 폴백

        // F) 부모/모델 생성 — 중심(XZ) + 바닥(Y)에 스냅
        previewInstance = new GameObject("BuildingPreviewParent");
        previewInstance.transform.SetPositionAndRotation(
            new Vector3(selB.center.x, selB.max.y, selB.center.z),
            Quaternion.Euler(0f, desiredRot, 0f)
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

        // G) 스케일 — 정확 채움(fillBothAxes) or 균등 스케일
        if (bd.tileWidth != bd.tileHeight)
        {
            // 🔹 비대칭 건물 (예: 2x1, 1x2) → 무조건 작은 쪽 기준으로 줄임
            ResizeToFitUniform(modelInstance, targetSize, modelBounds);
        }
        else
        {
            // 🔹 정사각형 건물은 기존 옵션 따라감
            if (fillBothAxes) ResizeToFitExact(modelInstance, targetSize);
            else ResizeToFitUniform(modelInstance, targetSize, modelBounds);
        }


        // H) 바닥/중앙으로 최종 정렬(정확 스냅)
        AlignPreviewToSelection(selB);

        modelInstance.SetActive(true);
        buildingInstallPanel?.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;

        // 실제 적용된 회전을 캐시에 반영
        previewRotation = desiredRot;
    }
    // 비율 유지(균등) 스케일
    void ResizeToFitUniform(GameObject building, Vector3 targetSize, Bounds _unused)
    {
        var t = building.transform;
        t.localScale = Vector3.one;

        if (!TryGetModelBounds(building, out Bounds baseB)) return;
        Vector3 size = baseB.size; if (size.x <= 0f || size.z <= 0f) return;

        float s = Mathf.Min(targetSize.x / size.x, targetSize.z / size.z);
        t.localScale = new Vector3(s, s, s);
    }

    // X/Z를 각각 정확히 맞추는 스케일(약간의 왜곡 허용) — ‘보강’ 느낌
    void ResizeToFitExact(GameObject building, Vector3 targetSize)
    {
        var t = building.transform;
        t.localScale = Vector3.one;

        if (!TryGetModelBounds(building, out Bounds baseB)) return;
        Vector3 size = baseB.size; if (size.x <= 0f || size.z <= 0f) return;

        float sx = targetSize.x / size.x;
        float sz = targetSize.z / size.z;
        float sy = Mathf.Min(sx, sz); // Y는 과도 변형 방지(낮은 배율만 적용)
        t.localScale = new Vector3(sx, sy, sz);
    }


    void AlignPreviewToSelection(Bounds selectionBounds)
    {
        if (!TryGetModelBounds(modelInstance, out Bounds b)) return;
        Vector3 deltaWorld = new Vector3(
            selectionBounds.center.x - b.center.x,
            selectionBounds.max.y - b.min.y,
            selectionBounds.center.z - b.center.z
        );
        modelInstance.transform.position += deltaWorld;
    }

    void ConfirmInstall()
    {
        if (previewInstance == null || currentTiles == null) return;

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) return;

        BuildingData buildingData = modelInstance.GetComponent<BuildingData>() ?? modelInstance.GetComponentInChildren<BuildingData>();
        if (buildingData == null) return;

        // 태그 설정 시도
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

        int incomePerMinute = (buildingData.incomePer5Minutes > 0) ? buildingData.incomePer5Minutes / 5 : 0;

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
        SFXPlayer.Instance?.PlayClick();

        GameManager.Instance?.CompletePlacing();
        ClearPreviewAndPanel();

        ClearHighlight();                 // ✅ 설치 확정 시 하이라이트 숨김
        SFXPlayer.Instance?.PlayClick();
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
        SFXPlayer.Instance?.PlayClick();
        ClearPreviewAndPanel();
        ClearHighlight();                 // ✅ 취소 시 숨김 (파괴 아님)
    }

    void ClearPreviewAndPanel()
    {
        previewInstance = null;
        modelInstance = null;
        // selectedBuildingPrefab = null;  // 선택 유지
        currentTiles = null;
        if (buildingInstallPanel) buildingInstallPanel.SetActive(false);
    }

    public void ClearSelection()
    {
        if (previewInstance != null) Destroy(previewInstance);
        selectedBuildingPrefab = null;
        ClearPreviewAndPanel();

    }

    Vector2Int GetRotatedSize(int width, int height, float rotation)
    {
        // rotation을 0/90/180/270으로 스냅
        int r = Mathf.RoundToInt(Mathf.Repeat(rotation, 360f) / 90f) * 90;
        return (r % 180 != 0) ? new Vector2Int(height, width) : new Vector2Int(width, height);
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

            Bounds b = TransformMeshBounds(mfs[0]);
            for (int i = 1; i < mfs.Length; i++) b.Encapsulate(TransformMeshBounds(mfs[i]));
            bounds = b;
            return true;
        }

        bounds = new Bounds(go.transform.position, Vector3.zero);
        return false;
    }

    // ====== (회전 전 방식) 타일 찾기: RotatePreview에서만 쓰면 됨 (보존)
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
