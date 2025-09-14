using UnityEngine;

/// <summary>
/// 게임 데이터를 관리하는 싱글톤 클래스
/// 씬 간 데이터 전달과 저장/로드를 담당
/// </summary>
public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    [Header("현재 게임 데이터")]
    public ScenePayload currentPayload;

    // 정적 변수로 불러온 데이터를 임시 저장
    public static ScenePayload LoadedPayload { get; set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 불러온 데이터가 있다면 현재 payload에 적용
            if (LoadedPayload != null)
            {
                currentPayload = LoadedPayload;
                LoadedPayload = null; // 사용 후 초기화
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 게임 시작 시 데이터 초기화 (새 게임)
    /// </summary>
    public void InitializeNewGame()
    {
        if (currentPayload == null)
        {
            currentPayload = ScriptableObject.CreateInstance<ScenePayload>();
        }

        // 새 게임 기본값으로 초기화
        currentPayload.co2Tons = 0f;
        currentPayload.citizenSatisfactionLabel = "";
        currentPayload.budget = 0;
        currentPayload.topTags = new string[0];
        currentPayload.aiCityName = "";

        PlayerPrefs.SetInt("StartNewGame", 0);
    }

    /// <summary>
    /// 현재 게임 데이터 저장
    /// </summary>
    public void SaveCurrentGame(System.Action<bool, string> callback = null)
    {
        if (currentPayload == null)
        {
            callback?.Invoke(false, "저장할 데이터가 없습니다.");
            return;
        }

        SaveGameManager.Instance.SaveGameData(currentPayload, callback);
    }

    /// <summary>
    /// 게임 씬에서 호출되어 현재 payload를 반환
    /// </summary>
    public ScenePayload GetCurrentPayload()
    {
        // 불러오기로 시작된 게임인지 확인
        bool loadFromSave = PlayerPrefs.GetInt("LoadFromSave", 0) == 1;
        if (loadFromSave)
        {
            PlayerPrefs.SetInt("LoadFromSave", 0);
            return currentPayload; // 이미 불러온 데이터
        }

        // 새 게임으로 시작된 경우
        bool startNewGame = PlayerPrefs.GetInt("StartNewGame", 0) == 1;
        if (startNewGame)
        {
            InitializeNewGame();
        }

        return currentPayload;
    }

    /// <summary>
    /// 게임 데이터 업데이트
    /// </summary>
    public void UpdateGameData(float co2Tons, string citizenSatisfaction, int budget, string[] topTags, string aiCityName = "")
    {
        if (currentPayload == null)
        {
            currentPayload = ScriptableObject.CreateInstance<ScenePayload>();
        }

        currentPayload.co2Tons = co2Tons;
        currentPayload.citizenSatisfactionLabel = citizenSatisfaction;
        currentPayload.budget = budget;
        currentPayload.topTags = topTags;
        if (!string.IsNullOrEmpty(aiCityName))
        {
            currentPayload.aiCityName = aiCityName;
        }
    }

    /// <summary>
    /// 자동 저장 (게임 진행 중 주요 시점에서 호출)
    /// </summary>
    public void AutoSave()
    {
        // KakaoLoginManager를 찾아서 로그인 상태 확인
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        if (kakaoManager != null && kakaoManager.IsLoggedIn())
        {
            SaveCurrentGame((success, message) =>
            {
                if (success)
                {
                    Debug.Log("자동 저장 완료");
                }
                else
                {
                    Debug.LogWarning($"자동 저장 실패: {message}");
                }
            });
        }
        else
        {
            Debug.Log("로그인되지 않아 자동 저장을 건너뜁니다.");
        }
    }

    /// <summary>
    /// 현재 사용자 ID 반환 (헬퍼 메서드)
    /// </summary>
    public string GetCurrentUserId()
    {
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        return kakaoManager?.GetCurrentUserId() ?? "";
    }

    /// <summary>
    /// 로그인 상태 확인 (헬퍼 메서드)
    /// </summary>
    public bool IsUserLoggedIn()
    {
        var kakaoManager = FindObjectOfType<KakaoLoginManager>();
        return kakaoManager != null && kakaoManager.IsLoggedIn();
    }
}