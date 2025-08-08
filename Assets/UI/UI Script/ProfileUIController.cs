using UnityEngine;

public class ProfileUIController : MonoBehaviour
{
    public GameObject questUI;           // Quest 패널
    public GameObject[] otherUIs;        // 다시 보여줄 UI 목록

    // Profile 버튼 클릭 → QuestUI 보이기, 나머지 숨기기
    public void OnProfileClicked()
    {
        questUI.SetActive(true);
        foreach (GameObject ui in otherUIs)
        {
            if (ui != null)
                ui.SetActive(false);
        }
    }

    // X 버튼 클릭 → QuestUI 숨기고, 나머지 다시 보이기
    public void OnCloseQuestUI()
    {
        questUI.SetActive(false);
        foreach (GameObject ui in otherUIs)
        {
            if (ui != null)
                ui.SetActive(true);
        }
    }
}