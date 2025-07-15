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
                child.gameObject.SetActive(false);
            }
        }

        // 이름순 정렬: Tile_5, Tile_7, ...
        tileList.Sort((a, b) => ExtractTileNumber(a.name).CompareTo(ExtractTileNumber(b.name)));

        // 첫 타일 활성화
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
                int index = i;
                if (index < tileList.Count)
                {
                    tileList[index].SetActive(true);
                }
            }
        }
    }

    int ExtractTileNumber(string name)
    {
        Match match = Regex.Match(name, @"Tile_(\d+)");
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }
        return 0;
    }
}