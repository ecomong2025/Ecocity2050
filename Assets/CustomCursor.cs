using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    public Texture2D cursorTexture;

    // 화살표 끝나는 지점 좌표(px, 좌측 상단이 (0,0))
    public Vector2 hotspot = new Vector2(10, 10);

    void Start()
    {
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }
}
