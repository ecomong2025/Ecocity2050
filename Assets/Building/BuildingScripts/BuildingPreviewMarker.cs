using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class BuildingPreviewMarker : MonoBehaviour
{
    private static int _globalSeq = 0;

    [Tooltip("가장 최근 생성/활성된 프리뷰를 고르기 위한 시퀀스 번호")]
    public int seq { get; private set; }

    [Tooltip("부모가 생기면(설치된 것으로 간주) 마커를 자동 제거")]
    public bool autoRemoveWhenParented = true;

    Vector3 _lastPos;
    float _lastBumpTime;
    const float MIN_BUMP_INTERVAL = 0.15f;

    public void ForceBump(string reason = "Force")
    {
        if (Time.unscaledTime - _lastBumpTime < MIN_BUMP_INTERVAL) return;
        seq = ++_globalSeq;
        _lastBumpTime = Time.unscaledTime;
        _lastPos = transform.position;
        // Debug.Log($"[Marker] BUMP {name} seq={seq} reason={reason}");
    }

    void Awake() { ForceBump("Awake"); }
    void OnEnable() { ForceBump("OnEnable"); }

    void LateUpdate()
    {
        if ((transform.position - _lastPos).sqrMagnitude > 0.0001f)
            ForceBump("Moved");
    }

    void OnTransformParentChanged()
    {
        if (autoRemoveWhenParented && transform.parent != null)
            Destroy(this); // 설치되면 후보 제외
    }
}
