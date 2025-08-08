using TMPro;
using UnityEngine;
using TMPro;

public class QuizTimer : MonoBehaviour
{
    public TMP_Text timerTxt;
    public float time = 10f;
    private float countdown;
    private bool isRunning = false;

    public System.Action OnTimeout;

    public void StartTimer()
    {
        countdown = time;
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    private void Update()
    {
        if (!isRunning) return;

        if (countdown > 0)
        {
            countdown -= Time.deltaTime;
            timerTxt.text = Mathf.Floor(countdown).ToString();
        }
        else
        {
            timerTxt.text = "0";
            isRunning = false;
            OnTimeout?.Invoke(); // 타임아웃 처리
        }
    }
}
