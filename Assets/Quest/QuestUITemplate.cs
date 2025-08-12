using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestUITemplate : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI yearValue;   // ProfileWithYear/Year/YearValue (TMP)

    [SerializeField] private TextMeshProUGUI[] questTexts = new TextMeshProUGUI[4]; // Quest1~4 텍스트
    [SerializeField] private Image[] checkMarks = new Image[4];                     // 동그라미 안 체크 이미지

    void Awake()
    {
        for (int i = 0; i < checkMarks.Length; i++)
        {
            if (checkMarks[i] != null)
                checkMarks[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 연도/퀘스트 세트/완료 상태 바인딩
    /// </summary>
    public void BindYear(int year, string[] texts, bool[] completed)
    {
        if (yearValue != null)
            yearValue.text = year.ToString();

        for (int i = 0; i < questTexts.Length; i++)
        {
            // 퀘스트 텍스트
            questTexts[i].text = (texts != null && i < texts.Length) ? texts[i] : $"Quest{i + 1}";

            // 체크 이미지 On/Off
            bool done = (completed != null && i < completed.Length) && completed[i];
            if (checkMarks != null && i < checkMarks.Length && checkMarks[i] != null)
                checkMarks[i].gameObject.SetActive(done);
        }
    }

    /// <summary>
    /// i번째 퀘스트의 체크 이미지 상태 변경
    /// </summary>
    public void UpdateCheck(int index, bool on)
    {
        if (index < 0 || index >= checkMarks.Length) return;
        var img = checkMarks[index];
        if (img == null) { Debug.LogError($"[QuestUI:{name}] img NULL idx={index}"); return; }

        img.gameObject.SetActive(on);

        if (on)
        {
            // 강제 가시화(가려짐/알파/정렬 문제 방지)
            img.enabled = true;
            var c = img.color; c.a = 1f; img.color = c;
            img.transform.SetAsLastSibling();
            var cg = img.GetComponentInParent<CanvasGroup>();
            if (cg && cg.alpha < 1f) cg.alpha = 1f;
            var rt = img.rectTransform;
            if (rt.rect.width < 4f || rt.rect.height < 4f) rt.sizeDelta = new Vector2(32, 32);
        }

        Debug.Log($"[QuestUI:{name}] UpdateCheck idx={index} on={on} " +
                  $"active={img.gameObject.activeInHierarchy} alpha={img.color.a} " +
                  $"sprite={(img.sprite ? img.sprite.name : "NULL")} canvas={(img.canvas ? img.canvas.name : "NULL")}");
    }

    // QuestUITemplate.cs 안, Awake() 아래에 추가
    void OnEnable()
    {
        // YearQuestManager 인스턴스 가져오기
        var mgr = YearQuestManager.Instance;
        if (mgr == null) return;

        // 현재 퀘스트 완료 상태 가져오기
        var flags = mgr.GetCompletedSnapshot();

        // 상태에 맞춰 체크 이미지 On/Off
        for (int i = 0; i < checkMarks.Length && i < flags.Length; i++)
        {
            if (checkMarks[i] != null)
                checkMarks[i].gameObject.SetActive(flags[i]);
        }
    }

}