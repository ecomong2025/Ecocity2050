using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class QuestUITemplate : MonoBehaviour
{
    [Header("Year & Gauge UI")]
    [SerializeField] private TextMeshProUGUI yearValue;          // 중앙 연도 TMP
    [SerializeField] private YearGaugePiece[] gaugePieces;       // 연도 게이지 조각

    [Header("Quest UI")]
    [SerializeField] private TextMeshProUGUI[] questTexts = new TextMeshProUGUI[4]; // Quest1~4 텍스트
    [SerializeField] private Image[] checkMarks = new Image[4];                     // 동그라미 안 체크 이미지

    [Header("Next Year Popup (Overlay) — UI 전담")]
    [SerializeField] private GameObject nextYearPanel;
    [SerializeField] private TextMeshProUGUI nextYearTextTMP;
    [SerializeField] private TextMeshProUGUI announceTextTMP;
    [SerializeField] private float popupSeconds = 3f;
    [SerializeField] private bool fadeWithCanvasGroup = true;

    // 내부 캐시
    private int _cachedYear = -1;
    private readonly string[] _cachedTexts = new string[4];
    private readonly bool[] _cachedCompleted = new bool[4];

    void Awake()
    {
        // 게이지 조각 정렬(연도 오름차순)
        if (gaugePieces != null && gaugePieces.Length > 0)
            System.Array.Sort(gaugePieces, (a, b) => a.year.CompareTo(b.year));

        // 체크 전부 끔
        for (int i = 0; i < checkMarks.Length; i++)
            if (checkMarks[i] != null) checkMarks[i].gameObject.SetActive(false);

        if (nextYearPanel) nextYearPanel.SetActive(false);
    }

    void OnEnable()
    {
        // UI가 다시 Enable될 때 캐시 기준으로 표시 복구
        if (_cachedYear > 0)
        {
            ApplyYearText(_cachedYear);
            ApplyGauge(_cachedYear);

            for (int i = 0; i < questTexts.Length; i++)
                if (questTexts[i] != null)
                    questTexts[i].text = !string.IsNullOrEmpty(_cachedTexts[i]) ? _cachedTexts[i] : $"Quest{i + 1}";

            for (int i = 0; i < checkMarks.Length; i++)
                if (checkMarks[i] != null)
                    checkMarks[i].gameObject.SetActive(i < _cachedCompleted.Length && _cachedCompleted[i]);
        }
    }

    /// <summary>
    /// 연도/퀘스트 세트/완료 상태 바인딩 (YearQuestManager가 호출)
    /// </summary>
    public void BindYear(int year, string[] texts, bool[] completed)
    {
        // 캐시 저장
        _cachedYear = year;
        for (int i = 0; i < 4; i++)
        {
            _cachedTexts[i] = (texts != null && i < texts.Length && !string.IsNullOrEmpty(texts[i]))
                                ? texts[i] : $"Quest{i + 1}";
            _cachedCompleted[i] = (completed != null && i < completed.Length) && completed[i];
        }

        // 화면 반영
        ApplyYearText(year);
        ApplyGauge(year);

        for (int i = 0; i < questTexts.Length; i++)
        {
            if (questTexts[i] != null)
                questTexts[i].text = _cachedTexts[i];

            if (checkMarks != null && i < checkMarks.Length && checkMarks[i] != null)
                checkMarks[i].gameObject.SetActive(_cachedCompleted[i]);
        }
    }

    /// <summary>
    /// i번째 퀘스트의 체크 이미지 상태 변경 (YearQuestManager가 완료 시 호출)
    /// </summary>
    public void UpdateCheck(int index, bool on)
    {
        if (index < 0 || index >= checkMarks.Length) return;

        // 캐시 갱신
        if (index < _cachedCompleted.Length)
            _cachedCompleted[index] = on;

        var img = checkMarks[index];
        if (img == null)
        {
            Debug.LogError($"[QuestUI:{name}] img NULL idx={index}");
            return;
        }

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
            if (rt.rect.width < 4f || rt.rect.height < 4f)
                rt.sizeDelta = new Vector2(32, 32);
        }

        Debug.Log($"[QuestUI:{name}] UpdateCheck idx={index} on={on} " +
                  $"active={img.gameObject.activeInHierarchy} alpha={img.color.a} " +
                  $"sprite={(img.sprite ? img.sprite.name : "NULL")} canvas={(img.canvas ? img.canvas.name : "NULL")}");
    }

    /// <summary>
    /// 다음 해 팝업을 재생하고 끝날 때까지 yield. (매니저에서 StartCoroutine으로 호출)
    /// </summary>
    public IEnumerator PlayNextYearPopup(int nextYear)
    {
        if (nextYearTextTMP) nextYearTextTMP.text = nextYear.ToString();
        if (announceTextTMP) announceTextTMP.text = $"{nextYear}년도에 도달했어요!";

        if (nextYearPanel) nextYearPanel.SetActive(true);

        CanvasGroup cg = null;
        if (fadeWithCanvasGroup && nextYearPanel)
        {
            cg = nextYearPanel.GetComponent<CanvasGroup>() ?? nextYearPanel.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.SmoothStep(0f, 1f, t / 0.15f);
                yield return null;
            }
        }

        float timer = 0f;
        while (timer < popupSeconds)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (fadeWithCanvasGroup && cg)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.SmoothStep(1f, 0f, t / 0.2f);
                yield return null;
            }
        }

        if (nextYearPanel) nextYearPanel.SetActive(false);
    }

    // ====== 내부: 연도/게이지 표시 반영 ======
    private void ApplyYearText(int year)
    {
        if (yearValue != null)
            yearValue.text = year.ToString();
    }

    private void ApplyGauge(int currentYear)
    {
        if (gaugePieces == null) return;
        foreach (var p in gaugePieces)
        {
            if (p == null || p.imageObj == null) continue;
            p.imageObj.SetActive(currentYear >= p.year);
        }
    }
}