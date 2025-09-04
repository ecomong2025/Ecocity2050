using UnityEngine;

public class CoinUIController : MonoBehaviour
{
    public int incomeAmount = 100;

    [SerializeField] private AudioClip clickSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    void Start()
    {
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
        // 특정 UI가 열려있으면 클릭 무시
        if (IsPanelActive("BuildingPanel") ||
            IsPanelActive("BuildingInstallPanel") ||
            IsPanelActive("ChatPanel") ||
            IsPanelActive("QuestUI") ||
            IsPanelActive("NewsOverlayCanvas"))
            return;

        var sfxPlayer = GameObject.Find("SFXPlayer");
        if (sfxPlayer != null && clickSfx != null)
        {
            var src = sfxPlayer.GetComponent<AudioSource>();
            if (src != null) src.PlayOneShot(clickSfx, sfxVolume);
        }

        GameManager.Instance.AddBudget(incomeAmount);
        Destroy(gameObject);
    }

    bool IsPanelActive(string panelName)
    {
        var go = GameObject.Find(panelName);
        return go != null && go.activeInHierarchy;
    }

    public void SetWorldPosition(Vector3 worldPosition) => transform.position = worldPosition;
}