using UnityEngine;

public class CitizenWanderer : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float walkDuration = 2f;
    public float idleDuration = 1.5f;
    public float zMin = -1.8f;
    public float zMax = 1.8f;
    public float startOffsetRange = 2f;

    private Rigidbody rb;
    private Animator animator;
    private float timer;
    private bool isWalking = false;
    private Vector2 moveDirection; // X, Z 평면에서 움직임

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // 시작 위치에 랜덤 오프셋 적용 (Z축 포함)
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
                rb.linearVelocity = Vector3.zero;
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
    }

    void FixedUpdate()
    {
        if (isWalking)
        {
            Vector3 velocity = new Vector3(moveDirection.x, 0, moveDirection.y) * moveSpeed;
            rb.linearVelocity = velocity;

            // 회전 (Y축 기준)
            if (moveDirection != Vector2.zero)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                transform.eulerAngles = new Vector3(0f, 90f - angle, 0f);
                // 90도 빼는 건 방향 맞추기 위해서 (필요시 조절)
            }

            // Z축 범위 
            if (transform.position.z < zMin || transform.position.z > zMax)
            {
                // 방향 Y 성분 반전 (Z축 대응)
                moveDirection = new Vector2(moveDirection.x, -moveDirection.y);

                // Y축 180도 회전 
                Vector3 rot = transform.eulerAngles;
                rot.y = (rot.y + 180f) % 360f;
                transform.eulerAngles = rot;

                // Z 위치 클램핑
                float clampedZ = Mathf.Clamp(transform.position.z, zMin, zMax);
                transform.position = new Vector3(transform.position.x, transform.position.y, clampedZ);
            }
        }
    }

    // 무작위 방향 반환 (X, Z 평면)
    Vector2 GetRandomDirection()
    {
        float angle = Random.Range(0f, 2f * Mathf.PI);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }
}