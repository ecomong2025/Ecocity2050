using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text.RegularExpressions;

public class GoEndingFromUI : MonoBehaviour
{
    [Header("UI 참조")]
    public TMP_Text budgetValueText;       // BudgetValueText
    public TMP_Text co2ValueText;          // Co2ValueText
    public TMP_Text satisfactionText;      // SatisfactionText

    [Header("씬 간 데이터 공유")]
    public ScenePayload payload;           // 같은 SO를 엔딩 씬에서도 연결

    bool isEnding;

    public void ToEndingScene()
    {
        if (isEnding) return;
        isEnding = true;
        StartCoroutine(CaptureAndGo());
    }

    System.Collections.IEnumerator CaptureAndGo()
    {
        // 마지막 프레임까지 반영되게 기다림
        yield return new WaitForEndOfFrame();

        // UI 문자열에서 숫자만 뽑아내기
        float ParseFloat(string s)
        {
            var cleaned = Regex.Replace(s, @"[^0-9\.\-]", "");
            float.TryParse(cleaned, out var v);
            return v;
        }

        int ParseInt(string s)
        {
            var cleaned = Regex.Replace(s, @"[^0-9\-]", "");
            int.TryParse(cleaned, out var v);
            return v;
        }

        // 스냅샷 저장
        payload.co2Tons = ParseFloat(co2ValueText.text);
        payload.citizenSatisfaction = ParseFloat(satisfactionText.text);
        payload.aiCityName = ""; // 엔딩씬에서 GPT로 채울 예정

        // 엔딩 씬으로 이동
        SceneManager.LoadScene("EndingScene");
    }
}
