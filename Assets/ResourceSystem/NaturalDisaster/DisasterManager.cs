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

    private GameManager gameManager;
    private Coroutine _disasterCoroutine;
    private float _currentInterval = -1f;

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
        // 시작 시 현재 상태에 따라 타이머 시작
        if (gameManager != null)
            UpdateTimerForStatus(gameManager.GetSatisfactionLevel());
    }

    // 이벤트 핸들러: 만족도 변화 시 호출
    private void OnSatisfactionChanged(string newStatus)
    {
        UpdateTimerForStatus(newStatus);
    }

    private void UpdateTimerForStatus(string status)
    {
        if (status == "매우 나쁨")
        {
            StartDisasterTimer(severeDisasterInterval);
        }
        else if (status == "나쁨")
        {
            StartDisasterTimer(normalDisasterInterval);
        }
        else
        {
            StopDisasterTimer();
        }
    }

    private void StartDisasterTimer(float interval)
    {
        if (_disasterCoroutine != null && Mathf.Approximately(_currentInterval, interval))
            return; // 이미 같은 간격으로 동작 중

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

            // 재난 발생 시점에 최신 만족도 확인 — 여전히 나쁨 계열이면 발생
            if (gameManager == null) yield break;
            string status = gameManager.GetSatisfactionLevel();
            if (status == "나쁨" || status == "매우 나쁨")
                TriggerDisaster();
        }
    }

    void TriggerDisaster()
    {
        GameObject[] tiles = GameObject.FindGameObjectsWithTag("Tile");
        List<GameObject> tilesWithBuildings = new List<GameObject>();

        foreach (GameObject tile in tiles)
        {
            BuildingData buildingData = FindBuildingDataInChildren(tile.transform);
            if (buildingData != null)
            {
                tilesWithBuildings.Add(buildingData.gameObject);
            }
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

        // 재난 발생 시 수입 코루틴 중지 (GameManager.Instance 가 있으면 호출)
        if (GameManager.Instance != null)
            GameManager.Instance.StopIncomeForBuilding(buildingToDestroy.transform);

        // 뉴스 출력 (있으면)
        if (GPTNewsGenerator.Instance != null)
            GPTNewsGenerator.Instance.ShowDisasterNews(selectedDisaster);

        // 효과음
        PlayCollapseSfx();

        // 깜박이고 파괴 — 파괴 후 그 자리는 빈 상태(다시 설치 가능)
        StartCoroutine(BlinkAndDestroy(buildingToDestroy, 2f, 6));
    }

    IEnumerator BlinkAndDestroy(GameObject building, float duration, int blinkCount)
    {
        if (building == null) yield break;

        Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < blinkCount; i++)
        {
            foreach (Renderer r in renderers) if (r != null) r.enabled = false;
            yield return new WaitForSeconds(duration / (blinkCount * 2));
            foreach (Renderer r in renderers) if (r != null) r.enabled = true;
            yield return new WaitForSeconds(duration / (blinkCount * 2));
        }

        // 🔹 파괴 직전: 타일 점유 해제(마커 제거)
        FreeTilesForBuilding(building);

        // 🔹 건물 오브젝트 제거 — 타일은 비워지므로 다시 설치 가능
        Destroy(building);
    }

    void PlayCollapseSfx()
    {
        if (collapseSfx == null) return;

        var sfxPlayer = GameObject.Find("SFXPlayer");
        if (sfxPlayer != null)
        {
            var src = sfxPlayer.GetComponent<AudioSource>();
            if (src != null) src.PlayOneShot(collapseSfx, collapseVolume);
        }
        else
        {
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(collapseSfx, cam ? cam.transform.position : Vector3.zero, collapseVolume);
        }
    }

    // ─────────────────────────────────────────────
    // 점유 해제 유틸

    /// <summary>
    /// 건물 루트에서 BuildingFootprint를 찾아 모든 타일의 점유 마커를 제거.
    /// 없으면 부모 타일(태그 "Tile")을 찾아 대표 마커만 제거(폴백).
    /// </summary>
    void FreeTilesForBuilding(GameObject buildingRoot)
    {
        if (!buildingRoot) return;

        // 1) 우선 BuildingFootprint가 있으면 공식 API로 해제
        var fp = buildingRoot.GetComponent<BuildingFootprint>() ??
                 buildingRoot.GetComponentInChildren<BuildingFootprint>() ??
                 buildingRoot.GetComponentInParent<BuildingFootprint>();

        if (fp != null)
        {
            fp.ReleaseAll();
            return;
        }

        // 2) (폴백) Footprint가 없을 때: 부모 타일 기준으로 마커 제거 시도
        var tile = FindTileAncestor(buildingRoot.transform);
        if (tile != null)
        {
            // 기본 마커명과, 설치기에서 사용하는 마커명 둘 다 시도
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

    // ─────────────────────────────────────────────
    // 탐색 유틸

    BuildingData FindBuildingDataInChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            BuildingData data = child.GetComponent<BuildingData>();
            if (data != null)
                return data;

            BuildingData nested = FindBuildingDataInChildren(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}