using UnityEngine;
using SpeechBubble;

public class BuildingCO2Announcer : MonoBehaviour
{
    public SpeechBubble_TMP speechBubble;

    private BuildingData buildingData;

    private void Start()
    {
        buildingData = GetComponent<BuildingData>();

        if (buildingData != null)
        {
            CheckCO2AndAnnounce();
        }
        else
        {
            Debug.LogWarning("BuildingData가 연결되어 있지 않습니다.");
        }
    }

    private void CheckCO2AndAnnounce()
    {
        if (speechBubble == null) return;

        string message = null;

        // 조건별 메시지 출력 -> 추후 수정
        if (buildingData.instantCO2Change >= 50)
        {
            message = "탄소 배출량이 급격히 증가했어요!";
            speechBubble.setDialogueTextColor(Color.red);
        }
        else if (buildingData.co2PerSecond >= 5)
        {
            message = "지속적으로 탄소가 많이 배출되고 있어요.";
            speechBubble.setDialogueTextColor(new Color(1f, 0.5f, 0f)); // 주황
        }
        else if (buildingData.maxCO2Change >= 200)
        {
            message = "이 건물은 많은 탄소를 배출할 수 있어요. 주의하세요";
            speechBubble.setDialogueTextColor(Color.gray);
        }

        // 출력할 메시지가 있으면 말풍선 표시
        if (!string.IsNullOrEmpty(message))
        {
            speechBubble.setDialogueText(message);
            speechBubble.gameObject.SetActive(true);
            StartCoroutine(HideSpeechBubbleAfterSeconds(3f));
        }
    }

    private System.Collections.IEnumerator HideSpeechBubbleAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        speechBubble.gameObject.SetActive(false);
    }
}
