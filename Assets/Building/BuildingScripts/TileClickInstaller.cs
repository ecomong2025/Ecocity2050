using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TileClickInstaller : MonoBehaviour
{
    // ====== 설정 ======
    [Header("Placement Settings")]
    [Tooltip("타일 점유를 표시할 마커 이름")]
    public string occupiedMarkerName = "__OCCUPIED__";

    [Header("Hotkeys")]
    public bool enableHotkeys = true;
    public bool requirePlacingForHotkeys = true;
    public KeyCode rotateKey = KeyCode.Space;
    public enum PivotMode { Center, PivotTile }
    [Header("Pivot Mode")] public PivotMode pivotMode = PivotMode.PivotTile;

    [Header("Scaling")]
    [Range(0.90f, 1.10f)] public float footprintPadding = 1.00f; // 설치 면적 패딩
    public bool fillBothAxes = true;  // true: U/V 각각 맞춤(왜곡 허용), false: 균등 스케일
    public bool keepHeight = true;    // true: Y 스케일 고정(추천)

    // === Tile edge (타일 둘레 초록 라인) ===
    [Header("Tile Edge Lines")]
    [Tooltip("각 Tile의 자식 중 '테두리 라인'이 들어있는 자식 이름")]
    public string tileEdgeChildName = "TileEdge";

    readonly List<Renderer> _tileEdgeRenderers = new();

    // === Drag Select ===
    [Header("Drag Select")]
    public LayerMask tileLayerMask = ~0; // Tile 레이어 추천
    public Material highlightMat;
    public Color highlightValid = new Color(0.2f, 1f, 0.4f, 0.35f);
    public Color highlightInvalid = new Color(1f, 0.25f, 0.25f, 0.35f);
    public float highlightYOffset = 0.01f;
    public Material lineMat;

    // === Grid Lines(선택) ===
    [Header("Grid Lines (Optional)")]
    public Transform gridLinesRoot;
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
    private GameObject previewInstance;   // 회전 중심 부모(Holder)
    private GameObject modelInstance;     // 실제 모델(자식)
    private float previewRotation = 0f;   // 0/90/180/270
    private List<GameObject> currentTiles;

    // 그리드 정보 캐시
    Vector3 _gridU, _gridV;
    float _stepU, _stepV;
    int _signU, _signV;
    GameObject _pivotTile;
    GameObject _dirTile;

    [Header("Grid Lines (toggle while placing)")]
    public string gridLineTag = "GridLine";

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
            if (edge != null)
            {
                _tileEdgeRenderers.AddRange(edge.GetComponentsInChildren<Renderer>(true));
            }
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
        foreach (var r in _tileEdgeRenderers)
            if (r) r.enabled = on;
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
        if (gridLinesRoot) { gridLinesRoot.gameObject.SetActive(on); return; }
        foreach (var lr in _gridLineRenderers) if (lr) lr.enabled = on;
        foreach (var r in _gridRenderers) if (r) r.enabled = on;
    }

    bool IsPlacingNow()
    {
        return selectedBuildingPrefab != null
               && buildingInstallPanel != null
               && buildingInstallPanel.activeInHierarchy;
    }

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

        CacheGridLines();
        SetGridLinesVisible(false);
        _gridShown = false;
        CacheTileEdgeLines();
        TogglePlacementFX(false);
        ClearHighlight();
        CacheGridLines();
        SetGridLinesVisible(false);
        _gridShown = false;
    }

    void Update()
    {
        bool placing = IsPlacingNow();
        if (placing != _gridShown) TogglePlacementFX(placing);

        bool shouldShowGrid = IsPlacingNow();
        if (shouldShowGrid != _gridShown)
        {
            SetGridLinesVisible(shouldShowGrid);
            _gridShown = shouldShowGrid;
            if (!shouldShowGrid) { ClearHighlight(); HideSelectionLine(); }
        }

        if (selectedBuildingPrefab == null) return;
        if (enableHotkeys && (!requirePlacingForHotkeys || placing))
            if (Input.GetKeyDown(rotateKey)) RotatePreview();

        if (!placing) return;

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
                        SpawnPreviewOverSelection(selectedBuildingPrefab);
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

        if (!IsPlacingNow() && highlightedTiles.Count > 0)
            ClearHighlight();
    }

    void HideSelectionLine() { /* 선택 라인 쓰면 여기서 끄기 */ }

    // ─────────────────────────────────────────────────────────────
    // 그리드 사각형 찾기
    List<GameObject> FindTilesRectangleOnGrid(
        GameObject baseTile, int width, int height, GameObject dragTile,
        out Vector3 u, out Vector3 vAxis, out float stepU, out float stepV, out int signU, out int signV)
    {
        u = vAxis = Vector3.zero; stepU = stepV = 0f; signU = signV = 1;

        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        Vector3 basePos = baseTile.transform.position;

        var neigh = new List<Vector3>();
        foreach (var t in allTiles)
        {
            if (t == baseTile) continue;
            Vector3 vv = t.transform.position - basePos; vv.y = 0;
            if (vv.sqrMagnitude > 0.0001f) neigh.Add(vv);
        }
        if (neigh.Count == 0) return null;
        neigh.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));

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

        Vector3 dragDir = dragTile.transform.position - basePos; dragDir.y = 0;
        int signUVal = Vector3.Dot(dragDir, uNorm) >= 0 ? 1 : -1;
        int signVVal = Vector3.Dot(dragDir, vNorm) >= 0 ? 1 : -1;

        float tolerance = 0.45f * Mathf.Min(stepUVal, stepVVal);

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

        TogglePlacementFX(true);
        SFXPlayer.Instance?.PlayClick();
    }

    void RotatePreview()
    {
        if (selectedBuildingPrefab == null) return;

        SFXPlayer.Instance?.PlayClick();
        previewRotation = (previewRotation + 90f) % 360f;
        int snapped = Mathf.RoundToInt(Mathf.Repeat(previewRotation, 360f) / 90f) * 90;

        var bd = selectedBuildingPrefab.GetComponent<BuildingData>() ??
                 selectedBuildingPrefab.GetComponentInChildren<BuildingData>();
        if (bd == null) return;

        var size = GetRotatedSize(bd.tileWidth, bd.tileHeight, snapped);

        List<GameObject> rectTiles = null;
        bool valid = false;

        if (pivotMode == PivotMode.PivotTile && _pivotTile != null && _stepU > 0f && _stepV > 0f)
        {
            GetRotatedAxes(snapped, out var uR, out var vR, out float stepUR, out float stepVR);

            GameObject baseTile = _pivotTile;
            GameObject dragTile = FindNearestTileToWorld(
                baseTile.transform.position + uR * stepUR + vR * stepVR
            );

            rectTiles = FindTilesRectangleOnGrid(
                baseTile, size.x, size.y, dragTile,
                out _gridU, out _gridV, out _stepU, out _stepV, out _signU, out _signV
            );

            valid = rectTiles != null && rectTiles.Count == size.x * size.y && AllTilesFree(rectTiles);
        }
        else
        {
            if (currentTiles != null && currentTiles.Count > 0 && _stepU > 0f && _stepV > 0f)
            {
                Bounds selB = GetTilesBounds(currentTiles);
                Vector3 C = new Vector3(selB.center.x, 0f, selB.center.z);

                float bestErr = float.PositiveInfinity;
                List<GameObject> best = null; bool bestValid = false;

                int[] s = { +1, -1 };
                foreach (var su in s)
                    foreach (var sv in s)
                    {
                        GameObject bt, dt; float err;
                        var rect = BuildRectCentered(C, size.x, size.y, su, sv, out bt, out dt, out err);
                        bool ok = rect != null && rect.Count == size.x * size.y && AllTilesFree(rect);
                        if (ok && err < bestErr) { best = rect; bestErr = err; bestValid = true; }
                        else if (!bestValid && rect != null && err < bestErr) { best = rect; bestErr = err; }
                    }
                rectTiles = best; valid = bestValid;
            }
            else if (_pivotTile != null)
            {
                var dirTile = _dirTile != null ? _dirTile : _pivotTile;
                rectTiles = FindTilesRectangleOnGrid(
                    _pivotTile, size.x, size.y, dirTile,
                    out _gridU, out _gridV, out _stepU, out _stepV, out _signU, out _signV
                );
                valid = rectTiles != null && rectTiles.Count == size.x * size.y && AllTilesFree(rectTiles);
            }
            ApplyPreviewYaw();   // 이미 떠 있는 프리뷰가 있으면 각도만 갱신
        }

        HighlightTiles(rectTiles, valid);
        if (buildingInstallPanel) buildingInstallPanel.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = valid;

        if (rectTiles != null)
        {
            currentTiles = rectTiles;
            if (valid)
            {
                SpawnPreviewOverSelection(selectedBuildingPrefab);
                HighlightTiles(currentTiles, true);
            }
            else if (previewInstance != null)
            {
                previewInstance.transform.rotation = Quaternion.Euler(0f, snapped, 0f);
            }
        }
    }

    Bounds GetTilesBounds(List<GameObject> tiles)
    {
        var r0 = tiles[0].GetComponent<Renderer>();
        Bounds b = r0 ? r0.bounds : new Bounds(tiles[0].transform.position, Vector3.zero);
        for (int i = 1; i < tiles.Count; i++)
        {
            var r = tiles[i].GetComponent<Renderer>();
            if (r) b.Encapsulate(r.bounds);
            else b.Encapsulate(tiles[i].transform.position);
        }
        return b;
    }

    List<GameObject> BuildRectCentered(
        Vector3 C, int w, int h, int signU, int signV,
        out GameObject baseTile, out GameObject dragTile, out float centerError)
    {
        Vector3 uHat = _gridU.normalized;
        Vector3 vHat = _gridV.normalized;
        float stepUabs = Mathf.Abs(_stepU);
        float stepVabs = Mathf.Abs(_stepV);

        Vector3 minTarget =
            C - uHat * stepUabs * ((w - 1) * 0.5f) * signU
              - vHat * stepVabs * ((h - 1) * 0.5f) * signV;

        baseTile = FindNearestTileToWorld(minTarget);
        dragTile = FindNearestTileToWorld(minTarget + uHat * stepUabs * signU + vHat * stepVabs * signV);

        var rect = FindTilesRectangleOnGrid(
            baseTile, w, h, dragTile,
            out _gridU, out _gridV, out _stepU, out _stepV, out _signU, out _signV
        );

        if (rect != null && rect.Count == w * h)
        {
            Bounds b = GetTilesBounds(rect);
            Vector3 cc = new Vector3(b.center.x, 0f, b.center.z);
            centerError = Vector3.Distance(cc, C);
        }
        else centerError = float.PositiveInfinity;

        return rect;
    }

    GameObject FindNearestTileToWorld(Vector3 pos)
    {
        GameObject closest = null; float best = float.MaxValue;
        var tiles = GameObject.FindGameObjectsWithTag("Tile");
        foreach (var t in tiles)
        {
            float d = (t.transform.position - pos).sqrMagnitude;
            if (d < best) { best = d; closest = t; }
        }
        return closest;
    }

    List<GameObject> BuildRectCenteredRotated(
        Vector3 C, int w, int h, int rotSteps, out float centerError)
    {
        Vector3 uHat = _gridU.normalized;
        Vector3 vHat = _gridV.normalized;
        float sU = Mathf.Abs(_stepU);
        float sV = Mathf.Abs(_stepV);

        Vector3 uR, vR; float stepUR, stepVR;
        switch (rotSteps & 3)
        {
            case 0: uR = uHat; vR = vHat; stepUR = sU; stepVR = sV; break;
            case 1: uR = vHat; vR = -uHat; stepUR = sV; stepVR = sU; break;
            case 2: uR = -uHat; vR = -vHat; stepUR = sU; stepVR = sV; break;
            default: uR = -vHat; vR = uHat; stepUR = sV; stepVR = sU; break;
        }

        Vector3 minTarget =
            C - uR * stepUR * ((w - 1) * 0.5f)
              - vR * stepVR * ((h - 1) * 0.5f);

        var allTiles = GameObject.FindGameObjectsWithTag("Tile");
        float tol = 0.45f * Mathf.Min(stepUR, stepVR);

        var rect = new List<GameObject>(w * h);
        for (int iu = 0; iu < w; iu++)
        {
            for (int iv = 0; iv < h; iv++)
            {
                Vector3 target = minTarget + uR * stepUR * iu + vR * stepVR * iv;

                GameObject closest = null; float minD = float.MaxValue;
                foreach (var t in allTiles)
                {
                    float d = Vector3.Distance(t.transform.position, target);
                    if (d < tol && d < minD) { minD = d; closest = t; }
                }
                if (!closest) { centerError = float.PositiveInfinity; return null; }
                rect.Add(closest);
            }
        }

        Bounds b = GetTilesBounds(rect);
        Vector3 cc = new Vector3(b.center.x, 0f, b.center.z);
        centerError = Vector3.Distance(cc, C);
        return rect;
    }

    void GetRotatedAxes(int snappedDeg, out Vector3 uR, out Vector3 vR, out float stepUR, out float stepVR)
    {
        Vector3 u = _gridU.normalized;
        Vector3 v = _gridV.normalized;
        float sU = Mathf.Abs(_stepU);
        float sV = Mathf.Abs(_stepV);

        switch (((snappedDeg / 90) % 4 + 4) % 4)
        {
            case 0: uR = u; vR = v; stepUR = sU; stepVR = sV; break;
            case 1: uR = v; vR = -u; stepUR = sV; stepVR = sU; break;
            case 2: uR = -u; vR = -v; stepUR = sU; stepVR = sV; break;
            default: uR = -v; vR = u; stepUR = sV; stepVR = sU; break;
        }
    }

    GameObject GetCornerMinMin(List<GameObject> tiles, Vector3 uPos, Vector3 vPos)
    {
        GameObject best = null;
        float bestU = float.MaxValue, bestV = float.MaxValue;
        const float eps = 1e-4f;

        foreach (var t in tiles)
        {
            var p = t.transform.position;
            float du = Vector3.Dot(p, uPos);
            float dv = Vector3.Dot(p, vPos);

            if (du < bestU - eps || (Mathf.Abs(du - bestU) <= eps && dv < bestV))
            { best = t; bestU = du; bestV = dv; }
        }
        return best != null ? best : tiles[0];
    }

    // 0/90/180/270 스냅
    int Snap90(float deg) => Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 90f) * 90;

    // 그리드 프레임/스텝/중심/길이 추정
    bool TryComputeSelectionFrame(out Vector3 uHat, out Vector3 vHat, out float stepU, out float stepV,
                                  out Vector3 center, out float lenU, out float lenV)
    {
        uHat = Vector3.right; vHat = Vector3.forward; stepU = 1f; stepV = 1f;
        center = Vector3.zero; lenU = 1f; lenV = 1f;
        if (currentTiles == null || currentTiles.Count == 0) return false;

        if (_gridU.sqrMagnitude > 1e-6f && _gridV.sqrMagnitude > 1e-6f)
        {
            uHat = _gridU.normalized;
            vHat = _gridV.normalized;
        }
        else
        {
            Vector3 a = currentTiles[0].transform.position, b = a; float best = -1f;
            for (int i = 0; i < currentTiles.Count; i++)
            {
                var pi = currentTiles[i].transform.position; pi.y = 0;
                for (int j = i + 1; j < currentTiles.Count; j++)
                {
                    var pj = currentTiles[j].transform.position; pj.y = 0;
                    float d2 = (pi - pj).sqrMagnitude;
                    if (d2 > best) { best = d2; a = pi; b = pj; }
                }
            }
            uHat = (b - a); uHat.y = 0; if (uHat.sqrMagnitude < 1e-6f) uHat = Vector3.forward; uHat.Normalize();
            vHat = Vector3.Cross(Vector3.up, uHat).normalized;
        }
        if (Vector3.Dot(Vector3.Cross(uHat, vHat), Vector3.up) < 0f) vHat = -vHat;

        float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
        float minV = float.PositiveInfinity, maxV = float.NegativeInfinity;
        var projU = new List<float>(currentTiles.Count);
        var projV = new List<float>(currentTiles.Count);

        foreach (var t in currentTiles)
        {
            Vector3 p = t.transform.position; p.y = 0;
            float pu = Vector3.Dot(p, uHat);
            float pv = Vector3.Dot(p, vHat);
            projU.Add(pu); projV.Add(pv);
            if (pu < minU) minU = pu; if (pu > maxU) maxU = pu;
            if (pv < minV) minV = pv; if (pv > maxV) maxV = pv;
        }

        float EstimateStep(List<float> xs)
        {
            if (xs.Count < 2) return 1f;
            xs.Sort();
            var diffs = new List<float>();
            for (int i = 1; i < xs.Count; i++) { float d = xs[i] - xs[i - 1]; if (d > 1e-4f) diffs.Add(d); }
            if (diffs.Count == 0) return 1f;
            diffs.Sort();
            int m = diffs.Count / 2;
            return (diffs.Count % 2 == 1) ? diffs[m] : 0.5f * (diffs[m - 1] + diffs[m]);
        }

        stepU = (_stepU > 1e-6f) ? _stepU : EstimateStep(new List<float>(projU));
        stepV = (_stepV > 1e-6f) ? _stepV : EstimateStep(new List<float>(projV));

        Vector3 minCorner = (uHat * (minU - 0.5f * stepU)) + (vHat * (minV - 0.5f * stepV));
        lenU = (maxU - minU) + stepU;
        lenV = (maxV - minV) + stepV;
        center = minCorner + uHat * (lenU * 0.5f) + vHat * (lenV * 0.5f);

        return true;
    }

    // 모델 바닥을 targetY에 맞추기
    void RaiseBottomTo(GameObject go, float targetY)
    {
        if (!TryGetModelBounds(go, out Bounds b)) return;
        float dy = targetY - b.min.y;
        if (Mathf.Abs(dy) > 1e-6f) go.transform.position += new Vector3(0f, dy, 0f);
    }

    void SpawnPreviewOverSelection(GameObject prefab)
    {
        if (previewInstance) Destroy(previewInstance);
        modelInstance = null;
        if (prefab == null || currentTiles == null || currentTiles.Count == 0) return;

        if (!TryComputeSelectionFrame(out var uHat, out var vHat, out var stepU, out var stepV,
                                       out var selCenter, out var lenU, out var lenV))
            return;

        Bounds selB = GetTilesBounds(currentTiles);
        float topY = selB.max.y;

        // 기본 회전 (셀 방향 기준 회전)
        int snapped = Snap90(previewRotation);
        Quaternion rot = Quaternion.LookRotation(vHat.normalized, Vector3.up) * Quaternion.Euler(0f, snapped, 0f);

        // 프리뷰 홀더 생성
        previewInstance = new GameObject("PreviewHolder");
        previewInstance.transform.SetPositionAndRotation(
            new Vector3(selCenter.x, topY, selCenter.z),
            rot
        );

        // 프리팹 인스턴스 생성
        modelInstance = Instantiate(prefab, previewInstance.transform);
        modelInstance.name = "BuildingModel";
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        var bd = prefab.GetComponent<BuildingData>() ?? prefab.GetComponentInChildren<BuildingData>();
        if (bd != null)
        {
            // 회전에 따른 셀 크기 보정
            Vector2Int size = GetRotatedSize(bd.tileWidth, bd.tileHeight, snapped);
            float targetU = Mathf.Max(1, size.x) * stepU * footprintPadding;
            float targetV = Mathf.Max(1, size.y) * stepV * footprintPadding;

            // 초기 스케일 유지
            modelInstance.transform.localScale = prefab.transform.localScale;

            // 원래 바운즈 측정
            MeasureOrientedBounds(modelInstance, previewInstance.transform,
                out float lenU0, out float lenV0, out float minY0, out float maxY0, out Vector3 center0);

            // 스케일 팩터 계산
            float scaleU = (lenU0 > 1e-4f) ? targetU / lenU0 : 1f;
            float scaleV = (lenV0 > 1e-4f) ? targetV / lenV0 : 1f;
            float scaleY = keepHeight ? 1f : Mathf.Min(scaleU, scaleV);

            modelInstance.transform.localScale = new Vector3(
                modelInstance.transform.localScale.x * scaleU,
                modelInstance.transform.localScale.y * scaleY,
                modelInstance.transform.localScale.z * scaleV
            );

            // 다시 바운즈 측정해서 위치 보정
            MeasureOrientedBounds(modelInstance, previewInstance.transform,
                out _, out _, out float minY1, out _, out Vector3 center1);

            Vector3 deltaXZ = new Vector3(selCenter.x - center1.x, 0f, selCenter.z - center1.z);
            float dy = (topY + 0.001f) - minY1;

            modelInstance.transform.position += deltaXZ + new Vector3(0f, dy, 0f);
        }

        buildingInstallPanel?.SetActive(true);
        if (confirmInstallButton) confirmInstallButton.interactable = true;
        HighlightTiles(currentTiles, true);
    }



    void MeasureOrientedBounds(GameObject go, Transform holder,
                           out float lenU, out float lenV,
                           out float minY, out float maxY,
                           out Vector3 centerWorld)
    {
        Vector3 u = holder.right.normalized;
        Vector3 v = holder.forward.normalized;

        float minUacc = float.PositiveInfinity, maxUacc = float.NegativeInfinity;
        float minVacc = float.PositiveInfinity, maxVacc = float.NegativeInfinity;
        float minYacc = float.PositiveInfinity, maxYacc = float.NegativeInfinity;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            lenU = lenV = 0f;
            minY = maxY = go.transform.position.y;
            centerWorld = go.transform.position;
            return;
        }

        foreach (var r in renderers)
        {
            Bounds lb = r.localBounds;
            Vector3 c = lb.center;
            Vector3 e = lb.extents;

            for (int xi = -1; xi <= 1; xi += 2)
                for (int yi = -1; yi <= 1; yi += 2)
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 localCorner = c + new Vector3(xi * e.x, yi * e.y, zi * e.z);
                        Vector3 w = r.transform.TransformPoint(localCorner);

                        float du = Vector3.Dot(w, u);
                        float dv = Vector3.Dot(w, v);
                        float yy = w.y;

                        if (du < minUacc) minUacc = du; if (du > maxUacc) maxUacc = du;
                        if (dv < minVacc) minVacc = dv; if (dv > maxVacc) maxVacc = dv;
                        if (yy < minYacc) minYacc = yy; if (yy > maxYacc) maxYacc = yy;
                    }
        }

        lenU = maxUacc - minUacc;
        lenV = maxVacc - minVacc;
        minY = minYacc;
        maxY = maxYacc;

        float cU = 0.5f * (minUacc + maxUacc);
        float cV = 0.5f * (minVacc + maxVacc);
        float cY = 0.5f * (minYacc + maxYacc);
        centerWorld = u * cU + v * cV + Vector3.up * cY;
    }


    // 선택영역 상단에 Y만 맞춤(보조용 – 현재는 사용 안 함)
    void AlignPreviewYToSelectionTop(Bounds selectionBounds)
    {
        if (!TryGetModelBounds(modelInstance, out Bounds b)) return;
        float dy = selectionBounds.max.y - b.min.y;
        modelInstance.transform.position += new Vector3(0f, dy, 0f);
    }

    void ApplyPreviewYaw()
    {
        if (!previewInstance) return;

        int snapped = Snap90(previewRotation);

        if (_gridU.sqrMagnitude > 1e-6f)
        {
            // right == +U, forward == +V 가 되도록 basis 구성
            Vector3 uB = _gridU.normalized;
            Vector3 vB = -Vector3.Cross(Vector3.up, uB).normalized;
            Quaternion basis = Quaternion.LookRotation(vB, Vector3.up);
            previewInstance.transform.rotation = basis * Quaternion.Euler(0f, snapped, 0f);
        }
        else
        {
            // 축 정보를 못 잡은 경우: 그냥 Y축 스냅 회전만
            previewInstance.transform.rotation = Quaternion.Euler(0f, snapped, 0f);
        }
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
        if (!IsPlacingNow()) { ClearHighlight(); return; }

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

    // (보존) 세계 X/Z 기준 직사각형
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

    // 선택 영역 바운즈(보조)
    void FitModelXZToSelectionBounds(GameObject model, Bounds selB, bool exactBothAxes, float padding)
    {
        if (!TryGetModelBounds(model, out Bounds b)) return;

        float targetX = selB.size.x * padding;
        float targetZ = selB.size.z * padding;

        if (b.size.x < 1e-5f || b.size.z < 1e-5f) return;
        Vector3 curLocal = model.transform.localScale;

        float sx = targetX / b.size.x;
        float sz = targetZ / b.size.z;

        if (exactBothAxes)
        {
            float yFactor = keepHeight ? 1f : Mathf.Min(sx, sz);
            model.transform.localScale = new Vector3(curLocal.x * sx, curLocal.y * yFactor, curLocal.z * sz);
        }
        else
        {
            float s = Mathf.Min(sx, sz);
            float yFactor = keepHeight ? 1f : s;
            model.transform.localScale = new Vector3(curLocal.x * s, curLocal.y * yFactor, curLocal.z * s);
        }

        if (TryGetModelBounds(model, out b))
        {
            Vector3 deltaXZ = new Vector3(selB.center.x - b.center.x, 0f, selB.center.z - b.center.z);
            model.transform.position += deltaXZ;
        }
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

        foreach (var tile in currentTiles)
            if (tile && tile.transform.Find(occupiedMarkerName) == null)
                new GameObject(occupiedMarkerName).transform.SetParent(tile.transform, false);

        var footprint = previewInstance.GetComponent<BuildingFootprint>() ?? previewInstance.AddComponent<BuildingFootprint>();
        footprint.Init(currentTiles, occupiedMarkerName);

        var buildingData = previewInstance.GetComponent<BuildingData>() ??
                           previewInstance.GetComponentInChildren<BuildingData>();
        if (buildingData)
        {
            int totalCO2Impact = buildingData.instantCO2Change;
            if (buildingData.co2PerSecond != 0) totalCO2Impact += buildingData.maxCO2Change;
            int incomePerMinute = (buildingData.incomePer5Minutes > 0) ? buildingData.incomePer5Minutes / 5 : 0;

            GameManager.Instance?.AddBuilding(
                selectedBuildingPrefab.name.Replace("Prefab", ""),
                buildingData.cost,
                totalCO2Impact,
                incomePerMinute,
                previewInstance.transform.position,
                previewInstance
            );

            GameManager.Instance?.ApplyBuildingCost(
                buildingData.cost,
                buildingData.instantCO2Change,
                buildingData.co2PerSecond,
                buildingData.maxCO2Change,
                buildingData.incomePer5Minutes,
                buildingData.transform,
                buildingData.maxIncomeAmount
            );
        }

        YearQuestManager.Instance?.OnBuildingInstalled(selectedBuildingPrefab, buildingData);
        NotifyCitizensOfNewBuilding();
        FindObjectOfType<CitizenGroupController>()?.OnBuildingInstalled(previewInstance.transform.position);

        GameManager.Instance?.CompletePlacing();
        ClearHighlight();
        SetGridLinesVisible(false);
        _gridShown = false;
        buildingInstallPanel?.SetActive(false);
    }

    void ClearPreviewAndPanel(bool destroyPreview = true)
    {
        if (destroyPreview && previewInstance != null)
            Destroy(previewInstance);

        previewInstance = null;
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

        TogglePlacementFX(false);
        SFXPlayer.Instance?.PlayClick();
    }

    public void ClearSelection()
    {
        if (previewInstance != null) Destroy(previewInstance);
        selectedBuildingPrefab = null;
        ClearPreviewAndPanel(true);

        TogglePlacementFX(false);
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
}
