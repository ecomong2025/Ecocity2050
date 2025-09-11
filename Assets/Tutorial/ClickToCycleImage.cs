using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

[System.Serializable]
public class TutorialPage
{
    public Sprite sprite;          // 해당 페이지 이미지
    public Vector2 skipButtonPos;  // Skip 버튼 위치
    public bool skipButtonActive;  // 버튼 활성화 여부
}

public class ClickToCycleImage : MonoBehaviour
{
    [Header("Target (둘 중 하나만 지정)")]
    [SerializeField] private Image uiImage;                 // UGUI용
    [SerializeField] private SpriteRenderer spriteRenderer; // 2D Sprite용

    [Header("Sprites")]
    [SerializeField] private TutorialPage[] pages;          // 기존 sprites 대신 TutorialPage 사용

    [Header("Options")]
    [SerializeField] private bool loop = true;                     // 마지막 다음에 처음으로
    [SerializeField, Range(0f, 2f)] private float fadeDuration = 0f; // 0이면 즉시 변경
    [SerializeField] private bool clickAnywhere = true;            // 화면 아무데나 클릭

    [Header("Finish Action (loop=false일 때만 사용)")]
    [SerializeField] private UnityEvent onFinished;                // 끝났을 때 실행(선택)
    [SerializeField] private string nextSceneName = "";            // 씬 이름 비우면 미사용
    [SerializeField, Range(0f, 3f)] private float sceneDelay = 0f; // 씬 전환 지연

    [Header("UI References")]
    [SerializeField] private RectTransform skipButton; // Skip 버튼

    private int index = 0;
    private bool isFading = false;

    // 캐시
    private RectTransform uiRect;
    private Camera mainCam;
    private Color uiBaseColor = Color.white;
    private Color spriteBaseColor = Color.white;

    void Awake()
    {
        // 타겟 유효성 검사
        if ((uiImage == null && spriteRenderer == null) || (uiImage != null && spriteRenderer != null))
            Debug.LogWarning("[ClickToCycleImage] uiImage 또는 spriteRenderer 중 딱 하나만 지정하세요.");

        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("[ClickToCycleImage] pages가 비어있습니다.");
            return;
        }

        // 시작 페이지 세팅
        ApplyPage(0);

        uiRect = uiImage ? uiImage.rectTransform : null;
        mainCam = Camera.main;

        if (uiImage) uiBaseColor = uiImage.color;
        if (spriteRenderer) spriteBaseColor = spriteRenderer.color;
    }

    void Update()
    {
        if (pages == null || pages.Length == 0) return;

        if (GetClickedThisFrame())
        {
            Next();
        }
    }

    /// <summary>클릭 입력 판단</summary>
    private bool GetClickedThisFrame()
    {
        // 터치/마우스 다운 공통
        bool pressed = Input.GetMouseButtonDown(0) ||
                       (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (!pressed) return false;

        // UI 버튼 위 클릭이면 Next() 호출 방지
        if (skipButton != null)
        {
            if (skipButton != null)
            {
                Vector2 clickPos = (Input.touchCount > 0) ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
                if (RectTransformUtility.RectangleContainsScreenPoint(skipButton, clickPos))
                    return false;
            }

            // 클릭 허용
            return true;
        }

        if (clickAnywhere)
        {
            // SFXPlayer.Instance.PlayClick();  // 클릭 효과음 실행 (필요 시 활성화)
            return true;
        }

        // 특정 대상만 클릭해야 하는 경우
        // 1) UI(Image)일 때: 이 오브젝트의 RectTransform 영역 안인지 확인
        if (uiRect)
        {
            Vector2 screenPos = (Input.touchCount > 0) ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            return RectTransformUtility.RectangleContainsScreenPoint(uiRect, screenPos, mainCam);
        }

        // 2) 스프라이트(월드)일 때: 레이캐스트로 이 오브젝트의 콜라이더를 맞췄는지 검사
        if (mainCam == null) mainCam = Camera.main;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit3D))
            return hit3D.transform == transform;
        if (Physics2D.Raycast(mainCam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, 0f, ~0))
        {
            var hit2D = Physics2D.Raycast(mainCam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit2D.collider != null) return hit2D.collider.transform == transform;
        }
        return false;
    }

    public void Next()
    {
        if (isFading || pages == null || pages.Length == 0) return;

        int next = index + 1;

        // 마지막 다음
        if (next >= pages.Length)
        {
            if (!loop)
            {
                // 완료 처리
                onFinished?.Invoke();

                if (!string.IsNullOrEmpty(nextSceneName))
                {
                    PlayerPrefs.SetInt("FromTutorial", 1); // ✅ 튜토리얼에서 넘어간다는 표시
                    PlayerPrefs.Save();
                    StartCoroutine(LoadSceneAfterDelay(nextSceneName, sceneDelay));
                }

                return; // 더 이상 진행 X
            }
            next = 0;
        }

        index = next;

        if (fadeDuration > 0f && (uiImage != null || spriteRenderer != null))
            StartCoroutine(FadeTo(pages[index].sprite, fadeDuration));
        else
            ApplyPage(index);
    }

    /// <summary>이미지 적용 + 버튼 위치/활성화 설정</summary>
    private void ApplyPage(int idx)
    {
        var page = pages[idx];

        // 이미지 적용
        if (uiImage != null)
        {
            uiImage.sprite = page.sprite;
            if (fadeDuration > 0f)
                uiImage.color = new Color(uiBaseColor.r, uiBaseColor.g, uiBaseColor.b, 1f);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.sprite = page.sprite;
            if (fadeDuration > 0f)
                spriteRenderer.color = new Color(spriteBaseColor.r, spriteBaseColor.g, spriteBaseColor.b, 1f);
        }

        // Skip 버튼 위치 및 활성화
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
            // 페이드 아웃
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / half);
                uiImage.color = new Color(uiBaseColor.r, uiBaseColor.g, uiBaseColor.b, a);
                yield return null;
            }

            uiImage.sprite = target;

            // 페이드 인
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, t / half);
                uiImage.color = new Color(uiBaseColor.r, uiBaseColor.g, uiBaseColor.b, a);
                yield return null;
            }
        }
        else if (spriteRenderer != null)
        {
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / half);
                spriteRenderer.color = new Color(spriteBaseColor.r, spriteBaseColor.g, spriteBaseColor.b, a);
                yield return null;
            }

            spriteRenderer.sprite = target;

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, t / half);
                spriteRenderer.color = new Color(spriteBaseColor.r, spriteBaseColor.g, spriteBaseColor.b, a);
                yield return null;
            }
        }

        isFading = false;

        // 페이지 적용 후 버튼 위치 및 활성화 갱신
        ApplyPage(index);
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Skip 버튼 클릭 이벤트</summary>
    public void OnSkip()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }
}
