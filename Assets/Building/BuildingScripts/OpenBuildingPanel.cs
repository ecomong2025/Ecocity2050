using UnityEngine;
using UnityEngine.UI;  // ScrollRect 사용하려면 필요
using System.Collections;

public class OpenBuildingPanel : MonoBehaviour
{
    [SerializeField] private GameObject targetPanel;
    [SerializeField] private float animDuration = 0.25f;

    [Header("Optional Scroll Reset")]
    [SerializeField] private ScrollRect scrollRect;                 // BuildingPanel 안 ScrollRect
    [SerializeField] private RectTransform scrollContent;           // ScrollRect의 Content

    private Vector3 panelOriginalScale = Vector3.one;
    private Coroutine animCoroutine;

    void Awake()
    {
        if (targetPanel != null)
            panelOriginalScale = targetPanel.transform.localScale;
    }

    public void Open()
    {
        if (targetPanel != null)
        {
            if (animCoroutine != null) StopCoroutine(animCoroutine);

            targetPanel.SetActive(true);                 // 먼저 켜주고
            targetPanel.transform.localScale = Vector3.zero; // 0 크기에서 시작

            // 📌 스크롤 위치 초기화
            ResetScrollPosition();

            animCoroutine = StartCoroutine(OpenWithAnim());
        }
        else
        {
            Debug.LogWarning("targetPanel이 설정되지 않았습니다!");
        }
    }

    private IEnumerator OpenWithAnim()
    {
        yield return StartCoroutine(ScalePanel(targetPanel.transform, panelOriginalScale, animDuration));
        targetPanel.transform.localScale = panelOriginalScale; // 안전하게 복구

        // 📌 애니메이션 끝난 뒤에도 한 번 더 스냅 (레이아웃 갱신 이후 튀는 거 방지)
        ResetScrollPosition();
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

    private void ResetScrollPosition()
    {
        if (scrollRect == null) return;

        // 관성/속도 제거
        scrollRect.StopMovement();
        scrollRect.velocity = Vector2.zero;

        // pivot에 따라 맨 위가 1인지 0인지 다를 수 있음
        float top = (scrollContent != null && scrollContent.pivot.y > 0.5f) ? 1f : 0f;
        scrollRect.verticalNormalizedPosition = top;

        Canvas.ForceUpdateCanvases();
    }
}