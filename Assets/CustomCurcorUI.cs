using UnityEngine;
using UnityEngine.UI;

public class CustomCursorUI : MonoBehaviour
{
    public Image cursorImage;

    void Awake()
    {
        Cursor.visible = false;        // OS 커서 숨기기
        Cursor.lockState = CursorLockMode.Confined;  // 화면 밖으로 나가지 않게
    }

    void Update()
    {
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            cursorImage.canvas.transform as RectTransform,
            Input.mousePosition,
            cursorImage.canvas.worldCamera,
            out pos
        );
        cursorImage.rectTransform.localPosition = pos;
    }
}
