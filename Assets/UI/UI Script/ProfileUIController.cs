using UnityEngine;
using System.Collections;

public class ProfileUIController : MonoBehaviour
{
    public GameObject questUI;           // Quest 패널
    public GameObject[] otherUIs;        // 다시 보여줄 UI 목록

    [Header("Animation")]
    public float animDuration = 0.25f;
    private Coroutine animCoroutine;
    private Vector3 panelOriginalScale = Vector3.one;

    void Awake()
    {
        if (questUI != null)
            panelOriginalScale = questUI.transform.localScale; // 퀘스트 패널 원래 크기 저장
    }

    // Profile 버튼 클릭 → QuestUI 보이기, 나머지 숨기기 + 커지는 애니메이션
    public void OnProfileClicked()
    {
        // ProfileUI 오브젝트가 비활성화되어 있으면 먼저 활성화
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SFXPlayer.Instance.PlayClick();
        questUI.SetActive(true);
        
        foreach (GameObject ui in otherUIs)
        {
            if (ui != null)
                ui.SetActive(false);
        }
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        questUI.SetActive(true);
        questUI.transform.localScale = Vector3.zero;
        animCoroutine = StartCoroutine(ScalePanel(questUI.transform, panelOriginalScale, animDuration));
    }

    // X 버튼 클릭 → QuestUI 숨기고, 나머지 다시 보이기 + 작아지는 애니메이션
    public void OnCloseQuestUI()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(CloseWithAnim());
    }

    private IEnumerator CloseWithAnim()
    {
        yield return StartCoroutine(ScalePanel(questUI.transform, Vector3.zero, animDuration));
        SFXPlayer.Instance.PlayClick();
        questUI.SetActive(false);
        questUI.transform.localScale = panelOriginalScale; // 다시 원래 크기로 복구
        foreach (GameObject ui in otherUIs)
        {
            if (ui != null)
                ui.SetActive(true);
        }
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