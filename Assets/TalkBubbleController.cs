using UnityEngine;
using TMPro;
using System.Collections;

public class TalkBubbleController : MonoBehaviour
{
    public GameObject talkBubble; // Panel
    public TMP_Text bubbleText;

    public float showTime = 5f;
    private int currentRange = -1;
    private Coroutine bubbleCoroutine;

    void Start()
    {
        if (talkBubble != null)
            talkBubble.SetActive(false); // 시작 시 비활성화
        currentRange = -1;
    }

    public void ShowBubble(float co2Value)
    {
        int newRange = GetRange(co2Value);

        // 같은 범위면 아무것도 하지 않음
        if (newRange == currentRange)
            return;

        currentRange = newRange;

        // 범위 벗어나면 말풍선 숨기기
        if (newRange == -1)
        {
            if (talkBubble.activeSelf)
                talkBubble.SetActive(false);
            return;
        }

        bubbleText.text = GetTextForRange(newRange);

        // 기존 코루틴 중지 후 새로 시작
        if (bubbleCoroutine != null)
            StopCoroutine(bubbleCoroutine);

        bubbleCoroutine = StartCoroutine(ShowBubbleCoroutine());
    }

    private IEnumerator ShowBubbleCoroutine()
    {
        talkBubble.SetActive(true); // Panel 표시
        yield return new WaitForSeconds(showTime);
        talkBubble.SetActive(false); // 5초 뒤 Panel과 Text 모두 숨김
        bubbleCoroutine = null;
    }

    private int GetRange(float co2)
    {
        if (co2 >= 100 && co2 <= 300) return 0;
        else if (co2 >= 301 && co2 <= 450) return 1;
        else if (co2 >= 451 && co2 <= 650) return 2;
        else if (co2 >= 651 && co2 <= 750) return 3;
        else if (co2 >= 751 && co2 <= 850) return 4;
        else if (co2 >= 851) return 5;
        else return -1; // 범위 밖이면 말풍선 안 뜸
    }

    private string GetTextForRange(int range)
    {
        switch (range)
        {
            case 0: return "요즘 공기가 너무 좋아요!\n이 도시가 자랑스러워요.";
            case 1: return "이 정도면 꽤 좋은 도시군요.\n앞으로도 잘 유지해주세요!";
            case 2: return "이대로 괜찮은 걸까요?\n대책이 필요해요.";
            case 3: return "요즘 머리가 아파요…\n공기 때문일까요?";
            case 4: return "공기가 점점 탁해지는 것 같아요.\n환경이 나빠지고 있어요.";
            case 5: return "이대로면 숨 쉬기도 힘들겠어요…\n아이들 건강이 걱정돼요.";
            default: return "";
        }
    }



    public void ResetRange()
    {
        currentRange = -1;
    }
}
