using UnityEngine;

public class CitizenWanderer : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 0.5f;
    public float runSpeed = 1.0f;
    public float walkDuration = 8f;
    public float idleDuration = 4f;
    public float rotationSpeed = 45f;

    [Header("Area Bounds")]
    public float zMin = -1.8f;
    public float zMax = 1.8f;
    public float xMin = -8f;
    public float xMax = 8f;
    public float startOffsetRange = 3f;

    [Header("Behavior Settings")]
    public float directionChangeChance = 0.1f;
    public float speedVariation = 0.3f;
    public float pauseChance = 0.2f;
    public float lookAroundChance = 0.15f;

    private Rigidbody rb;
    private Animator animator;
    private float timer;
    private bool isWalking = false;
    private bool isRotating = false;
    private float currentSpeed;
    private Vector3 currentDirection;
    private Vector3 targetDirection;
    private float currentWalkDuration;
    private float currentIdleDuration;
    private bool useTransform = false;

    private float speedMultiplier = 1f;
    private float rotationTimer = 0f;
    private bool isPausing = false;
    private bool isLookingAround = false;
    private float lookAroundTimer = 0f;

    private Vector3[] primaryDirections = {
        Vector3.forward,
        Vector3.back,
        Vector3.right,
        Vector3.left
    };

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (rb != null && rb.isKinematic)
        {
            useTransform = true;
        }

        Vector3 offset = new Vector3(
            Random.Range(-startOffsetRange, startOffsetRange),
            0f,
            Random.Range(zMin, zMax)
        );
        transform.position += offset;

        currentDirection = GetRandomDirection();
        targetDirection = currentDirection;
        SetInitialRotation();
        currentSpeed = walkSpeed;

        speedVariation = Random.Range(0.8f, 1.2f);
        walkSpeed *= speedVariation;
        runSpeed *= speedVariation;

        StartIdling();
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (isLookingAround)
        {
            HandleLookAround();
            return;
        }

        if (isRotating)
        {
            HandleRotation();
            return;
        }

        if (isPausing)
        {
            if (timer <= 0f)
            {
                isPausing = false;
                StartWalking();
            }
            return;
        }

        if (timer <= 0f)
        {
            if (isWalking)
            {
                if (Random.Range(0f, 1f) < lookAroundChance)
                {
                    StartLookingAround();
                }
                else
                {
                    StartIdling();
                }
            }
            else
            {
                if (Random.Range(0f, 1f) < pauseChance)
                {
                    StartPausing();
                }
                else
                {
                    StartWalking();
                }
            }
        }

        if (isWalking && Random.Range(0f, 1f) < directionChangeChance * Time.deltaTime)
        {
            ChangeDirection();
        }

        if (isWalking)
        {
            speedMultiplier = Mathf.Lerp(speedMultiplier,
                Random.Range(0.8f, 1.2f), Time.deltaTime * 0.5f);
        }
    }

    void FixedUpdate()
    {
        if (isWalking && !isRotating && !isPausing && !isLookingAround)
        {
            float actualSpeed = currentSpeed * speedMultiplier;
            Vector3 movement = currentDirection * actualSpeed * Time.fixedDeltaTime;

            if (useTransform)
            {
                transform.position += movement;
            }
            else
            {
                rb.linearVelocity = new Vector3(
                    currentDirection.x * actualSpeed,
                    rb.linearVelocity.y,
                    currentDirection.z * actualSpeed
                );
            }

            CheckBoundaries();
        }
        else if (!useTransform && rb != null)
        {
            Vector3 currentVel = rb.linearVelocity;
            rb.linearVelocity = new Vector3(
                Mathf.Lerp(currentVel.x, 0, Time.fixedDeltaTime * 5f),
                currentVel.y,
                Mathf.Lerp(currentVel.z, 0, Time.fixedDeltaTime * 5f)
            );
        }
    }

    void StartWalking()
    {
        isWalking = true;
        isPausing = false;
        currentWalkDuration = Random.Range(walkDuration * 0.5f, walkDuration * 1.5f);
        timer = currentWalkDuration;

        currentSpeed = Random.Range(0f, 1f) < 0.1f ? runSpeed : walkSpeed;

        if (animator != null)
        {
            float animSpeed = currentSpeed / walkSpeed;
            animator.speed = Random.Range(0.9f, 1.1f) * animSpeed;
            animator.SetFloat("Speed", animSpeed);
        }
    }

    void StartIdling()
    {
        isWalking = false;
        isPausing = false;
        currentIdleDuration = Random.Range(idleDuration * 0.3f, idleDuration * 1.2f);
        timer = currentIdleDuration;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.speed = Random.Range(0.5f, 1f);
        }
    }

    void StartPausing()
    {
        isPausing = true;
        timer = Random.Range(0.5f, 2f);

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
    }

    void StartLookingAround()
    {
        isLookingAround = true;
        isWalking = false;
        lookAroundTimer = Random.Range(2f, 4f);

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
    }

    void HandleLookAround()
    {
        lookAroundTimer -= Time.deltaTime;

        float lookAngle = Mathf.Sin(Time.time * 2f) * 30f;
        Vector3 currentRotation = transform.eulerAngles;
        Quaternion targetRotation = Quaternion.Euler(0, currentRotation.y + lookAngle, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);

        if (lookAroundTimer <= 0f)
        {
            isLookingAround = false;
            ChangeDirection();
            StartIdling();
        }
    }

    void ChangeDirection()
    {
        Vector3 newDirection = GetRandomDirection();
        if (newDirection != currentDirection)
        {
            targetDirection = newDirection;
            StartRotating();
        }
    }

    void StartRotating()
    {
        isRotating = true;
        rotationTimer = 0f;
    }

    void HandleRotation()
    {
        rotationTimer += Time.deltaTime;

        currentDirection = Vector3.Slerp(currentDirection, targetDirection,
            Time.deltaTime * (rotationSpeed / 90f));

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                Time.deltaTime * (rotationSpeed / 90f));
        }

        if (Vector3.Angle(currentDirection, targetDirection) < 5f || rotationTimer > 2f)
        {
            currentDirection = targetDirection;
            isRotating = false;
        }
    }

    Vector3 GetRandomDirection()
    {
        return primaryDirections[Random.Range(0, primaryDirections.Length)];
    }

    void SetInitialRotation()
    {
        if (currentDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(currentDirection);
        }
    }

    void CheckBoundaries()
    {
        Vector3 pos = transform.position;
        bool shouldChangeDirection = false;

        if (pos.x <= xMin || pos.x >= xMax || pos.z <= zMin || pos.z >= zMax)
        {
            Vector3 clampedPos = new Vector3(
                Mathf.Clamp(pos.x, xMin + 0.1f, xMax - 0.1f),
                pos.y,
                Mathf.Clamp(pos.z, zMin + 0.1f, zMax - 0.1f)
            );
            transform.position = clampedPos;
            shouldChangeDirection = true;
        }

        if (shouldChangeDirection)
        {
            Vector3 centerDirection = (Vector3.zero - transform.position).normalized;
            centerDirection.y = 0;
            targetDirection = centerDirection;
            StartRotating();
        }
    }

    // 시민 상태를 초기화하고 새로운 위치에서 걷기 시작하게 함
    public void ResetWandering()
    {
        isWalking = false;
        isRotating = false;
        isPausing = false;
        isLookingAround = false;

        currentDirection = GetRandomDirection();
        targetDirection = currentDirection;

        SetInitialRotation();
        StartIdling();
    }
}
