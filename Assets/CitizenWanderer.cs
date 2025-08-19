using UnityEngine;

public class CitizenWanderer : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkDuration = 10f;
    public float idleDuration = 3f;

    [Header("Animation Settings")]
    public float animationSpeed = 0.8f;
    public float speedChangeRate = 1f;

    public void OnNewBuildingInstalled() { }

    private Animator animator;
    private float timer;
    private bool isWalking = false;

    // 애니메이션 부드러움을 위한 변수
    private float currentAnimationSpeed = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();

        timer = idleDuration;
        isWalking = false;
        currentAnimationSpeed = 0f;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            if (isWalking)
            {
                StopWalking();
            }
            else
            {
                StartWalking();
            }
        }

        UpdateAnimation();
    }

    void StartWalking()
    {
        // 랜덤 회전 (90, -90, 180 중 하나)
        float[] angles = { -90f, 90f, 180f };
        float randomAngle = angles[Random.Range(0, angles.Length)];
        transform.Rotate(0, randomAngle, 0);

        isWalking = true;
        timer = walkDuration;
    }

    void StopWalking()
    {
        isWalking = false;
        timer = idleDuration;
    }

    void UpdateAnimation()
    {
        if (animator == null) return;

        float targetAnimSpeed = isWalking ? animationSpeed : 0f;
        currentAnimationSpeed = Mathf.Lerp(currentAnimationSpeed, targetAnimSpeed, Time.deltaTime * speedChangeRate);

        animator.SetFloat("Speed", currentAnimationSpeed);
        animator.speed = isWalking ? 1f : Mathf.Max(0.1f, currentAnimationSpeed);
    }
}
