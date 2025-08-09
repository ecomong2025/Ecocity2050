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
        if (checkMarks[index] != null)
            checkMarks[index].gameObject.SetActive(on);
    }
}