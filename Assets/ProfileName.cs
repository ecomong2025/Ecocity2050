using UnityEngine;
using TMPro;

public class ProfileName : MonoBehaviour
{
    public TMP_Text nameText;

    void Start()
    {
        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.onLoginSuccess += UpdateName;

            // 이미 로그인된 상태면 바로 UI 갱신
            if (UserDataManager.Instance.isLoggedIn)
            {
                UpdateName(UserDataManager.Instance.GetDisplayName());
            }
            else
            {
                nameText.text = "로그인이 필요합니다";
            }
        }
        else
        {
            Debug.LogWarning("[ProfileName] UserDataManager.Instance가 null임");
        }
    }

    void UpdateName(string displayName)
    {
        Debug.Log($"[ProfileName] 로그인 후 불러온 닉네임: {displayName}");
        if (nameText != null)
            nameText.text = displayName;
    }

    private void OnDestroy()
    {
        if (UserDataManager.Instance != null)
            UserDataManager.Instance.onLoginSuccess -= UpdateName;
    }
}
