using UnityEngine;

public class CitizenWanderer : MonoBehaviour
{
    public float moveSpeed = 1.5f;          // �̵� �ӵ�
    public float walkDuration = 2f;         // �ȴ� �ð�
    public float idleDuration = 1.5f;       // ���ߴ� �ð�
    public float zMin = -1.4f;              // Z�� �̵� ���� �ּҰ�
    public float zMax = 1f;                 // Z�� �̵� ���� �ִ밪
    public float startOffsetRange = 2f;     // ���� ��ġ ���� ����

    private Rigidbody2D rb;
    private Animator animator;
    private float timer;
    private bool isWalking = false;
    private Vector2 moveDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // ���� ��ġ ���� ������ ����
        Vector3 offset = new Vector3(Random.Range(-startOffsetRange, startOffsetRange), 0, Random.Range(zMin, zMax));
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
                // ���߱�
                moveDirection = Vector2.zero;
                rb.linearVelocity = Vector2.zero;
                animator.SetFloat("Speed", 0f);
                animator.speed = 0f;
                isWalking = false;
                timer = idleDuration;
            }
            else
            {
                // �ȱ� ����
                moveDirection = GetRandomDirection();
                isWalking = true;
                timer = walkDuration;

                animator.speed = 1f;
                animator.SetFloat("Speed", 1f); // �ȴ� ���·� ����
            }
        }

        if (isWalking)
        {
            rb.linearVelocity = moveDirection * moveSpeed;

            // ȸ�� (���� ����)
            if (moveDirection != Vector2.zero)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, 0, angle), Time.deltaTime * 5f);
            }

            // z�� ���� (2D �󿡼��� z�� ��� �� ������ ���������� ���ܵ�)
            float clampedZ = Mathf.Clamp(transform.position.z, zMin, zMax);
            transform.position = new Vector3(transform.position.x, transform.position.y, clampedZ);
        }
    }

    // ������ ���� ����
    Vector2 GetRandomDirection()
    {
        float angle = Random.Range(0f, 2f * Mathf.PI);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }
}
