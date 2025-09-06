using UnityEngine;
using System.Collections;

public class CloseBuildingPanel : MonoBehaviour
{
    [SerializeField] private GameObject targetPanel;
    [SerializeField] private float animDuration = 0.25f;
    private Vector3 panelOriginalScale = Vector3.one;
    private Coroutine animCoroutine;

    void Awake()
    {
        if (targetPanel != null)
            panelOriginalScale = targetPanel.transform.localScale;
    }

    public void Close()
    {
        if (targetPanel != null)
        {
            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(CloseWithAnim());
        }
        else
        {
            Debug.LogWarning("targetPanel이 설정되지 않았습니다!");
        }
    }

    private IEnumerator CloseWithAnim()
    {
        yield return StartCoroutine(ScalePanel(targetPanel.transform, Vector3.zero, animDuration));
        targetPanel.SetActive(false);
        targetPanel.transform.localScale = panelOriginalScale; // 다시 원래 크기로 복구
    }

    private IEnumerator ScalePanel(Transform panel, Vector3 target, float duration)
    {
        Vector3 start = panel.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            panel.localScale = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
        panel.localScale = target;
    }
}
