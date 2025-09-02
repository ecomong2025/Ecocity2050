using UnityEngine;

public class KakaoStartManager : MonoBehaviour
{
    //gpt가 만든 예시 코드라, 삭제하고 작성해주시면 됩니다.
    // 싱글톤 패턴 (간단 버전)
    public static KakaoStartManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TryLogin()
    {
        // ✅ 카카오 로그인 API 호출 (SDK 연동 부분)
        Debug.Log("카카오 로그인 시도...");

        // 여기서 SDK 콜백 처리 → 성공하면 GameSceneLoader에 알리기
        bool loginSuccess = true; // 가정

        if (loginSuccess)
        {
            // GameSceneLoader에 성공 알림
            FindObjectOfType<GameSceneLoader>().OnKakaoLoginSuccess();
        }
    }
}
