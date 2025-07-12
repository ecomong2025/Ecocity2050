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
            Debug.LogWarning("BuildingData�� ����Ǿ� ���� �ʽ��ϴ�.");
        }
    }

    private void CheckCO2AndAnnounce()
    {
        if (speechBubble == null) return;

        string message = null;

        // ���Ǻ� �޽��� ��� -> ���� ����
        if (buildingData.instantCO2Change >= 50)
        {
            message = "ź�� ���ⷮ�� �ް��� �����߾��!";
            speechBubble.setDialogueTextColor(Color.red);
        }
        else if (buildingData.co2PerSecond >= 5)
        {
            message = "���������� ź�Ұ� ���� ����ǰ� �־��.";
            speechBubble.setDialogueTextColor(new Color(1f, 0.5f, 0f)); // ��Ȳ
        }
        else if (buildingData.maxCO2Change >= 200)
        {
            message = "�� �ǹ��� ���� ź�Ҹ� ������ �� �־��. �����ϼ���";
            speechBubble.setDialogueTextColor(Color.gray);
        }

        // ����� �޽����� ������ ��ǳ�� ǥ��
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
