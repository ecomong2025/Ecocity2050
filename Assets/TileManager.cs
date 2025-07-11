using UnityEngine;

public class TileManager : MonoBehaviour
{
    public GameObject tilePrefab;  // �����Ϳ��� �Ҵ�
    public int maxSize = 10;       // �ִ� Ÿ�� ũ��
    private int currentSize = 5;   // �ʱ� Ÿ�� ũ��
    private GameObject[,] tiles;   // Ÿ�� ���� �迭

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
        Debug.Log($"���� ���̴� Ÿ�� ũ��: {currentSize}x{currentSize}");
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
            Debug.Log($"Ÿ�� ũ�� ����: {currentSize}x{currentSize}");
        }
    }
}
