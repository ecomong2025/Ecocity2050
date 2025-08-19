using System.Collections.Generic;
using UnityEngine;

public class BuildingFootprint : MonoBehaviour
{
    private List<Transform> tiles;
    private string markerName;

    public void Init(List<GameObject> tileObjs, string marker)
    {
        tiles = new List<Transform>(tileObjs.Count);
        foreach (var t in tileObjs) tiles.Add(t.transform);
        markerName = marker;
    }

    void OnDestroy()
    {
        if (tiles == null) return;
        foreach (var t in tiles)
        {
            var m = t.Find(markerName);
            if (m != null) Object.Destroy(m.gameObject);
        }
    }
}