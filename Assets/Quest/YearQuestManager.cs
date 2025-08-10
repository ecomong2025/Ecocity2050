using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class YearQuests
{
    [Range(2025, 2050)] public int year;
    public string[] questTexts = new string[4];
}

[System.Serializable]
public class YearGaugePiece
{
    public int year;              // 2030, 2035, 2040, 2045, 2050
    public GameObject imageObj;   // 해당 연도에 채워질 게이지 이미지 오브젝트
}

public class YearQuestManager : MonoBehaviour
{
    // YearQuestManager 클래스 내부에 추가
    [Header("Auto-complete Rules")]
    [Tooltip("공장 설치 퀘스트가 위치한 인덱스 (0~3)")]
    [SerializeField][Range(0, 3)] private int factoryQuestIndex = 0;

    [Tooltip("무(無)배출 건물 설치 퀘스트 인덱스 (0~3)")]
    [SerializeField][Range(0, 3)] private int zeroEmissionQuestIndex = 1;

    [Tooltip("채팅 퀘스트가 위치한 인덱스 (0~3)")]
    [SerializeField][Range(0, 3)] private int chatQuestIndex = 3;

    /// <summary>
    /// TileClickInstaller에서 설치 확정되면 호출
    /// </summary>
    public void OnBuildingInstalled(GameObject prefab, BuildingData data)
    {
        if (prefab == null || data == null) return;

        bool isFactory = IsFactory(prefab, data);
        bool isZero = IsZeroEmission(data);

        // 디버그 로그로 흐름 확인
        Debug.Log($"[YearQuestManager] Installed: {prefab.name}, isFactory={isFactory}, zero={isZero}");

        if (isFactory) CompleteQuest(factoryQuestIndex);
        if (isZero) CompleteQuest(zeroEmissionQuestIndex);
    }

    /// GPT 채팅 완료 시 호출되는 메서드
    public void OnChatCompleted()
    {
        Debug.Log("[YearQuestManager] Chat quest completed!");
        CompleteQuest(chatQuestIndex);
    }

    // === 판정 유틸 ===
    // 가장 좋은 건 BuildingData에 명시 필드가 있는 것(예: data.isFactory)
    private bool IsFactory(GameObject prefab, BuildingData data)
    {
        // BuildingData에 bool isFactory가 있다면 아래 주석을 사용하고 나머지는 삭제
        // return data.isFactory;

        // 없을 땐 Tag/이름으로 판정 (권장: 프리팹 Tag = "Factory")
        if (prefab.CompareTag("Factory")) return true;

        string n = prefab.name.ToLower();
        return n.Contains("factory") || n.Contains("plant"); // 필요시 키워드 추가
    }

    private bool IsZeroEmission(BuildingData data)
    {
        // co2PerSecond, instantCO2Change, maxCO2Change 값이
        // 0 또는 음수(-)면 무배출로 판정
        return data.co2PerSecond <= 0f
            && data.instantCO2Change <= 0f
            && data.maxCO2Change <= 0f;
    }

    public static YearQuestManager Instance;

    [Header("Year Settings")]
    [SerializeField] private int currentYear = 2025;
    [SerializeField] private int minYear = 2025;
    [SerializeField] private int maxYear = 2050;
    [SerializeField] private int step = 5;

    [Header("Predefined Sets")]
    [SerializeField] private YearQuests[] yearSets;

    [Header("UI")]
    [SerializeField] private QuestUITemplate questUI;

    [Header("Gauge Pieces")]
    [SerializeField] private YearGaugePiece[] gaugePieces;

    [Header("Year Text (화면 중앙 표시)")]
    [SerializeField] private TextMeshProUGUI yearTextUI;  // ⬅ YearText 연결

    private bool[] completed = new bool[4];

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (questUI == null)
            questUI = FindFirstObjectByType<QuestUITemplate>();

        if (gaugePieces != null && gaugePieces.Length > 0)
            Array.Sort(gaugePieces, (a, b) => a.year.CompareTo(b.year));
    }

    void Start()
    {
        LoadYear(currentYear);
    }

    private void LoadYear(int year)
    {
        var set = yearSets.FirstOrDefault(s => s.year == year);

        string[] texts;
        if (set == null || set.questTexts == null || set.questTexts.Length != 4)
        {
            texts = new[] { "Quest1", "Quest2", "Quest3", "Quest4" };
            Debug.LogWarning($"[YearQuestManager] {year} 연도 세트가 없어 기본 라벨로 표시합니다.");
        }
        else
        {
            texts = set.questTexts;
        }

        completed = new bool[4] { false, false, false, false };
        questUI?.BindYear(year, texts, completed);

        RefreshGauge();   // 게이지 업데이트
        RefreshYearText(); // 중앙 YearText 업데이트

        //연도 변경시 퀴즈 카운트 초기화 요청
        QuizManager quizMgr = FindObjectOfType<QuizManager>();
        if (quizMgr != null)
        {
            quizMgr.ResetQuizCorrectCount();
        }
    }

    private void RefreshGauge()
    {
        if (gaugePieces == null) return;

        foreach (var p in gaugePieces)
        {
            if (p.imageObj == null) continue;
            p.imageObj.SetActive(currentYear >= p.year);
        }
    }

    private void RefreshYearText()
    {
        if (yearTextUI != null)
            yearTextUI.text = currentYear.ToString();
    }

    public void CompleteQuest(int index)
    {
        if (index < 0 || index > 3) return;
        if (completed[index]) return;

        completed[index] = true;
        questUI?.UpdateCheck(index, true);

        if (completed.All(x => x))
        {
            int next = Mathf.Clamp(currentYear + step, minYear, maxYear);
            if (next == currentYear)
            {
                Debug.Log("[YearQuestManager] 마지막 연도입니다.");
                return;
            }

            currentYear = next;
            LoadYear(currentYear); // 내부에서 게이지+YearText 둘 다 업데이트
        }
    }

    public void ResetCurrent() => LoadYear(currentYear);
    public int GetCurrentYear() => currentYear;
}