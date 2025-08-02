using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TileManagerSequential : MonoBehaviour
{
    private List<GameObject> tileList = new List<GameObject>();
    public CameraScaler cameraScaler; 

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

            // 최초 카메라 위치 조정
            int initialMapSize = ExtractTileNumber(tileList[0].name);
            UpdateCamera(initialMapSize);
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

                    // 새로운 타일 크기 얻기
                    int mapSize = ExtractTileNumber(tileList[index].name);
                    UpdateCamera(mapSize);
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

    void UpdateCamera(int mapSize)
    {
        if (cameraScaler != null)
        {
            cameraScaler.mapSize = mapSize;
            cameraScaler.AdjustCameraToMap();
        }
        else
        {
            Debug.LogWarning("[TileManagerSequential] CameraScaler 참조가 설정되지 않았습니다.");
        }
    }
}