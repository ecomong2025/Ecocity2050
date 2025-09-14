using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic; // 🔧 for Queue

public class QuestUITemplate : MonoBehaviour
{
    [Header("Year & Gauge UI")]
    [SerializeField] private TextMeshProUGUI yearValue;          
    [SerializeField] private TextMeshProUGUI gaugeYearText;      
    [SerializeField] private YearGaugePiece[] gaugePieces;       

    [Header("Quest UI")]
    [SerializeField] private TextMeshProUGUI[] questTexts = new TextMeshProUGUI[4];
    [SerializeField] private Image[] checkMarks = new Image[4];

    [Header("Next Year Popup (Overlay) — UI 전담")]
    [SerializeField] private GameObject nextYearPanel;
    [SerializeField] private TextMeshProUGUI nextYearTextTMP;
    [SerializeField] private TextMeshProUGUI announceTextTMP;
    [SerializeField] private float popupSeconds = 3f;
    [SerializeField] private bool fadeWithCanvasGroup = true;
    // QuestUITemplate 상단 필드 근처에 추가
    [SerializeField] private AudioSource sfxSource;        // 비워두면 자동으로 붙여줌
    [SerializeField] private AudioClip popupOpenSfx;       // 팝업 켤 때
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // 🔧 팝업 블로킹 패널(다 꺼졌을 때만 팝업 재생)
    [Header("Popup Blocking Panels")]
    [SerializeField] private GameObject quizPanel;     // 퀴즈 패널 루트
    [SerializeField] private GameObject chatPanel;     // 채팅 패널 루트

    // 내부 캐시
    private int _cachedYear = -1;
    private readonly string[] _cachedTexts = new string[4];

    // 🔧 팝업 큐 & 러너 상태
    private readonly Queue<int> _pendingPopupYears = new Queue<int>();
    private Coroutine _popupRunner;
    private bool _isPopupPlaying = false;

    void Awake()
    {
        if (gaugePieces != null && gaugePieces.Length > 0)
            System.Array.Sort(gaugePieces, (a, b) => a.year.CompareTo(b.year));

        if (nextYearPanel) nextYearPanel.SetActive(false);

        // === SFX 초기화 ===
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;      // UI 사운드: 2D
            sfxSource.loop = false;
        }
    }

    void OnEnable()
    {
        if (_cachedYear > 0)
        {
            ApplyYearText(_cachedYear);
            ApplyGauge(_cachedYear);

            for (int i = 0; i < questTexts.Length; i++)
                if (questTexts[i] != null)
                    questTexts[i].text = !string.IsNullOrEmpty(_cachedTexts[i]) ? _cachedTexts[i] : $"Quest{i + 1}";

            // YearQuestManager의 완료상태만 사용
            var yqm = FindObjectOfType<YearQuestManager>(true);
            var completed = yqm != null ? yqm.GetType().GetField("completed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(yqm) as bool[] : null;
            for (int i = 0; i < checkMarks.Length; i++)
                UpdateCheck(i, completed != null && i < completed.Length && completed[i]);
        }
    }

    /// <summary>
    /// 🔧 외부에서 호출: 팝업을 "즉시 재생"하지 않고 큐에 넣는다.
    /// (YearQuestManager: questUI.EnqueueNextYearPopup(nextYear); 로 바꾸기)
    /// </summary>
    public void EnqueueNextYearPopup(int nextYear)
    {
        _pendingPopupYears.Enqueue(nextYear);
        // 러너가 없으면 시작
        if (_popupRunner == null)
            _popupRunner = StartCoroutine(PopupRunner());
    }

    /// <summary>
    /// 🔧 팝업 러너: 블로킹 패널이 모두 닫힐 때까지 기다렸다가 차례로 팝업 재생
    /// </summary>
    private IEnumerator PopupRunner()
    {
        while (_pendingPopupYears.Count > 0)
        {
            // 패널 다 닫힐 때까지 대기
            while (AnyBlockingPanelOpen() || _isPopupPlaying)
                yield return null;

            int year = _pendingPopupYears.Peek();
            _isPopupPlaying = true;
            // 기존 팝업 코루틴 재생
            yield return StartCoroutine(PlayNextYearPopup(year));
            _isPopupPlaying = false;

            _pendingPopupYears.Dequeue();
        }
        _popupRunner = null;
    }

    /// <summary>
    /// 🔧 하나라도 열려 있으면 true
    /// - activeInHierarchy가 true이면 "열림"으로 간주
    /// - CanvasGroup이 있으면 alpha>~0일 때 열림으로 간주(페이드 중 대비)
    /// 프로젝트 상황에 맞게 기준을 조절할 수 있음.
    /// </summary>
    private bool AnyBlockingPanelOpen()
    {
        return IsPanelOpen(quizPanel) || IsPanelOpen(chatPanel);
    }

    private bool IsPanelOpen(GameObject panel)
    {
        if (!panel) return false;
        if (!panel.activeInHierarchy) return false;

        // active면 열린 것으로 간주
        return true;
    }

    /// <summary>
    /// 연도/퀘스트 세트/완료 상태 바인딩 (YearQuestManager가 호출)
    /// </summary>
    public void BindYear(int year, string[] texts, bool[] completed)
    {
        _cachedYear = year;
        for (int i = 0; i < 4; i++)
        {
            _cachedTexts[i] = (texts != null && i < texts.Length && !string.IsNullOrEmpty(texts[i]))
                                ? texts[i] : $"Quest{i + 1}";
        }

        ApplyYearText(year);
        ApplyGauge(year);

        for (int i = 0; i < questTexts.Length; i++)
        {
            if (questTexts[i] != null)
                questTexts[i].text = _cachedTexts[i];

            // YearQuestManager의 완료상태만 사용
            UpdateCheck(i, completed != null && i < completed.Length && completed[i]);
        }
    }

    public void UpdateCheck(int index, bool on)
    {
        if (index < 0 || index >= checkMarks.Length) return;

        var img = checkMarks[index];
        if (img == null)
        {
            Debug.LogError($"[QuestUI:{name}] img NULL idx={index}");
            return;
        }

        img.gameObject.SetActive(on); // 체크 표시를 SetActive로 관리
    }

    /// <summary>
    /// 🔧 이제 내부에서만 사용하도록 권장 (public 유지해도 외부에서는 Enqueue 호출)
    /// 다음 해 팝업을 재생하고 끝날 때까지 yield.
    /// </summary>
    public IEnumerator PlayNextYearPopup(int nextYear)
    {
        if (nextYearTextTMP) nextYearTextTMP.text = nextYear.ToString();
        if (announceTextTMP) announceTextTMP.text = $"{nextYear}년도에 도달했어요!";

        if (nextYearPanel) nextYearPanel.SetActive(true);
        // 🔊 팝업 열림 SFX
        // 우선 SFXPlayer 싱글턴을 사용. SFXPlayer에 popupOpenClip을 설정해두면 그걸 재생.
        if (SFXPlayer.Instance != null)
        {
            if (SFXPlayer.Instance.popupOpenClip != null)
            {
                SFXPlayer.Instance.PlayPopupOpen(sfxVolume);
            }
            else if (popupOpenSfx != null)
            {
                SFXPlayer.Instance.PlaySFX(popupOpenSfx, sfxVolume);
            }
        }
        else
        {
            // 폴백: 로컬 AudioSource가 있으면 재생
            if (sfxSource != null && popupOpenSfx != null)
                sfxSource.PlayOneShot(popupOpenSfx, sfxVolume);
        }

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
        if (yearValue != null) yearValue.text = year.ToString();
        if (gaugeYearText) gaugeYearText.text = year.ToString();
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