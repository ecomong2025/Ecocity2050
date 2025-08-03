using UnityEngine;

public class ObjectManipulator : MonoBehaviour
{
    public bool isRotating = false;
    public bool rotateX = true;
    public bool rotateY = false;
    public bool rotateZ = false;
    public float rotationSpeed = 10f;

    public bool isFloating = false;
    public float floatHeight = 1f;
    public float floatSpeed = 1f;
    public bool useEasingForFloating = false;

    private Vector3 initialPosition;
    private float floatTimer = 0f;

    void Start()
    {
        initialPosition = transform.position;
    }

    void Update()
    {
        if (isRotating)
        {
            Vector3 rotationVector = new Vector3(
                rotateX ? 1 : 0,
                rotateY ? 1 : 0,
                rotateZ ? 1 : 0
            );
            transform.Rotate(rotationVector * rotationSpeed * Time.deltaTime);
        }

        if (isFloating)
        {
            floatTimer += Time.deltaTime * floatSpeed;
            float t = Mathf.PingPong(floatTimer, 1f);
            if (useEasingForFloating) t = EaseInOutQuad(t);

            transform.position = initialPosition + new Vector3(0, t * floatHeight, 0);
        }
    }

    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
    }
}