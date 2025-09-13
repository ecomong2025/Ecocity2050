using UnityEngine;
public class UserDataManager : MonoBehaviour
{
    public static UserDataManager Instance;

    public KakaoUser currentUser;
    public bool isLoggedIn = false;

    public delegate void OnLoginSuccess(string displayName);
    public event OnLoginSuccess onLoginSuccess;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 로그인 시 데이터 저장
    public void SetUserData(KakaoUser user)
    {
        currentUser = user;
        isLoggedIn = true;

        string displayName = GetDisplayName();
        Debug.Log($"[UserDataManager] 로그인 완료: {displayName}");

        // 이벤트 호출
        onLoginSuccess?.Invoke(displayName);
    }

    // 로그아웃
    public void ClearUserData()
    {
        currentUser = null;
        isLoggedIn = false;
    }

    public string GetDisplayName()
    {
        if (currentUser == null) return "";
        string displayName = $"{currentUser.first_name} {currentUser.last_name}".Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = currentUser.username;
        return displayName;
    }
}