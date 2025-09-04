using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    public Texture2D cursorTexture;
    public Vector2 hotspot = new Vector2(10, 10);
    public int cursorSize = 32; // 원하는 크기(px)

    void Start()
    {
        if (cursorTexture != null)
        {
            Texture2D scaled = ScaleTexture(cursorTexture, cursorSize, cursorSize);
            Cursor.SetCursor(scaled, hotspot, CursorMode.Auto);
        }
    }

    Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        // 임시 RenderTexture 생성
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
        Graphics.Blit(source, rt);

        // RenderTexture -> Texture2D 변환
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();

        // 정리
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }
}