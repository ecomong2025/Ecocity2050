using UnityEngine;

public class WaypointPlacer : MonoBehaviour
{
    public GameObject waypointPrefab;
    public float tileSize = 1f;

    void Start()
    {
        Vector3[] positions = new Vector3[]
        {
            new Vector3(0, 0, 2),
            new Vector3(1, 0, 2),
            new Vector3(2, 0, 2),
            new Vector3(2, 0, 1),
            new Vector3(2, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 1),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject wp = Instantiate(waypointPrefab, positions[i] * tileSize, Quaternion.identity, transform);
            wp.name = "WP" + i;
        }
    }
}
