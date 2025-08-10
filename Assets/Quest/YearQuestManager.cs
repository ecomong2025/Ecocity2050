using System.Linq;
using UnityEngine;

[System.Serializable]
public class YearQuests
{
    [Range(2025, 2030)] public int year;           // 2025~2030 (5년 단위로만 사용 권장)
    public string[] questTexts = new string[4];    // 항상 4개: "Quest1"~"Quest4"
}

public class YearQuestManager : MonoBehaviour
{
    public static YearQuestManager Instance;

    [Header("Year Settings")]
    [SerializeField] private int currentYear = 2025;
    [SerializeField] private int minYear = 2025;
    [SerializeField] private int maxYear = 2030;
    [SerializeField] private int step = 5;

    [Header("Predefined Sets (인스펙터에서 2025~2030 세트 등록)")]
    [SerializeField] private YearQuests[] yearSets; // 2025~2030 세트 (인스펙터에서 설정)

    [Header("UI")]
    [SerializeField] private QuestUITemplate questUI;

    private bool[] completed = new bool[4]; // 4개 완료상태

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (questUI == null)
            questUI = FindFirstObjectByType<QuestUITemplate>(); // Unity 2022+ 권장
        // (구버전이면 FindObjectOfType<QuestUITemplate>())
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
            // 세트가 없으면 기본 라벨
            texts = new[] { "Quest1", "Quest2", "Quest3", "Quest4" };
            Debug.LogWarning($"[YearQuestManager] {year} 연도 세트가 없어 기본 라벨로 표시합니다.");
        }
        else
        {
            texts = set.questTexts;
        }

        completed = new bool[4] { false, false, false, false };
        questUI?.BindYear(year, texts, completed);
    }

    /// <summary>
    /// 외부 이벤트에서 i번째 퀘스트 완료 보고 (0~3)
    /// </summary>
    public void CompleteQuest(int index)
    {
        if (index < 0 || index > 3) return;
        if (completed[index]) return;

        // 퀘스트 완료 조건 필요
        completed[index] = true; //퀘스트 완료면 true로 변경
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
            LoadYear(currentYear);
        }
    }

    // 디버그/버튼 테스트용
    public void ResetCurrent() => LoadYear(currentYear);
    public int GetCurrentYear() => currentYear;
}