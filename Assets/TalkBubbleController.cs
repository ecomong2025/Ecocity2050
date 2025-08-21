using UnityEngine;
using TMPro;
using System.Collections;

public class TalkBubbleController : MonoBehaviour
{
    [Header("씬에 배치된 말풍선 UI 오브젝트 (Prefab 아님!)")]
    public GameObject talkBubblePrefab;
    private TMP_Text textComponent;

    private GameManager gameManager;

    // CO2 레벨별 노출 상태
    private bool shown900 = false;
    private bool shown800 = false;
    private bool shown700 = false;
    private bool shown500 = false;
    private bool shown200 = false;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();

        if (talkBubblePrefab != null)
        {
            textComponent = talkBubblePrefab.GetComponentInChildren<TMP_Text>();
            talkBubblePrefab.SetActive(false); // 시작 시 비활성화
        }
        else
        {
            Debug.LogError("⚠ talkBubblePrefab이 연결되지 않았습니다! Inspector에서 UI 오브젝트를 연결하세요.");
        }
    }

    void Update()
    {
        if (talkBubblePrefab == null || textComponent == null) return;

        int co2 = gameManager.co2;
        Debug.Log("Current CO2: " + co2); // 값 확인용

        // 구간 벗어나면 shown 초기화
        if (!(co2 >= 900)) shown900 = false;
        if (!(co2 >= 800 && co2 < 900)) shown800 = false;
        if (!(co2 >= 700 && co2 < 800)) shown700 = false;
        if (!(co2 >= 500 && co2 < 700)) shown500 = false;
        if (!(co2 >= 200 && co2 < 500)) shown200 = false;

        // 조건 충족 시 말풍선 보여주기
        if (co2 >= 900 && !shown900) { shown900 = true; ShowBubble("이대로면 숨 쉬기도 힘들겠어요.. 아이들 건강이 걱정돼요."); }
        else if (co2 >= 800 && co2 < 900 && !shown800) { shown800 = true; ShowBubble("공기가 점점 탁해지는 것 같아요. 환경이 나빠지고 있어요"); }
        else if (co2 >= 700 && co2 < 800 && !shown700) { shown700 = true; ShowBubble("요즘 머리가 아파요.. 공기 때문일까요?"); }
        else if (co2 >= 500 && co2 < 700 && !shown500) { shown500 = true; ShowBubble("이대로 괜찮은 걸까요? 대책이 필요해요."); }
        else if (co2 >= 200 && co2 < 500 && !shown200) { shown200 = true; ShowBubble("요즘 공기가 너무 좋아요! 이 도시가 자랑스러워요."); }
    }

    void ShowBubble(string message)
    {
        // 마침표 뒤에 줄바꿈 자동 추가
        string processedMessage = message.Replace(".", ".\n");

        textComponent.text = processedMessage;
        talkBubblePrefab.SetActive(true);

        // 중복 코루틴 방지
        StopAllCoroutines();
        StartCoroutine(HideBubbleAfterSeconds(10f));
    }

    IEnumerator HideBubbleAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        talkBubblePrefab.SetActive(false);
    }
}
