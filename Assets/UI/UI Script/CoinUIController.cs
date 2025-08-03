using UnityEngine;

public class CoinUIController : MonoBehaviour
{
    public int incomeAmount = 100;

    void Start()
    {
        // Collider 존재 확인 후 없으면 추가
        if (!TryGetComponent(out Collider _))
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 1f, 1f); // 3D 모델 크기에 맞게 조절
        }

        FaceCamera();
    }

    void Update()
    {
        FaceCamera();
    }

    void FaceCamera()
    {
        transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }

    void OnMouseDown()
    {
        GameManager.Instance.AddBudget(incomeAmount);
        Destroy(gameObject);
    }

    public void SetWorldPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }
}