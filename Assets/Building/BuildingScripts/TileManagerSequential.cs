using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

[System.Serializable]
public class YearTileMap
{
    public int year;        // 2025, 2030, ...
    public int tileNumber;  // 7 이면 "Tile_7"
}

public class TileManagerSequential : MonoBehaviour
{
    private List<GameObject> tileList = new List<GameObject>();
    public CameraScaler cameraScaler;

    [Header("연도→타일 매핑")]
    public List<YearTileMap> yearTileMaps = new List<YearTileMap>();

    void Start()
    {
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Tile_"))
            {
                tileList.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
        }

        tileList.Sort((a, b) => ExtractTileNumber(a.name).CompareTo(ExtractTileNumber(b.name)));

        if (tileList.Count > 0)
        {
            tileList[0].SetActive(true);
            int initialMapSize = ExtractTileNumber(tileList[0].name);
            UpdateCamera(initialMapSize);
        }
    }

    void Update()
    {
        // 기존: 숫자키로 수동 오픈 기능 유지
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                int index = i;
                if (index < tileList.Count)
                {
                    tileList[index].SetActive(true);
                    int mapSize = ExtractTileNumber(tileList[index].name);
                    UpdateCamera(mapSize);
                }
            }
        }
    }

    // ✅ 연도 완료 시 호출 (YearQuestManager에서 씀)
    public void UnlockTileForYear(int year)
    {
        var map = yearTileMaps.FirstOrDefault(m => m.year == year);
        if (map == null) { Debug.LogWarning($"[TMS] {year} 매핑 없음"); return; }

        int num = map.tileNumber;
        var go = tileList.FirstOrDefault(t => ExtractTileNumber(t.name) == num);
        if (go == null) { Debug.LogWarning($"[TMS] Tile_{num} 없음"); return; }

        if (!go.activeSelf) go.SetActive(true);
        UpdateCamera(num);
        Debug.Log($"[TMS] {year} 완료 → Tile_{num} 활성화");
    }

    // (옵션) 다음 비활성 타일 하나 자동 오픈
    public void UnlockNextTile()
    {
        var next = tileList.FirstOrDefault(t => !t.activeSelf);
        if (next == null) { Debug.Log("[TMS] 더 열 타일 없음"); return; }
        next.SetActive(true);
        UpdateCamera(ExtractTileNumber(next.name));
    }

    int ExtractTileNumber(string name)
    {
        var m = Regex.Match(name, @"Tile_(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    void UpdateCamera(int mapSize)
    {
        if (cameraScaler != null)
        {
            cameraScaler.mapSize = mapSize;
            cameraScaler.AdjustCameraToMap();
        }
        else Debug.LogWarning("[TMS] CameraScaler 미할당");
    }
}
