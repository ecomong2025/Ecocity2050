using UnityEngine;
using TMPro;

public class HintBubbleController : MonoBehaviour
{
    public GameObject bubbleObject;      // 말풍선 전체 오브젝트 (Panel 등)
    public TMP_Text hintText;            // 말풍선 안의 텍스트
    public string hintMessage = "에너지를 절약하면 시민 만족도가 올라가요!";

    public void ToggleHint()
    {
        bool isActive = bubbleObject.activeSelf;

        // 상태를 반대로 전환
        bubbleObject.SetActive(!isActive);

        // 새로 켜질 때만 텍스트 갱신
        if (!isActive)
        {
            hintText.text = hintMessage;
        }
    }
}
