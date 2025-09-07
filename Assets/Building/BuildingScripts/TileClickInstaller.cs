using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TileClickInstaller : MonoBehaviour
{
    // ====== 설정 ======
    [Header("Placement Settings")]
    [Tooltip("타일 점유를 표시할 마커 이름")]
    public string occupiedMarkerName = "__OCCUPIED__";

    [Range(0.90f, 1.10f)] public float footprintPadding = 1.00f; // 설치 면적 패딩
    public bool fillBothAxes = true; // true: X/Z 각각 채움, false: 균등 스케일
                                     // === Tile edge (타일 둘레 초록 라인) ===
    [Header("Tile Edge Lines")]
    [Tooltip("각 Tile의 자식 중 '테두리 라인'이 들어있는 자식 이름")]
    public string tileEdgeChildName = "TileEdge";   // 씬에서 쓰는 실제 이름으로 바꿔도 OK

    readonly List<Renderer> _tileEdgeRenderers = new();

    // === Drag Select ===
    [Header("Drag Select")]
    public LayerMask tileLayerMask = ~0; // Tile 레이어 추천
    public Material highlightMat;         // URP Unlit Transparent 권장(없으면 lineMat 사용)
    public Color highlightValid = new Color(0.2f, 1f, 0.4f, 0.35f);
    public Color highlightInvalid = new Color(1f, 0.25f, 0.25f, 0.35f);
    public float highlightYOffset = 0.01f;
    public Material lineMat;

    // === Grid Lines(선택) ===
    [Header("Grid Lines (Optional)")]
    [Tooltip("씬에서 격자 라인들의 공통 부모(있으면 여기 자식의 Renderer 전부 토글).")]
    public Transform gridLinesRoot;
    [Tooltip("부모가 없으면 필요 렌더러를 수동으로 넣어두세요.")]
    public Renderer[] extraGridLineRenderers;

    // 내부
    bool isDragging;
    GameObject dragStartTile;
    List<GameObject> highlightedTiles = new();
    List<GameObject> highlightQuads = new();

    static readonly int BaseColorID2 = Shader.PropertyToID("_BaseColor"); // URP
    static readonly int ColorID = Shader.PropertyToID("_Color");          // Built-in
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

    // 선택/프리뷰 상태
    private GameObject selectedBuildingPrefab;
    private GameObject previewInstance;   // 회전 중심 부모
    private GameObject modelInstance;     // 실제 모델(자식)
    private float previewRotation = 0f;   // 0/90/180/270
    private List<GameObject> currentTiles;

    // 그리드 정보 캐시
    Vector3 _gridU, _gridV; // 그리드 축(정규화)
    float _stepU, _stepV;   // 축 간격(월드)
    int _signU, _signV;     // 드래그 방향 부호
    GameObject _pivotTile;  // 드래그 시작 타일
    GameObject _dirTile;    // 드래그 중 기준 타일
                            // ====== Grid Line Toggle ======
    [Header("Grid Lines (toggle while placing)")]

    public string gridLineTag = "GridLine"; // 방법 B: 라인 오브젝트에 이 태그 지정

    readonly List<Renderer> _gridRenderers = new();
    readonly List<LineRenderer> _gridLineRenderers = new();
    bool _gridShown = false; // 현재 표시 상태 캐시
    void CacheTileEdgeLines()
    {
        _tileEdgeRenderers.Clear();
        var tiles = GameObject.FindGameObjectsWithTag("Tile");
        foreach (var t in tiles)
        {
            // 1) 이름으로 우선 찾기
            Transform edge = string.IsNullOrEmpty(tileEdgeChildName) ? null : t.transform.Find(tileEdgeChildName);
            if (edge != null)
            {
                _tileEdgeRenderers.AddRange(edge.GetComponentsInChildren<Renderer>(true));
            }
            else
            {
                // 2) 폴백: 라인처럼 보이는 렌더러 추려 담기(이름 힌트)
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is LineRenderer) { _tileEdgeRenderers.Add(r); continue; }
                    var n = r.name.ToLowerInvariant();
                    if (n.Contains("edge") || n.Contains("glow") || n.Contains("grid"))
                        _tileEdgeRenderers.Add(r);
                }
            }
        }
    }

    void SetTileEdgeLinesVisible(bool on)
    {
        foreach (var r in _tileEdgeRenderers)
            if (r) r.enabled = on;
    }

    // 설치 관련 모든 FX(격자 + 타일둘레 + 하이라이트/선택라인) 토글
    void TogglePlacementFX(bool on)
    {
        SetGridLinesVisible(on);       // 기존 "전체 격자" 토글
        SetTileEdgeLinesVisible(on);   // 새로 추가: 타일 둘레 라인 토글

        if (!on)
        {
            ClearHighlight();
            // 선택 사각 라인을 쓰면 여기에 HideSelectionLine(); 호출
        }
        _gridShown = on; // (겸사겸사 상태 캐시)
    }

    // 모두 모아서 캐시 (부모/태그/수동 배열)
    void CacheGridLines()
    {
        _gridRenderers.Clear();
        _gridLineRenderers.Clear();

        // A) 부모 기준으로만 수집
        if (gridLinesRoot)
        {
            _gridRenderers.AddRange(gridLinesRoot.GetComponentsInChildren<Renderer>(true));
            _gridLineRenderers.AddRange(gridLinesRoot.GetComponentsInChildren<LineRenderer>(true));
        }

        // B) 인스펙터에서 수동으로 넣은 라인도 포함
        if (extraGridLineRenderers != null && extraGridLineRenderers.Length > 0)
            _gridRenderers.AddRange(extraGridLineRenderers);

        // ✅ 태그 검색 제거 (없으면 예외 터졌던 부분)
    }


    // ✅ 부모가 있으면 그냥 그 부모만 ON/OFF (가장 확실)
    void SetGridLinesVisible(bool on)
    {
        if (gridLinesRoot)
        {
            gridLinesRoot.gameObject.SetActive(on); // 부모 전체 on/off (가장 확실)
            return;
        }

        // 부모가 없을 때만 개별 컴포넌트 토글
        foreach (var lr in _gridLineRenderers) if (lr) lr.enabled = on;
        foreach (var r in _gridRenderers) if (r) r.enabled = on;
    }



    bool IsPlacingNow()
    {
        // “설치 중” 판단: 설치 패널이 켜져 있고, 선택 프리팹이 있을 때
        return selectedBuildingPrefab != null
               && buildingInstallPanel != null
               && buildingInstallPanel.activeInHierarchy;
    }

    // 격자 라인 토글 상태



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

        // 격자 라인 캐시 후 시작 시 OFF
        CacheGridLines();
        SetGridLinesVisible(false);
        _gridShown = false;
        CacheTileEdgeLines();    // ★ 타일 에지 라인 수집
        TogglePlacementFX(false); // ★ 시작은 전부 OFF
        // 시작 시 하이라이트도 OFF 안전장치
        ClearHighlight();
        CacheGridLines();
        SetGridLinesVisible(false); // 시작할 때는 무조건 OFF
        _gridShown = false;

    }

    void Update()
    {
        bool placing = IsPlacingNow();
        if (placing != _gridShown)
            TogglePlacementFX(placing);
        // 설치 모드 토글(상태 변할 때만)
        bool shouldShowGrid = IsPlacingNow();
        if (shouldShowGrid != _gridShown)
        {
            SetGridLinesVisible(shouldShowGrid);
            _gridShown = shouldShowGrid;
            if (!shouldShowGrid)
            {
                ClearHighlight();
                HideSelectionLine(); // 선택 사각 라인 쓰는 경우에 대비 (비워둔 스텁)
            }
        }

        if (selectedBuildingPrefab == null) return;

        // 드래그 시작
        if (Input.GetMouseButtonDown(0) && TryGetTileUnderMouse(out var tile))
        {
            isDragging = true;
            dragStartTile = tile;
            ClearHighlight(); // 새 드래그 시작 시 잔상 제거
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

                var rectTiles = FindTilesRectangleOnGrid(
                    dragStartTile, size.x, size.y, tileB,
                    out _gridU, out _gridV, out _stepU, out _stepV, out _signU, out _signV
                );

                _pivotTile = dragStartTile;
                _dirTile = tileB;

                bool valid = rectTiles != null && rectTiles.Count == size.x * size.y && AllTilesFree(rectTiles);

                HighlightTiles(rectTiles, valid);

                if (buildingInstallPanel) buildingInstallPanel.SetActive(true);
                if (confirmInstallButton) confirmInstallButton.interactable = valid;

                // 드래그 확정
                if (Input.GetMouseButtonUp(0))
                {
                    isDragging = false;
                    if (valid)
                    {
                        currentTiles = rectTiles;
                        SpawnPreviewOverSelection(selectedBuildingPrefab); // 프리뷰 생성
                        // 설치 확정 전까지 하이라이트 유지
                    }
                    else
                    {
                        ClearHighlight(); // 유효하지 않으면 끔
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                ClearHighlight();
            }
        }

        // 회전 단축키(프리뷰 있을 때만)
        if (previewInstance != null)
        {
            if (Input.GetKeyDown(KeyCode.R)) { RotatePreview(); }
            else if (Input.GetKeyDown(KeyCode.W)) { previewRotation = 0f; SpawnPreviewOverSelection(selectedBuildingPrefab); }
            else if (Input.GetKeyDown(KeyCode.D)) { previewRotation = 90f; SpawnPreviewOverSelection(selectedBuildingPrefab); }
            else if (Input.GetKeyDown(KeyCode.S)) { previewRotation = 180f; SpawnPreviewOverSelection(selectedBuildingPrefab); }
            else if (Input.GetKeyDown(KeyCode.A)) { previewRotation = 270f; SpawnPreviewOverSelection(selectedBuildingPrefab); }
        }

        // 설치 모드가 아니면 하이라이트 잔상 제거
        if (!IsPlacingNow() && highlightedTiles.Count > 0)
            ClearHighlight();
    }

    // 설치(배치) 중인지 판단


    // ─────────────────────────────────────────────────────────────




    // 선택 사각 라인 안 쓰는 프로젝트 대비 스텁
    void HideSelectionLine() { /* 필요 시 선택 라인 끄는 코드 넣기 */ }

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

        // out 값
        u = uNorm; vAxis = vNorm; stepU = stepUVal; stepV = stepVVal; signU = signUVal; signV = signVVal;
        return result;

        bool shouldShow = IsPlacingNow();
        if (shouldShow != _gridShown)
        {
            SetGridLinesVisible(shouldShow);
            _gridShown = shouldShow;

            if (!shouldShow)
            {
                // 설치 모드 종료 시 잔상 제거
                ClearHighlight();
                // 선택 사각 라인 쓰면 여기서 HideSelectionLine(); 같이 호출
            }
        }

    }

    public void CloseWarningPanel()
    {
        if (warningPanel != null) warningPanel.SetActive(false);
        SFXPlayer.Instance?.PlayClick();
    }

    public void SetSelectedBuilding(GameObject prefab)
    {
        if (previewInstance != null) CancelInstall();
        selectedBuildingPrefab = prefab;

        buildingInstallPanel?.SetActive(true);
        confirmInstallButton!.interactable = true;
        rotateButton!.interactable = true;

        GameManager.Instance?.StartPlacing();

        TogglePlacementFX(true);   // ★ 설치 들어가면 전부 ON
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
                SpawnPreviewOverSelection(selectedBuildingPrefab);
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

        // A) 선택 타일들의 합쳐진 바운즈
        Bounds selB = currentTiles[0].GetComponent<Renderer>().bounds;
        for (int i = 1; i < currentTiles.Count; i++)
            selB.Encapsulate(currentTiles[i].GetComponent<Renderer>().bounds);

        // B) 그리드 길이
        float lenU = (_stepU > 0f) ? _stepU * Mathf.Max(1, Mathf.RoundToInt(selB.size.x / _stepU)) : selB.size.x;
        float lenV = (_stepV > 0f) ? _stepV * Mathf.Max(1, Mathf.RoundToInt(selB.size.z / _stepV)) : selB.size.z;

        // C) 회전 기준 타일 폭/높이
        var sizeTiles = GetRotatedSize(bd.tileWidth, bd.tileHeight, previewRotation);

        // D) 비대칭 자동 보정
        int desiredRot = Mathf.RoundToInt(Mathf.Repeat(previewRotation, 360f));
        bool modelLongX = sizeTiles.x >= sizeTiles.y;
        bool gridLongU = lenU >= lenV;
        if (bd.tileWidth != bd.tileHeight && modelLongX != gridLongU)
        {
            desiredRot = (desiredRot + 90) % 360;
            sizeTiles = GetRotatedSize(bd.tileWidth, bd.tileHeight, desiredRot);
        }

        // E) 목표 크기
        Vector3 targetSize = (_stepU > 0f && _stepV > 0f)
            ? new Vector3(sizeTiles.x * _stepU * footprintPadding,
                          selB.size.y,
                          sizeTiles.y * _stepV * footprintPadding)
            : new Vector3(selB.size.x * footprintPadding, selB.size.y, selB.size.z * footprintPadding);

        // F) 프리뷰 생성/정렬
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
            Debug.LogError("[Installer] 프리팹에 렌더러/콜라이더/메시가 없습니다.");
            return;
        }

        // G) 스케일
        if (bd.tileWidth != bd.tileHeight)
        {
            ResizeToFitUniform(modelInstance, targetSize, modelBounds);
        }
        else
        {
            if (fillBothAxes) ResizeToFitExact(modelInstance, targetSize);
            else ResizeToFitUniform(modelInstance, targetSize, modelBounds);
        }

        // H) 바닥/중앙 정렬
        AlignPreviewToSelection(selB);

        modelInstance.SetActive(true);
        buildingInstallPanel?.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;

        previewRotation = desiredRot;
    }

    // 비율 유지(균등) 스케일
    void ResizeToFitUniform(GameObject building, Vector3 targetSize, Bounds _unused)
    {
        var t = building.transform; t.localScale = Vector3.one;
        if (!TryGetModelBounds(building, out Bounds baseB)) return;
        Vector3 size = baseB.size; if (size.x <= 0f || size.z <= 0f) return;
        float s = Mathf.Min(targetSize.x / size.x, targetSize.z / size.z);
        t.localScale = new Vector3(s, s, s);
    }

    // X/Z 각각 맞춤(약간 왜곡 허용)
    void ResizeToFitExact(GameObject building, Vector3 targetSize)
    {
        var t = building.transform; t.localScale = Vector3.one;
        if (!TryGetModelBounds(building, out Bounds baseB)) return;
        Vector3 size = baseB.size; if (size.x <= 0f || size.z <= 0f) return;
        float sx = targetSize.x / size.x;
        float sz = targetSize.z / size.z;
        float sy = Mathf.Min(sx, sz); // Y 과도 변형 방지
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

        BuildingData buildingData = modelInstance.GetComponent<BuildingData>() ??
                                    modelInstance.GetComponentInChildren<BuildingData>();
        if (buildingData == null) return;

        // 태그 설정 시도
        if (previewInstance.tag != "Building")
        {
            try { previewInstance.tag = "Building"; }
            catch (UnityException e) { Debug.LogWarning($"[TileClickInstaller] 'Building' 태그 설정 실패: {e.Message}"); }
        }

        // 대표 타일만 부모로
        previewInstance.transform.SetParent(currentTiles[0].transform, true);

        // 점유 마커
        foreach (var tile in currentTiles)
            if (tile.transform.Find(occupiedMarkerName) == null)
                new GameObject(occupiedMarkerName).transform.SetParent(tile.transform, false);

        // 풋프린트 기록
        var footprint = previewInstance.AddComponent<BuildingFootprint>();
        footprint.Init(currentTiles, occupiedMarkerName);

        // 게임 매니저 반영(프로젝트 로직 유지)
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

        ClearPreviewAndPanel(false);
        // 설치 끝 → 시각 효과 OFF
        ClearHighlight();
        SetGridLinesVisible(false);
        _gridShown = false;

        SFXPlayer.Instance?.PlayClick();
        GameManager.Instance?.CompletePlacing();
        

        TogglePlacementFX(false);  // ★ 끝났으니 전부 OFF
        SFXPlayer.Instance?.PlayClick();
    }
    // 미리보기 파괴 여부를 선택할 수 있게 수정
    void ClearPreviewAndPanel(bool destroyPreview = true)
    {
        if (destroyPreview && previewInstance != null)
            Destroy(previewInstance);   // 미리보기만 파괴

        previewInstance = null;         // 레퍼런스만 비우기
        modelInstance = null;

        if (buildingInstallPanel) buildingInstallPanel.SetActive(false);
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
        ClearPreviewAndPanel(true);

        TogglePlacementFX(false);  // ★ 취소도 OFF
        SFXPlayer.Instance?.PlayClick();
    }

    public void ClearSelection()
    {
        if (previewInstance != null) Destroy(previewInstance);
        selectedBuildingPrefab = null;
        ClearPreviewAndPanel(true);

        TogglePlacementFX(false);  // ★ 선택 해제도 OFF
    }




    Vector2Int GetRotatedSize(int width, int height, float rotation)
    {
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
    // 마우스 아래 타일 찾기
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

    // 선택한 타일들이 모두 비어있는지 검사
    bool AllTilesFree(List<GameObject> tiles)
    {
        if (tiles == null || tiles.Count == 0) return false;
        foreach (var t in tiles)
        {
            if (t == null) return false;
            if (t.transform.Find(occupiedMarkerName) != null) return false;
        }
        return true;
    }

    // 하이라이트 표시/해제
    void HighlightTiles(List<GameObject> tiles, bool valid)
    {
        // 설치 모드가 아니면 강제 OFF
        if (!IsPlacingNow())
        {
            ClearHighlight();
            return;
        }

        ClearHighlight();
        if (tiles == null) return;

        EnsureHighlightPool(tiles.Count);

        Color col = valid ? highlightValid : highlightInvalid;
        _mpbHighlight.Clear();
        _mpbHighlight.SetColor(BaseColorID2, col); // URP
        _mpbHighlight.SetColor(ColorID, col);      // Built-in

        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            var q = highlightQuads[i];
            var r = t.GetComponent<Renderer>();
            if (!r) continue;

            var b = r.bounds;

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

    // (보존) 세계 X/Z 기준 직사각형 — 회전 그리드에서는 권장 X
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

    // 회전 전 방식
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
