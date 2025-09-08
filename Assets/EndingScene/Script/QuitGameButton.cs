using UnityEngine;

public class QuitGameButton : MonoBehaviour
{
    // 버튼 클릭 시 실행할 메서드
    public void QuitGame()
    {
        Debug.Log("게임 종료");  // 에디터에서는 이 로그만 뜸
        Application.Quit();
    }
}
