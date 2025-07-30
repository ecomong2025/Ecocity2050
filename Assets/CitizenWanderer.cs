using UnityEngine;

public class CitizenWanderer : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float walkDuration = 2f;
    public float idleDuration = 1.5f;
    public float zMin = -1.8f;
    public float zMax = 1.8f;
    public float startOffsetRange = 2f;

    private Rigidbody2D rb;
    private Animator animator;
    private float timer;
    private bool isWalking = false;
    private Vector2 moveDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // 시작 위치에 랜덤 오프셋 적용
        Vector3 offset = new Vector3(Random.Range(-startOffsetRange, startOffsetRange), 0f, Random.Range(zMin, zMax));
        transform.position += offset;

        timer = idleDuration;
        isWalking = false;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            if (isWalking)
            {
                // 멈추기
                moveDirection = Vector2.zero;
                rb.linearVelocity = Vector2.zero;
                animator.SetFloat("Speed", 0f);
                animator.speed = 0f;
                isWalking = false;
                timer = idleDuration;
            }
            else
            {
                // 걷기 시작
                moveDirection = GetRandomDirection();
                isWalking = true;
                timer = walkDuration;

                animator.speed = 1f;
                animator.SetFloat("Speed", 1f);
            }
        }

        if (isWalking)
        {
            rb.linearVelocity = moveDirection * moveSpeed;

            // 회전 적용 (2D에서는 Z축 회전만 사용)
            if (moveDirection != Vector2.zero)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 5f);
            }

            // Z축 범위 벗어나면 뒤돌기
            if (transform.position.z < zMin || transform.position.z > zMax)
            {
                // 이동 방향 Y 성분 반전 (Z축 대응)
                moveDirection = new Vector2(moveDirection.x, -moveDirection.y);

                // Y축 기준 180도 회전
                transform.Rotate(0f, 180f, 0f);

                // Z 위치 클램프
                float clampedZ = Mathf.Clamp(transform.position.z, zMin, zMax);
                transform.position = new Vector3(transform.position.x, transform.position.y, clampedZ);
            }
        }
    }

    // 무작위 방향 반환
    Vector2 GetRandomDirection()
    {
        float angle = Random.Range(0f, 2f * Mathf.PI);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }
}
