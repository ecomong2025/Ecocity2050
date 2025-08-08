using UnityEngine;

public class EmojiController : MonoBehaviour
{
    public GameObject VeryLoveIcon;
    public GameObject VeryGoodIcon;
    public GameObject GoodIcon;
    public GameObject BadIcon;
    public GameObject VeryBadIcon;

    public void ShowEmoji(string satisfaction)
    {
        // 모든 이모지 비활성화
        VeryLoveIcon.SetActive(false);
        VeryGoodIcon.SetActive(false);
        GoodIcon.SetActive(false);
        BadIcon.SetActive(false);
        VeryBadIcon.SetActive(false);

        // 조건에 따라 하나만 활성화
        if (satisfaction == "매우 좋음")
        {
            VeryLoveIcon.SetActive(true);
        }
        else if (satisfaction == "좋음")
        {
            VeryGoodIcon.SetActive(true);
        }
        else if (satisfaction == "보통")
        {
            GoodIcon.SetActive(true);
        }
        else if (satisfaction == "나쁨")
        {
            BadIcon.SetActive(true);
        }
        else if (satisfaction == "매우 나쁨")
        {
            VeryBadIcon.SetActive(true);
        }
    }
}

