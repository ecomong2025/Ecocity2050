using UnityEngine;
using System.Collections;

public class GroupSequenceActivator : MonoBehaviour
{
    [Header("자동 수집")]
    public Transform parentForGroups;   // Group1, Group2… 가 들어있는 부모
    public float interval = 2f;
    public bool loop = false;

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
            // 현재 그룹만 켜기
            for (int i = 0; i < count; i++)
                parentForGroups.GetChild(i).gameObject.SetActive(i == idx);

            // 마지막 그룹이면 대기만 하고 break
            if (idx == count - 1 && !loop)
                yield break;

            yield return new WaitForSeconds(interval);

            idx++;
            if (idx >= count)
            {
                if (!loop) break;
                idx = 0;
            }
        }
    }

    void SetAll(bool on)
    {
        if (parentForGroups == null) return;
        int count = parentForGroups.childCount;
        for (int i = 0; i < count; i++)
            parentForGroups.GetChild(i).gameObject.SetActive(on);
    }
}
