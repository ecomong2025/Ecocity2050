using System.Collections.Generic;
using UnityEngine;

public class BuildingFootprint : MonoBehaviour
{
    [SerializeField] private List<GameObject> tiles = new List<GameObject>();
    [SerializeField] private string markerName = "__OCCUPIED__";

    // 설치 직후 TileClickInstaller에서 호출
    public void Init(List<GameObject> installedTiles, string occupiedMarkerName)
    {
        tiles = new List<GameObject>(installedTiles);
        markerName = occupiedMarkerName;
    }

    public IReadOnlyList<GameObject> Tiles => tiles;
    public string MarkerName => markerName;

    // 건물 제거(재난 등) 시: 점유 마커 제거 → 타일 해제
    public void ReleaseAll()
    {
        if (tiles == null) return;
        foreach (var tile in tiles)
        {
            if (tile == null) continue;
            var mark = tile.transform.Find(markerName);
            if (mark) GameObject.Destroy(mark.gameObject);
        }
        tiles.Clear();
    }
}