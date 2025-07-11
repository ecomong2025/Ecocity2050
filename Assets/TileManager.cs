using UnityEngine;

public class TileManager : MonoBehaviour
{
    public GameObject tilePrefab;  // 에디터에서 할당
    public int maxSize = 10;       // 최대 타일 크기
    private int currentSize = 5;   // 초기 타일 크기
    private GameObject[,] tiles;   // 타일 저장 배열

    void Start()
    {
        CreateTiles(maxSize);
        UpdateTileVisibility();
    }

    void CreateTiles(int size)
    {
        tiles = new GameObject[size, size];
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector3 pos = new Vector3(x, 0, y);
                GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, this.transform);
                tile.name = $"Tile_{x}_{y}";
                tiles[x, y] = tile;
            }
        }
    }

    void UpdateTileVisibility()
    {
        for (int x = 0; x < maxSize; x++)
        {
            for (int y = 0; y < maxSize; y++)
            {
                bool visible = (x < currentSize && y < currentSize);
                tiles[x, y].SetActive(visible);
            }
        }
        Debug.Log($"현재 보이는 타일 크기: {currentSize}x{currentSize}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            IncreaseTileRange();
        }
    }

    public void IncreaseTileRange()
    {
        if (currentSize < maxSize)
        {
            currentSize++;
            UpdateTileVisibility();
            Debug.Log($"타일 크기 증가: {currentSize}x{currentSize}");
        }
    }
}
