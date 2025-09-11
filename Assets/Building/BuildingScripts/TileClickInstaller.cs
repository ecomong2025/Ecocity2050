using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class TileClickInstaller : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // Hotkey / 외부 UI 입력 격리(선택)
    [Header("Hotkey Isolation (optional)")]
    [Tooltip("설치 중에는 여기 들어있는 컴포넌트들의 enabled를 꺼서 전역 핫키가 반응하지 않게 합니다. (예: PauseMenu, PanelToggler 등)")]
    public Behaviour[] disableWhilePlacing;

    void SetExternalHotkeysEnabled(bool on)
    {
        if (disableWhilePlacing == null) return;
        foreach (var b in disableWhilePlacing) if (b) b.enabled = on;
    }

    [Header("Hotkeys")]
    public bool enableHotkeys = true;                 // 키 사용 여부
    public bool requirePlacingForHotkeys = true;      // 설치 중일 때만 허용
    public KeyCode rotateKey = KeyCode.Space;         // 회전 키(스페이스)

    bool _uiNavWasOn = true;
    bool _uiNavSuppressed = false;
    void SuppressUINavEvents(bool on)
    {
        var es = EventSystem.current;
        if (!es) return;

        if (on && !_uiNavSuppressed)
        {
            _uiNavWasOn = es.sendNavigationEvents;
            es.sendNavigationEvents = false;
            es.SetSelectedGameObject(null);
            _uiNavSuppressed = true;
        }
        else if (!on && _uiNavSuppressed)
        {
            es.sendNavigationEvents = _uiNavWasOn;
            _uiNavSuppressed = false;
        }
    }
    [SerializeField]
    [Tooltip("긴축 자동 보정 (사용자 회전 우선, 기본 off)")]
    bool autoAlignRotationToSelection = false;

    // ─────────────────────────────────────────────────────────────
    // Placement / Grid / Highlight
    [Header("Placement Settings")]
    [Tooltip("타일 점유를 표시할 마커 이름")]
    public string occupiedMarkerName = "__OCCUPIED__";
    [Range(0.90f, 1.10f)] public float footprintPadding = 1.00f;
    public bool fillBothAxes = true;

    [Header("Tile Edge Lines")]
    [Tooltip("각 Tile의 자식 중 '테두리 라인'이 들어있는 자식 이름")]
    public string tileEdgeChildName = "TileEdge";
    readonly List<Renderer> _tileEdgeRenderers = new();

    [Header("Drag Select")]
    public LayerMask tileLayerMask = ~0;
    public Material highlightMat;
    public Color highlightValid = new Color(0.2f, 1f, 0.4f, 0.35f);
    public Color highlightInvalid = new Color(1f, 0.25f, 0.25f, 0.35f);
    public float highlightYOffset = 0.01f;
    public Material lineMat;

    [Header("Grid Lines (Optional)")]
    public Transform gridLinesRoot;
    public Renderer[] extraGridLineRenderers;

    // 내부 상태
    bool isDragging;
    GameObject dragStartTile;
    List<GameObject> highlightedTiles = new();
    List<GameObject> highlightQuads = new();

    static readonly int BaseColorID2 = Shader.PropertyToID("_BaseColor"); // URP
    static readonly int ColorID = Shader.PropertyToID("_Color");      // Built-in
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
    Vector3 _gridU, _gridV;
    float _stepU, _stepV;
    int _signU, _signV;
    GameObject _pivotTile;
    GameObject _dirTile;

    // Grid 캐시
    readonly List<Renderer> _gridRenderers = new();
    readonly List<LineRenderer> _gridLineRenderers = new();
    bool _gridShown = false;

    void CacheTileEdgeLines()
    {
        _tileEdgeRenderers.Clear();
        var tiles = GameObject.FindGameObjectsWithTag("Tile");
        foreach (var t in tiles)
        {
            Transform edge = string.IsNullOrEmpty(tileEdgeChildName) ? null : t.transform.Find(tileEdgeChildName);
            if (edge) { _tileEdgeRenderers.AddRange(edge.GetComponentsInChildren<Renderer>(true)); }
            else
            {
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
        foreach (var r in _tileEdgeRenderers) if (r) r.enabled = on;
    }

    void TogglePlacementFX(bool on)
    {
        SetGridLinesVisible(on);
        SetTileEdgeLinesVisible(on);
        if (!on) ClearHighlight();
        _gridShown = on;
    }

    void CacheGridLines()
    {
        _gridRenderers.Clear();
        _gridLineRenderers.Clear();

        if (gridLinesRoot)
        {
            _gridRenderers.AddRange(gridLinesRoot.GetComponentsInChildren<Renderer>(true));
            _gridLineRenderers.AddRange(gridLinesRoot.GetComponentsInChildren<LineRenderer>(true));
        }

        if (extraGridLineRenderers != null && extraGridLineRenderers.Length > 0)
            _gridRenderers.AddRange(extraGridLineRenderers);
    }

    void SetGridLinesVisible(bool on)
    {
        if (gridLinesRoot)
        {
            gridLinesRoot.gameObject.SetActive(on);
            return;
        }
        foreach (var lr in _gridLineRenderers) if (lr) lr.enabled = on;
        foreach (var r in _gridRenderers) if (r) r.enabled = on;
    }

    bool IsPlacingNow()
    {
        return selectedBuildingPrefab != null
               && buildingInstallPanel != null
               && buildingInstallPanel.activeInHierarchy;
    }

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this; else Destroy(gameObject);
    }

    void Start()
    {
        if (confirmButton) confirmButton.onClick.AddListener(CloseWarningPanel);
        if (confirmInstallButton) confirmInstallButton.onClick.AddListener(ConfirmInstall);
        if (cancelInstallButton) cancelInstallButton.onClick.AddListener(CancelInstall);
        if (rotateButton) rotateButton.onClick.AddListener(RotatePreview);

        _mpbHighlight = new MaterialPropertyBlock();

        CacheGridLines();
        SetGridLinesVisible(false);
        _gridShown = false;
        CacheTileEdgeLines();
        TogglePlacementFX(false);
        ClearHighlight();
    }
    // 드래그 확정(클릭) 직후 미리보기에서 '한 번' 긴축 보정을 허용하기 위한 플래그
    bool _autoAlignPending = false;


    void Update()
    {
        bool placing = IsPlacingNow();
        if (placing != _gridShown) TogglePlacementFX(placing);

        // 드래그 중에는 회전 금지
        if (enableHotkeys && (!requirePlacingForHotkeys || placing))
        {
            if (Input.GetKeyDown(rotateKey) && !isDragging)
                RotatePreview();
        }

        if (!placing) return;
        if (selectedBuildingPrefab == null) return;

        // ⬇⬇⬇ 여기서 한 번만 선언 (스코프 고정) ⬇⬇⬇
        GameObject hoverTile = null;                 // 마우스 아래 타일(= tileB)
        List<GameObject> rectTiles = null;           // 후보 타일 집합
        BuildingData bd = selectedBuildingPrefab.GetComponent<BuildingData>()
                          ?? selectedBuildingPrefab.GetComponentInChildren<BuildingData>();
        // ⬆⬆⬆ 이후에는 "재선언 금지" — 값만 대입해서 사용 ⬆⬆⬆

        // ── 드래그 시작 ──
        if (Input.GetMouseButtonDown(0) && TryGetTileUnderMouse(out var tile))
        {
            isDragging = true;
            dragStartTile = tile;
            ClearHighlight();
        }

        // ── 드래그 중 ──
        if (isDragging)
        {
            if (TryGetTileUnderMouse(out hoverTile))
            {
                if (bd == null) return;

                // 현재 회전에 따른 필요 칸수 (2x1 ↔ 1x2)
                var size = GetRotatedSize(bd.tileWidth, bd.tileHeight, previewRotation);

                // 드래그 영역 후보
                rectTiles = FindTilesRectangleOnGrid(
                    dragStartTile, size.x, size.y, hoverTile,
                    out _gridU, out _gridV, out _stepU, out _stepV, out _signU, out _signV
                );

                _pivotTile = dragStartTile;
                _dirTile = hoverTile;

                bool valid = rectTiles != null
                          && rectTiles.Count == size.x * size.y
                          && AllTilesFree(rectTiles);

                HighlightTiles(rectTiles, valid);
                if (buildingInstallPanel) buildingInstallPanel.SetActive(true);
                if (confirmInstallButton) confirmInstallButton.interactable = valid;

                // ── 드래그 종료(확정) ──
                if (Input.GetMouseButtonUp(0))
                {
                    isDragging = false;
                    if (valid)
                    {
                        currentTiles = rectTiles;

                        // 직사각형이면 드래그 축(U/V)에 맞춰 초기 각도 0°/90°
                        if (bd != null && bd.tileWidth != bd.tileHeight)
                        {
                            Vector3 dragDir = (hoverTile.transform.position - dragStartTile.transform.position);
                            dragDir.y = 0f;
                            float du = Mathf.Abs(Vector3.Dot(dragDir.normalized, _gridU.normalized));
                            float dv = Mathf.Abs(Vector3.Dot(dragDir.normalized, _gridV.normalized));
                            previewRotation = (du >= dv) ? 0f : 90f; // U=0/180, V=90/270
                        }
                        else
                        {
                            previewRotation = 0f;
                        }

                        // 이번 클릭으로 뜨는 미리보기에서 '1회' 긴축 보정 허용
                        _autoAlignPending = true;

                        SpawnPreviewOverSelection(selectedBuildingPrefab);
                    }
                    else
                    {
                        ClearHighlight();
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                ClearHighlight();
            }
        }

        // 안전장치
        if (!IsPlacingNow() && highlightedTiles.Count > 0)
            ClearHighlight();
    }


    // ─────────────────────────────────────────────────────────────
    void HideSelectionLine() { /* 필요 시 선택 라인 끄는 코드 */ }

    List<GameObject> FindTilesRectangleOnGrid(
        GameObject baseTile, int width, int height, GameObject dragTile,
        out Vector3 u, out Vector3 vAxis, out float stepU, out float stepV, out int signU, out int signV)
    {
        u = vAxis = Vector3.zero; stepU = stepV = 0f; signU = signV = 1;

        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        Vector3 basePos = baseTile.transform.position;

        // 1) 가까운 이웃 벡터
        var neigh = new List<Vector3>();
        foreach (var t in allTiles)
        {
            if (t == baseTile) continue;
            Vector3 vv = t.transform.position - basePos; vv.y = 0;
            if (vv.sqrMagnitude > 0.0001f) neigh.Add(vv);
        }
        if (neigh.Count == 0) return null;
        neigh.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));

        // 2) u/v 축 및 스텝
        Vector3 uNorm = neigh[0].normalized;
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

        // 3) 드래그 방향 부호
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

        u = uNorm; vAxis = vNorm; stepU = stepUVal; stepV = stepVVal; signU = signUVal; signV = signVVal;
        return result;
    }

    public void CloseWarningPanel()
    {
        if (warningPanel) warningPanel.SetActive(false);
        SFXPlayer.Instance?.PlayClick();
    }

    public void SetSelectedBuilding(GameObject prefab)
    {
        if (previewInstance != null) CancelInstall();
        selectedBuildingPrefab = prefab;

        buildingInstallPanel?.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;
        if (rotateButton) rotateButton.interactable = true;

        GameManager.Instance?.StartPlacing();

        TogglePlacementFX(true);
        SuppressUINavEvents(true);
        SetExternalHotkeysEnabled(false);
        SFXPlayer.Instance?.PlayClick();
    }

    void RotatePreview()
    {
        var bd = selectedBuildingPrefab?.GetComponent<BuildingData>() ??
                 selectedBuildingPrefab?.GetComponentInChildren<BuildingData>();
        if (!bd) return;

        if (bd.tileWidth != bd.tileHeight)
            previewRotation = Mathf.Repeat(previewRotation + 180f, 360f); // 2x1 등: 180°
        else
            previewRotation = Mathf.Repeat(previewRotation + 90f, 360f);  // 정사각형: 90°

        // 사용자 회전에는 긴축 보정 금지
        _autoAlignPending = false;

        SpawnPreviewOverSelection(selectedBuildingPrefab);
    }



    void SpawnPreviewOverSelection(GameObject prefab)
    {
        // 1) 기존 프리뷰 제거
        if (previewInstance != null) Destroy(previewInstance);
        modelInstance = null;

        var bd = prefab.GetComponent<BuildingData>() ?? prefab.GetComponentInChildren<BuildingData>();
        if (bd == null || currentTiles == null || currentTiles.Count == 0)
        {
            Debug.LogWarning("[Installer] BuildingData 또는 currentTiles 누락");
            return;
        }

        // 2) 선택 영역 바운즈
        Bounds selB = currentTiles[0].GetComponent<Renderer>().bounds;
        for (int i = 1; i < currentTiles.Count; i++)
            selB.Encapsulate(currentTiles[i].GetComponent<Renderer>().bounds);

        float lenU = (_stepU > 0f) ? _stepU * Mathf.Max(1, Mathf.RoundToInt(selB.size.x / _stepU)) : selB.size.x;
        float lenV = (_stepV > 0f) ? _stepV * Mathf.Max(1, Mathf.RoundToInt(selB.size.z / _stepV)) : selB.size.z;

        // 3) 회전: 사용자 입력이 기본
        int desiredRot = Mathf.RoundToInt(Mathf.Repeat(previewRotation, 360f));
        var sizeTiles = GetRotatedSize(bd.tileWidth, bd.tileHeight, desiredRot);

        // 🔒 긴축 보정: 클릭할 때마다 1회만
        if (_autoAlignPending && autoAlignRotationToSelection && bd.tileWidth != bd.tileHeight)
        {
            bool modelLongX = sizeTiles.x >= sizeTiles.y;
            bool gridLongU = lenU >= lenV;
            if (modelLongX != gridLongU)
            {
                // 긴축 맞춤은 90° 전환(가로↔세로)
                desiredRot = (desiredRot + 90) % 360;
                sizeTiles = GetRotatedSize(bd.tileWidth, bd.tileHeight, desiredRot);
            }
        }
        // 이번 미리보기에서 보정은 끝 — 다음 클릭 전까진 OFF
        _autoAlignPending = false;


        // 4) 프리뷰 한 번만 생성 (앵커는 pivotTile 있으면 그 중심, 없으면 selB.center)
        Vector3 center = selB.center;
        if (_pivotTile != null)
        {
            var pr = _pivotTile.GetComponent<Renderer>();
            if (pr != null) center = pr.bounds.center;
        }

        previewInstance = new GameObject("BuildingPreviewParent");
        previewInstance.transform.SetPositionAndRotation(
            new Vector3(center.x, selB.max.y, center.z),
            Quaternion.Euler(0f, desiredRot, 0f)
        );

        // 5) 모델 생성
        modelInstance = Instantiate(prefab, previewInstance.transform);
        modelInstance.name = "BuildingModel";
        modelInstance.SetActive(false);

        // 🔸 원래 프리팹 스케일 저장
        Vector3 originalScale = modelInstance.transform.localScale;

        // 6) 모델 바운즈
        if (!TryGetModelBounds(modelInstance, out Bounds modelBounds))
        {
            Destroy(previewInstance); previewInstance = null; modelInstance = null;
            Debug.LogError("[Installer] 프리팹에 렌더러/콜라이더/메시가 없습니다.");
            return;
        }

        // 7) 타일 크기에 맞춘 스케일
        Vector3 targetSize = (_stepU > 0f && _stepV > 0f)
            ? new Vector3(sizeTiles.x * _stepU * footprintPadding, selB.size.y, sizeTiles.y * _stepV * footprintPadding)
            : new Vector3(selB.size.x * footprintPadding, selB.size.y, selB.size.z * footprintPadding);

        if (bd.tileWidth != bd.tileHeight)
        {
            // Uniform 스케일 = 원래 스케일 × 보정
            modelInstance.transform.localScale = originalScale;
            if (modelBounds.size.x > 0f && modelBounds.size.z > 0f)
            {
                float s = Mathf.Min(targetSize.x / modelBounds.size.x, targetSize.z / modelBounds.size.z);
                modelInstance.transform.localScale = originalScale * s;
            }
        }
        else
        {
            if (fillBothAxes)
            {
                modelInstance.transform.localScale = originalScale;
                if (modelBounds.size.x > 0f && modelBounds.size.z > 0f)
                {
                    float sx = targetSize.x / modelBounds.size.x;
                    float sz = targetSize.z / modelBounds.size.z;
                    float sy = Mathf.Min(sx, sz);
                    modelInstance.transform.localScale = new Vector3(originalScale.x * sx,
                                                                     originalScale.y * sy,
                                                                     originalScale.z * sz);
                }
            }
            else
            {
                modelInstance.transform.localScale = originalScale;
                if (modelBounds.size.x > 0f && modelBounds.size.z > 0f)
                {
                    float s = Mathf.Min(targetSize.x / modelBounds.size.x, targetSize.z / modelBounds.size.z);
                    modelInstance.transform.localScale = originalScale * s;
                }
            }
        }

        // 8) 중심/바닥 정렬
        AlignPreviewToSelection(selB);

        // 9) 표시
        modelInstance.SetActive(true);
        buildingInstallPanel?.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;

        // 10) 자동보정 ON이면 최종 회전을 previewRotation에 동기화
        if (autoAlignRotationToSelection)
            previewRotation = desiredRot;

        Debug.Log($"[SpawnPreview] prevRot={previewRotation}, desiredRot={desiredRot}, sizeTiles={sizeTiles}, origScale={originalScale}");
    }


    void ResizeToFitUniform(GameObject building, Vector3 targetSize, Bounds _unused)
    {
        var t = building.transform; t.localScale = Vector3.one;
        if (!TryGetModelBounds(building, out Bounds baseB)) return;
        Vector3 size = baseB.size; if (size.x <= 0f || size.z <= 0f) return;
        float s = Mathf.Min(targetSize.x / size.x, targetSize.z / size.z);
        t.localScale = new Vector3(s, s, s);
    }

    void ResizeToFitExact(GameObject building, Vector3 targetSize)
    {
        var t = building.transform; t.localScale = Vector3.one;
        if (!TryGetModelBounds(building, out Bounds baseB)) return;
        Vector3 size = baseB.size; if (size.x <= 0f || size.z <= 0f) return;
        float sx = targetSize.x / size.x;
        float sz = targetSize.z / size.z;
        float sy = Mathf.Min(sx, sz);
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

    public void ConfirmInstall()
    {
        if (previewInstance == null || currentTiles == null) return;

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) return;

        BuildingData buildingData = modelInstance.GetComponent<BuildingData>() ??
                                    modelInstance.GetComponentInChildren<BuildingData>();
        if (buildingData == null) return;

        if (previewInstance.tag != "Building")
        {
            try { previewInstance.tag = "Building"; }
            catch (UnityException e) { Debug.LogWarning($"[TileClickInstaller] 'Building' 태그 설정 실패: {e.Message}"); }
        }

        previewInstance.transform.SetParent(currentTiles[0].transform, true);

        foreach (var tile in currentTiles)
            if (tile.transform.Find(occupiedMarkerName) == null)
                new GameObject(occupiedMarkerName).transform.SetParent(tile.transform, false);

        var footprint = previewInstance.AddComponent<BuildingFootprint>();
        footprint.Init(currentTiles, occupiedMarkerName);

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

        ClosePlacementUI();
        TogglePlacementFX(false);
        SuppressUINavEvents(false);
        SetExternalHotkeysEnabled(true);
        SFXPlayer.Instance?.PlayClick();
    }

    void ClosePlacementUI()
    {
        previewInstance = null;
        modelInstance = null;
        currentTiles = null;

        if (buildingInstallPanel) buildingInstallPanel.SetActive(false);
        TogglePlacementFX(false);
    }

    void DiscardPreviewAndCloseUI()
    {
        if (previewInstance) Destroy(previewInstance);
        previewInstance = null;
        modelInstance = null;
        currentTiles = null;

        if (buildingInstallPanel) buildingInstallPanel.SetActive(false);
        TogglePlacementFX(false);
    }

    void NotifyCitizensOfNewBuilding()
    {
        foreach (var c in FindObjectsOfType<CitizenWanderer>())
            c.OnNewBuildingInstalled();
    }

    public void CancelInstall()
    {
        if (previewInstance != null) Destroy(previewInstance);
        DiscardPreviewAndCloseUI();
        GameManager.Instance?.CancelPlacing();
        TogglePlacementFX(false);
        SuppressUINavEvents(false);
        SetExternalHotkeysEnabled(true);
        SFXPlayer.Instance?.PlayClick();
    }

    // GameManager에서 호출할 수 있게 public 유지
    public void ClearSelection()
    {
        if (previewInstance != null) Destroy(previewInstance);
        selectedBuildingPrefab = null;

        TogglePlacementFX(false);
        SuppressUINavEvents(false);
        SetExternalHotkeysEnabled(true);
    }

    void OnDisable() { SuppressUINavEvents(false); SetExternalHotkeysEnabled(true); }
    void OnDestroy() { SuppressUINavEvents(false); SetExternalHotkeysEnabled(true); }

    // ─────────────────────────────────────────────────────────────
    // Utils
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
        if (tiles == null || tiles.Count == 0) return false;
        foreach (var t in tiles)
        {
            if (t == null) return false;
            if (t.transform.Find(occupiedMarkerName) != null) return false;
        }
        return true;
    }

    void HighlightTiles(List<GameObject> tiles, bool valid)
    {
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
        _mpbHighlight.SetColor(BaseColorID2, col);
        _mpbHighlight.SetColor(ColorID, col);

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
    // ───── TileClickInstaller 맨 아래쪽에 추가 ─────
    public Transform CurrentPreviewRoot => previewInstance ? previewInstance.transform : null;
    public BuildingData CurrentBuildingData
        => modelInstance ? (modelInstance.GetComponent<BuildingData>() ?? modelInstance.GetComponentInChildren<BuildingData>()) : null;

}
