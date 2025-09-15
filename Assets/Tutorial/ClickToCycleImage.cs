using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

[System.Serializable]
public class TutorialPage
{
    public Sprite sprite; // 해당 페이지 이미지
    public Vector2 skipButtonPos; // Skip 버튼 위치
    public bool skipButtonActive; // 버튼 활성화 여부
}

[System.Serializable]
public class PanelControl
{
    public GameObject panel; // 제어할 패널
    public bool activeOnFinish; // 마지막에 활성화할지 여부
}

public class ClickToCycleImage : MonoBehaviour
{
    [Header("Target (둘 중 하나만 지정)")]
    [SerializeField] private Image uiImage; // 튜토리얼용 UI Image
    [SerializeField] private SpriteRenderer spriteRenderer; // (안쓰면 null)

    [Header("Sprites")]
    [SerializeField] private TutorialPage[] pages; // 페이지 데이터

    [Header("Options")]
    [SerializeField] private bool loop = false; // 기본은 false (끝나면 닫히게)
    [SerializeField, Range(0f, 2f)] private float fadeDuration = 0f;
    [SerializeField] private bool clickAnywhere = true;

    [Header("Finish Action")]
    [SerializeField] private UnityEvent onFinished; // 패널 닫기 등 이벤트 연결 가능

    [Header("UI References")]
    [SerializeField] private RectTransform skipButton; // Skip 버튼

    [Header("Panel Controls")]
    [SerializeField] private PanelControl[] panelControls; // 마지막에 제어할 패널들

    private int index = 0;
    private bool isFading = false;

    // 캐시
    private RectTransform uiRect;
    private Camera mainCam;
    private Color uiBaseColor = Color.white;
    private Color spriteBaseColor = Color.white;

    void Awake()
    {
        if ((uiImage == null && spriteRenderer == null) || (uiImage != null && spriteRenderer != null))
            Debug.LogWarning("[ClickToCycleImage] uiImage 또는 spriteRenderer 중 하나만 지정하세요.");

        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("[ClickToCycleImage] pages가 비어있습니다.");
            return;
        }

        ApplyPage(0);
        uiRect = uiImage ? uiImage.rectTransform : null;
        mainCam = Camera.main;
        if (uiImage) uiBaseColor = uiImage.color;
        if (spriteRenderer) spriteBaseColor = spriteRenderer.color;
    }

    void OnEnable()
    {
        index = 0;
        if (pages != null && pages.Length > 0) ApplyPage(0);
    }

    void Update()
    {
        if (pages == null || pages.Length == 0) return;
        if (GetClickedThisFrame())
        {
            Next();
        }
    }

    private bool GetClickedThisFrame()
    {
        bool pressed = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (!pressed) return false;

        if (skipButton != null)
        {
            Vector2 clickPos = (Input.touchCount > 0) ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            if (RectTransformUtility.RectangleContainsScreenPoint(skipButton, clickPos)) return false;
            return true;
        }

        if (clickAnywhere) return true;
        if (uiRect)
        {
            Vector2 screenPos = (Input.touchCount > 0) ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            return RectTransformUtility.RectangleContainsScreenPoint(uiRect, screenPos, mainCam);
        }

        return false;
    }

    public void Next()
    {
        if (isFading || pages == null || pages.Length == 0) return;
        if (SFXPlayer.Instance != null) SFXPlayer.Instance.PlayClick();

        int next = index + 1;
        if (next >= pages.Length)
        {
            if (!loop)
            {
                // 마지막 이미지가 끝날 때 패널들 제어
                ControlPanelsOnFinish();

                onFinished?.Invoke();
                gameObject.SetActive(false);
                return;
            }
            next = 0;
        }

        index = next;

        if (fadeDuration > 0f && (uiImage != null || spriteRenderer != null))
            StartCoroutine(FadeTo(pages[index].sprite, fadeDuration));
        else ApplyPage(index);
    }

    private void ControlPanelsOnFinish()
    {
        if (panelControls == null || panelControls.Length == 0) return;

        foreach (var panelControl in panelControls)
        {
            if (panelControl.panel != null)
            {
                panelControl.panel.SetActive(panelControl.activeOnFinish);
            }
        }
    }

    private void ApplyPage(int idx)
    {
        var page = pages[idx];

        if (uiImage != null)
        {
            uiImage.sprite = page.sprite;
            if (fadeDuration > 0f) uiImage.color = new Color(uiBaseColor.r, uiBaseColor.g, uiBaseColor.b, 1f);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.sprite = page.sprite;
            if (fadeDuration > 0f) spriteRenderer.color = new Color(spriteBaseColor.r, spriteBaseColor.g, spriteBaseColor.b, 1f);
        }

        if (skipButton != null)
        {
            skipButton.anchoredPosition = page.skipButtonPos;
            skipButton.gameObject.SetActive(page.skipButtonActive);
        }
    }

    private IEnumerator FadeTo(Sprite target, float duration)
    {
        isFading = true;
        float half = Mathf.Max(0.0001f, duration * 0.5f);

        if (uiImage != null)
        {
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / half);
                uiImage.color = new Color(uiBaseColor.r, uiBaseColor.g, uiBaseColor.b, a);
                yield return null;
            }

            uiImage.sprite = target;
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, t / half);
                uiImage.color = new Color(uiBaseColor.r, uiBaseColor.g, uiBaseColor.b, a);
                yield return null;
            }
        }

        isFading = false;
        ApplyPage(index);
    }

    public void OnSkip()
    {
        // Skip 버튼을 눌렀을 때도 패널들 제어
        ControlPanelsOnFinish();
        gameObject.SetActive(false);
    }
}