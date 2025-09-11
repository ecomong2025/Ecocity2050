using UnityEngine;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class UISlowShrink : MonoBehaviour
{
    [Header("대상 (비우면 자기 자신)")]
    public RectTransform target;

    [Header("스케일 (시작 → 끝)")]
    public Vector3 fromScale = new Vector3(1.00f, 1.00f, 1f);
    public Vector3 toScale = new Vector3(0.90f, 0.90f, 1f);  // 천천히 작아짐

    [Header("재생")]
    [Min(0.1f)] public float duration = 3f;        // 총 소요 시간(초)
    public bool playOnEnable = true;               // 활성화 시 자동 재생
    public AnimationCurve easing =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine co;

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        if (co != null) StopCoroutine(co);
        co = null;
    }

    public void Play()
    {
        if (target == null) target = GetComponent<RectTransform>();
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Animate(fromScale, toScale));
    }

    IEnumerator Animate(Vector3 from, Vector3 to)
    {
        float t = 0f;
        if (target) target.localScale = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float e = easing != null ? easing.Evaluate(u) : u;
            if (target) target.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }

        if (target) target.localScale = to;  // 최종값 고정
        co = null;
    }
}
