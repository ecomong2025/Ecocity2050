using UnityEngine;
using TMPro;
using System.Collections;

public class TalkBubbleController : MonoBehaviour
{
    public GameObject talkBubblePrefab;
    public Transform talkPoint;

    private GameObject currentBubble;
    private bool isShowingBubble = false;

    // CO2 레벨별 노출 상태
    private bool shown900 = false;
    private bool shown800 = false;
    private bool shown700 = false;
    private bool shown500 = false;
    private bool shown200 = false;

    private int lastCo2Level = 0;

    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        lastCo2Level = gameManager.co2; // 초기값 세팅
    }

    void Update()
    {
        int co2 = gameManager.co2;

        if (!isShowingBubble)
        {
            if (co2 >= 900 && lastCo2Level < 900 && !shown900)
            {
                shown900 = true;
                ShowBubble("이대로면 숨 쉬기도 힘들겠어요..\n아이들 건강이 걱정돼요.");
            }
            else if (co2 >= 800 && lastCo2Level < 800 && !shown800)
            {
                shown800 = true;
                ShowBubble("공기가 점점 탁해지는 것 같아요.\n환경이 나빠지고 있어요");
            }
            else if (co2 >= 700 && lastCo2Level < 700 && !shown700)
            {
                shown700 = true;
                ShowBubble("요즘 머리가 아파요..\n공기 때문일까요?");
            }
            else if (co2 >= 200 && lastCo2Level < 500 && !shown500)
            {
                shown500 = true;
                ShowBubble("이대로 괜찮은 걸까요?\n대책이 필요해요.");
            }
            else if (co2 >= 10 && lastCo2Level < 200 && !shown200)
            {
                shown200 = true;
                ShowBubble("요즘 공기가 너무 좋아요!\n이 도시가 자랑스러워요.");
            }
        }

        // 하강 시 구간 리셋
        if (co2 < 900 && shown900) shown900 = false;
        if (co2 < 800 && shown800) shown800 = false;
        if (co2 < 700 && shown700) shown700 = false;
        if (co2 < 500 && shown500) shown500 = false;
        if (co2 < 200 && shown200) shown200 = false;

        lastCo2Level = co2;
    }

    void ShowBubble(string message)
    {
        if (currentBubble != null)
            Destroy(currentBubble);

        currentBubble = Instantiate(talkBubblePrefab, talkPoint.position, Quaternion.identity, talkPoint);

        TMP_Text textComponent = currentBubble.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
            textComponent.text = message;

        isShowingBubble = true;
        StartCoroutine(HideBubbleAfterSeconds(10f));
    }

    IEnumerator HideBubbleAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (currentBubble != null)
            Destroy(currentBubble);

        isShowingBubble = false;
    }
}
