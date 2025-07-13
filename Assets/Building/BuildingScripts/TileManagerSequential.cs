using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TileManagerSequential : MonoBehaviour
{
    private List<GameObject> tileList = new List<GameObject>();

    void Start()
    {
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Tile_"))
            {
                tileList.Add(child.gameObject);
                child.gameObject.SetActive(false); // 전부 꺼두기
            }
        }

        // 숫자 추출해서 정렬: Tile_5, Tile_7, ...
        tileList.Sort((a, b) => ExtractTileNumber(a.name).CompareTo(ExtractTileNumber(b.name)));

        // 맨 처음 타일만 켜기 (Tile_5)
        if (tileList.Count > 0)
        {
            tileList[0].SetActive(true);
        }
    }

    void Update()
    {
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                int index = i; // Tile_5는 index 0 → i=1 → tileList[1] (Tile_7)

                if (index < tileList.Count)
                {
                    tileList[index].SetActive(true);
                }
            }
        }
    }

    int ExtractTileNumber(string name)
    {
        // Tile_5 → 5, Tile_11 → 11
        Match match = Regex.Match(name, @"Tile_(\d+)");
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }
        return 0;
    }
}
