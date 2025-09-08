using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class WarningPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject warningPanel;          // 이 스크립트가 관리할 패널
    [SerializeField] private Button confirmButton;             // 선택: 확인 버튼(없으면 자동 탐색)

    [Header("Block Targets")]
    [Tooltip("WarningPanel 열릴 때 클릭/레이캐스트를 잠글 UI 루트들")]
    [SerializeField] private List<GameObject> blockTargets = new List<GameObject>();

    // CanvasGroup 원래 상태 저장용
    private struct CGState { public CanvasGroup cg; public bool i; public bool b; }
    private readonly List<CGState> locked = new List<CGState>();

    void Reset()
    {
        // 에디터에서 붙일 때 편의상 기본 할당 시도
        if (!warningPanel) warningPanel = gameObject;
    }

    void Awake()
    {
        if (!warningPanel) warningPanel = gameObject; // 이 스크립트가 패널에 붙어있다면 자기 자신
        if (warningPanel && !confirmButton)
        {
            // 하위에서 첫 번째 Button을 기본으로 사용 (원하면 인스펙터로 지정)
            confirmButton = warningPanel.GetComponentInChildren<Button>(true);
        }

        if (confirmButton)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Close);
        }

        if (warningPanel) warningPanel.SetActive(false);
    }

    void OnDisable()
    {
        // 패널이 외부에서 꺼져도 잠금 해제 보장
        UnlockBehindUI();
    }

    // === 외부에서 호출할 API ===
    public void Open()
    {
        if (!warningPanel)
        {
            Debug.LogError("[WarningPanelController] warningPanel이 비어 있습니다.");
            return;
        }

        LockBehindUI();

        warningPanel.SetActive(true);

        // 패널 자체는 조작 가능 보장
        var wcg = warningPanel.GetComponent<CanvasGroup>();
        if (wcg) { wcg.interactable = true; wcg.blocksRaycasts = true; }
    }

    public void Close()
    {
        if (warningPanel) warningPanel.SetActive(false);
        UnlockBehindUI();
    }

    // === 내부 ===
    void LockBehindUI()
    {
        locked.Clear();

        foreach (var go in blockTargets)
        {
            if (!go) continue;
            if (go == warningPanel) continue; // 자기 자신은 잠그지 않음

            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();

            locked.Add(new CGState { cg = cg, i = cg.interactable, b = cg.blocksRaycasts });
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }

    void UnlockBehindUI()
    {
        for (int i = 0; i < locked.Count; i++)
        {
            var s = locked[i];
            if (!s.cg) continue;
            s.cg.interactable   = s.i;
            s.cg.blocksRaycasts = s.b;
        }
        locked.Clear();
    }
}