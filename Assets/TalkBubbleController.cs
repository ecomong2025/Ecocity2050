using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TalkBubbleController : MonoBehaviour
{
    [Header("Bubble UI")]
    public GameObject talkBubble; // Panel
    public TMP_Text bubbleText;

    [Header("Block When These Panels Are Open")]
    [Tooltip("이 리스트 안에 있는 패널이 켜져 있으면 말풍선 숨김")]
    [SerializeField] private List<GameObject> blockPanels = new List<GameObject>();

    public float showTime = 5f;

    private int currentRange = -1;
    private Coroutine bubbleCoroutine;

    void Start()
    {
        // GameManager에 퀴즈 패널이 있다면 자동으로 추가 (인스펙터 비워둬도 OK)
        if (GameManager.Instance != null && GameManager.Instance.quizMainPanel != null 
            && !blockPanels.Contains(GameManager.Instance.quizMainPanel))
        {
            blockPanels.Add(GameManager.Instance.quizMainPanel);
        }

        if (talkBubble != null)
            talkBubble.SetActive(false); // 시작 시 비활성화
        currentRange = -1;
    }

    void Update()
    {
        // 패널이 열리면 떠 있는 말풍선 즉시 숨김
        if (talkBubble != null && talkBubble.activeSelf && IsBlockedByAnyPanel())
        {
            if (bubbleCoroutine != null)
            {
                StopCoroutine(bubbleCoroutine);
                bubbleCoroutine = null;
            }
            talkBubble.SetActive(false);
        }
    }

    public void ShowBubble(float co2Value)
    {
        if (IsBlockedByAnyPanel())
            return;

        int newRange = GetRange(co2Value);

        if (newRange == currentRange)
            return;

        currentRange = newRange;

        if (newRange == -1)
        {
            if (talkBubble != null && talkBubble.activeSelf)
                talkBubble.SetActive(false);
            return;
        }

        if (bubbleText != null)
            bubbleText.text = GetTextForRange(newRange);

        if (bubbleCoroutine != null)
            StopCoroutine(bubbleCoroutine);

        bubbleCoroutine = StartCoroutine(ShowBubbleCoroutine());
    }

    private IEnumerator ShowBubbleCoroutine()
    {
        if (talkBubble != null)
            talkBubble.SetActive(true);

        float t = 0f;
        while (t < showTime)
        {
            if (IsBlockedByAnyPanel())
                break;

            t += Time.deltaTime;
            yield return null;
        }

        if (talkBubble != null)
            talkBubble.SetActive(false);
        bubbleCoroutine = null;
    }

    private bool IsBlockedByAnyPanel()
    {
        // 리스트에 있는 패널 중 하나라도 열려 있으면 true
        foreach (var panel in blockPanels)
        {
            if (panel != null && panel.activeInHierarchy)
                return true;
        }

        // 설치중이면 차단
        bool placing = GameManager.Instance != null && GameManager.Instance.IsPlacingBuilding;
        return placing;
    }

    private int GetRange(float co2)
    {
        if (co2 >= 100 && co2 <= 300) return 0;
        else if (co2 >= 301 && co2 <= 450) return 1;
        else if (co2 >= 451 && co2 <= 650) return 2;
        else if (co2 >= 651 && co2 <= 750) return 3;
        else if (co2 >= 751 && co2 <= 850) return 4;
        else if (co2 >= 851) return 5;
        else return -1;
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