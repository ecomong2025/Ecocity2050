using UnityEngine;
using System.Collections;

public class GroupSequenceActivator : MonoBehaviour
{
    [Header("자동 수집")]
    public Transform parentForGroups;            // Group1, Group2… 부모
    [Tooltip("각 그룹이 보이는 유지 시간(초)")]
    public float interval = 3.5f;                // 요청: 3.5초
    public bool loop = false;

    [Header("페이드")]
    [Tooltip("페이드 인/아웃 시간(초)")]
    public float fadeDuration = 1f;              // 요청: 1초

    private Coroutine _co;

    void OnEnable()
    {
        if (_co == null) _co = StartCoroutine(Run());
    }

    void OnDisable()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        SetAll(false);
    }

    IEnumerator Run()
    {
        if (parentForGroups == null) yield break;

        int count = parentForGroups.childCount;
        if (count == 0) yield break;

        SetAll(false);

        int idx = 0;
        while (true)
        {
            // 현재 그룹 활성화 + 페이드 인
            Transform curT = parentForGroups.GetChild(idx);
            GameObject cur = curT.gameObject;
            cur.SetActive(true);

            CanvasGroup cgCur = cur.GetComponent<CanvasGroup>();
            if (cgCur == null) cgCur = cur.AddComponent<CanvasGroup>();
            cgCur.alpha = 0f;
            cgCur.interactable = true;
            cgCur.blocksRaycasts = true;

            // 페이드 인
            yield return Fade(cgCur, 0f, 1f, fadeDuration);

            // 유지 시간 대기
            yield return new WaitForSeconds(interval);

            // 마지막 그룹이고 loop==false면 페이드아웃 없이 종료(화면 유지)
            bool isLast = (idx == count - 1);
            if (isLast && !loop) yield break;

            // 다음으로 넘어가기 전 현재 그룹 페이드 아웃 후 비활성
            yield return Fade(cgCur, 1f, 0f, fadeDuration);
            cur.SetActive(false);

            // 다음 인덱스
            idx++;
            if (idx >= count) idx = 0;
        }
    }

    IEnumerator Fade(CanvasGroup cg, float from, float to, float seconds)
    {
        float t = 0f;
        seconds = Mathf.Max(0.0001f, seconds);
        cg.alpha = from;

        while (t < seconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / seconds);
            cg.alpha = Mathf.LerpUnclamped(from, to, u);
            yield return null;
        }
        cg.alpha = to;
    }

    void SetAll(bool on)
    {
        if (parentForGroups == null) return;
        int count = parentForGroups.childCount;
        for (int i = 0; i < count; i++)
        {
            var go = parentForGroups.GetChild(i).gameObject;
            go.SetActive(on);
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = on ? 1f : 0f;
        }
    }
}
