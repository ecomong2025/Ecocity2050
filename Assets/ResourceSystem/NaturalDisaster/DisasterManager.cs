using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DisasterManager : MonoBehaviour
{
    [Header("SFX")]
    [SerializeField] private AudioClip collapseSfx;
    [SerializeField, Range(0f, 1f)] private float collapseVolume = 1f;

    // 요구사항: '나쁨' 10초, '매우 나쁨' 5초
    public float normalDisasterInterval = 10f;
    public float severeDisasterInterval = 5f;

    [Header("FX (looping prefabs)")]
    [Tooltip("가뭄 FX 프리팹 (루프)")]
    [SerializeField] private GameObject fxDroughtPrefab;
    [Tooltip("화재 FX 프리팹 (루프)")]
    [SerializeField] private GameObject fxFirePrefab;
    [Tooltip("폭우/홍수 FX 프리팹 (루프)")]
    [SerializeField] private GameObject fxRainPrefab;
    [Tooltip("태풍/강풍 FX 프리팹 (루프)")]
    [SerializeField] private GameObject fxStormPrefab;

    [Tooltip("타일 중심에서의 오프셋 (살짝 띄우기)")]
    [SerializeField] private Vector3 fxOffset = new Vector3(0f, 0.05f, 0f);
    [Tooltip("파괴 연출(깜빡임) 중 FX를 유지하는 시간(초)")]
    [SerializeField] private float fxGraceSeconds = 0.35f;

    private GameManager gameManager;
    private Coroutine _disasterCoroutine;
    private float _currentInterval = -1f;

    // 재난 1회 동안 생성된 FX 인스턴스들 추적
    private readonly List<GameObject> _activeFX = new();

    void Awake()
    {
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
            Debug.LogError("[DisasterManager] GameManager를 찾을 수 없습니다.");
    }

    void OnEnable()
    {
        GameManager.OnSatisfactionChanged += OnSatisfactionChanged;
    }

    void OnDisable()
    {
        GameManager.OnSatisfactionChanged -= OnSatisfactionChanged;
        StopDisasterTimer();
    }

    void Start()
    {
        if (gameManager != null)
            UpdateTimerForStatus(gameManager.GetSatisfactionLevel());
    }

    private void OnSatisfactionChanged(string newStatus) => UpdateTimerForStatus(newStatus);

    private void UpdateTimerForStatus(string status)
    {
        if (status == "매우 나쁨")      StartDisasterTimer(severeDisasterInterval);
        else if (status == "나쁨")      StartDisasterTimer(normalDisasterInterval);
        else                           StopDisasterTimer();
    }

    private void StartDisasterTimer(float interval)
    {
        if (_disasterCoroutine != null && Mathf.Approximately(_currentInterval, interval)) return;
        StopDisasterTimer();
        _currentInterval = interval;
        _disasterCoroutine = StartCoroutine(DisasterLoop(interval));
        Debug.Log($"[DisasterManager] 재난 타이머 시작 interval={interval}s");
    }

    private void StopDisasterTimer()
    {
        if (_disasterCoroutine != null)
        {
            StopCoroutine(_disasterCoroutine);
            _disasterCoroutine = null;
            _currentInterval = -1f;
            Debug.Log("[DisasterManager] 재난 타이머 중지");
        }
    }

    private IEnumerator DisasterLoop(float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            if (gameManager == null) yield break;
            string status = gameManager.GetSatisfactionLevel();
            if (status == "나쁨" || status == "매우 나쁨")
                TriggerDisaster();
        }
    }

    void TriggerDisaster()
    {
        GameObject[] tiles = GameObject.FindGameObjectsWithTag("Tile");
        List<GameObject> tilesWithBuildings = new();

        foreach (GameObject tile in tiles)
        {
            BuildingData buildingData = FindBuildingDataInChildren(tile.transform);
            if (buildingData != null)
                tilesWithBuildings.Add(buildingData.gameObject);
        }

        if (tilesWithBuildings.Count == 0)
        {
            Debug.Log("[DisasterManager] 재난으로 제거할 건물이 없습니다.");
            return;
        }

        string[] disasterTypes = { "가뭄", "화재", "폭우", "태풍" };
        string selectedDisaster = disasterTypes[Random.Range(0, disasterTypes.Length)];

        int index = Random.Range(0, tilesWithBuildings.Count);
        GameObject buildingToDestroy = tilesWithBuildings[index];

        Debug.Log($"🚨 {selectedDisaster} 발생! {buildingToDestroy.name} 건물이 파괴됩니다...");

        // 수입 정지
        if (GameManager.Instance != null)
            GameManager.Instance.StopIncomeForBuilding(buildingToDestroy.transform);

        // 뉴스
        if (GPTNewsGenerator.Instance != null)
            GPTNewsGenerator.Instance.ShowDisasterNews(selectedDisaster);

        // 효과음
        PlayCollapseSfx();

        // 🔸 타일들에 파티클 생성(루프 재생)
        var targetTiles = GetTilesForBuilding(buildingToDestroy);
        var fxPrefab = GetFXPrefab(selectedDisaster);
        SpawnFXOnTiles(targetTiles, fxPrefab);

        // 🔸 깜빡임 후 파괴 (파괴 직전에 FX 정지/정리)
        StartCoroutine(BlinkAndDestroy(buildingToDestroy, 2f, 6));
    }

    IEnumerator BlinkAndDestroy(GameObject building, float duration, int blinkCount)
    {
        if (building == null) yield break;

        Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < blinkCount; i++)
        {
            foreach (Renderer r in renderers) if (r) r.enabled = false;
            yield return new WaitForSeconds(duration / (blinkCount * 2));
            foreach (Renderer r in renderers) if (r) r.enabled = true;
            yield return new WaitForSeconds(duration / (blinkCount * 2));
        }

        // 🔹 FX를 먼저 멈추고 약간의 그레이스 타임 후 정리
        StopActiveFX();
        if (fxGraceSeconds > 0f) yield return new WaitForSeconds(fxGraceSeconds);
        CleanupActiveFX();

        // 🔹 타일 점유 해제 → 같은 자리 재설치 가능
        FreeTilesForBuilding(building);

        // 🔹 실제 제거
        Destroy(building);
    }

    void PlayCollapseSfx()
    {
        if (collapseSfx == null) return;
        var sfxPlayer = GameObject.Find("SFXPlayer");
        if (sfxPlayer)
        {
            var src = sfxPlayer.GetComponent<AudioSource>();
            if (src) src.PlayOneShot(collapseSfx, collapseVolume);
        }
        else
        {
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(collapseSfx, cam ? cam.transform.position : Vector3.zero, collapseVolume);
        }
    }

    // ───────────────────────────────
    // FX 유틸

    GameObject GetFXPrefab(string disaster)
    {
        switch (disaster)
        {
            case "가뭄": return fxDroughtPrefab   ? fxDroughtPrefab : fxStormPrefab;
            case "화재": return fxFirePrefab      ? fxFirePrefab    : fxStormPrefab;
            case "폭우": return fxRainPrefab      ? fxRainPrefab    : fxStormPrefab;
            case "태풍": return fxStormPrefab;
        }
        return fxStormPrefab;
    }

    List<Transform> GetTilesForBuilding(GameObject buildingRoot)
    {
        var result = new List<Transform>();

        // 1) 설치 시 부착된 Footprint로 멀티타일 지원
        var fp = buildingRoot.GetComponent<BuildingFootprint>() ??
                 buildingRoot.GetComponentInChildren<BuildingFootprint>() ??
                 buildingRoot.GetComponentInParent<BuildingFootprint>();
        if (fp != null && fp.Tiles != null)
        {
            foreach (var t in fp.Tiles) if (t) result.Add(t.transform);
            if (result.Count > 0) return result;
        }

        // 2) 폴백: 부모 타일 1개만
        var tile = FindTileAncestor(buildingRoot.transform);
        if (tile) result.Add(tile);
        return result;
    }

    void SpawnFXOnTiles(List<Transform> tiles, GameObject fxPrefab)
    {
        CleanupActiveFX(); // 혹시 이전 재난 찌꺼기 제거
        if (fxPrefab == null || tiles == null) return;

        foreach (var t in tiles)
        {
            if (!t) continue;
            var go = Instantiate(fxPrefab, t.position + fxOffset, Quaternion.identity, t);
            _activeFX.Add(go);

            // 자식 모든 파티클 재생(루프 전제)
            var psArray = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psArray) ps.Play(true);
        }
    }

    void StopActiveFX()
    {
        foreach (var fx in _activeFX)
        {
            if (!fx) continue;
            var psArray = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psArray) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void CleanupActiveFX()
    {
        foreach (var fx in _activeFX)
            if (fx) Destroy(fx);
        _activeFX.Clear();
    }

    // ───────────────────────────────
    // 점유 해제 유틸

    void FreeTilesForBuilding(GameObject buildingRoot)
    {
        if (!buildingRoot) return;

        var fp = buildingRoot.GetComponent<BuildingFootprint>() ??
                 buildingRoot.GetComponentInChildren<BuildingFootprint>() ??
                 buildingRoot.GetComponentInParent<BuildingFootprint>();

        if (fp != null) { fp.ReleaseAll(); return; }

        var tile = FindTileAncestor(buildingRoot.transform);
        if (tile != null)
        {
            TryRemoveMarker(tile, "__OCCUPIED__");
            string installerMarker = (TileClickInstaller.Instance != null)
                ? TileClickInstaller.Instance.occupiedMarkerName
                : "__OCCUPIED__";
            if (installerMarker != "__OCCUPIED__")
                TryRemoveMarker(tile, installerMarker);
        }
    }

    Transform FindTileAncestor(Transform t)
    {
        var cur = t;
        while (cur != null)
        {
            if (cur.CompareTag("Tile")) return cur;
            cur = cur.parent;
        }
        return null;
    }

    void TryRemoveMarker(Transform tile, string markerName)
    {
        if (!tile || string.IsNullOrEmpty(markerName)) return;
        var mark = tile.Find(markerName);
        if (mark) Destroy(mark.gameObject);
    }

    // ───────────────────────────────
    // 탐색 유틸

    BuildingData FindBuildingDataInChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            BuildingData data = child.GetComponent<BuildingData>();
            if (data != null) return data;

            BuildingData nested = FindBuildingDataInChildren(child);
            if (nested != null) return nested;
        }
        return null;
    }
}