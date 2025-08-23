using UnityEngine;

public class CoinUIController : MonoBehaviour
{
    public int incomeAmount = 100;

    // ▼ 추가: 클릭 사운드
    [SerializeField] private AudioClip clickSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    void Start()
    {
        // Collider 보정 (그대로 유지)
        if (!TryGetComponent(out Collider _))
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 1f, 1f);
        }
        FaceCamera();
    }

    void Update() => FaceCamera();

    void FaceCamera()
    {
        if (Camera.main == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }

    void OnMouseDown()
    {
        // 효과음 재생
        var sfxPlayer = GameObject.Find("SFXPlayer");
        if (sfxPlayer != null && clickSfx != null)
        {
            var src = sfxPlayer.GetComponent<AudioSource>();
            if (src != null) src.PlayOneShot(clickSfx, sfxVolume);
        }

        GameManager.Instance.AddBudget(incomeAmount);
        Destroy(gameObject);
    }

    public void SetWorldPosition(Vector3 worldPosition) => transform.position = worldPosition;
}