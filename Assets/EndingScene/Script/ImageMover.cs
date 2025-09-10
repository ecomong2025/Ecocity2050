using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ImageMover : MonoBehaviour
{
    public RectTransform target;   // 움직일 UI 이미지
    public Vector2 startPos = new Vector2(-200, 0);
    public Vector2 endPos = new Vector2(200, 0);
    public float duration = 2f;
    public bool loop = true;

    void OnEnable()
    {
        if (target != null) StartCoroutine(MoveLoop());
    }

    IEnumerator MoveLoop()
    {
        while (true)
        {
            // 왼쪽 → 오른쪽
            yield return Move(startPos, endPos);
            if (!loop) break;
        }
    }

    IEnumerator Move(Vector2 from, Vector2 to)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            target.anchoredPosition = Vector2.Lerp(from, to, t / duration);
            yield return null;
        }
        target.anchoredPosition = to;
    }
}
