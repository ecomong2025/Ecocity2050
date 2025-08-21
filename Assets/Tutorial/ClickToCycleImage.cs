using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

public class ClickToCycleImage : MonoBehaviour
{
    [Header("Target (둘 중 하나만 지정)")]
    [SerializeField] private Image uiImage;                 // UGUI용
    [SerializeField] private SpriteRenderer spriteRenderer; // 2D Sprite용

    [Header("Sprites")]
    [SerializeField] private Sprite[] sprites;

    [Header("Options")]
    [SerializeField] private bool loop = true;                     // 마지막 다음에 처음으로
    [SerializeField, Range(0f, 2f)] private float fadeDuration = 0f; // 0이면 즉시 변경
    [SerializeField] private bool clickAnywhere = true;            // 화면 아무데나 클릭

    [Header("Finish Action (loop=false일 때만 사용)")]
    [SerializeField] private UnityEvent onFinished;                // 끝났을 때 실행(선택)
    [SerializeField] private string nextSceneName = "";            // 씬 이름 비우면 미사용
    [SerializeField, Range(0f, 3f)] private float sceneDelay = 0f; // 씬 전환 지연

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
        {
            Debug.LogWarning("[ClickToCycleImage] uiImage 또는 spriteRenderer 중 딱 하나만 지정하세요.");
        }

        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning("[ClickToCycleImage] sprites가 비어있습니다.");
            return;
        }

        // 시작 스프라이트 세팅
        ApplySprite(sprites[0], instant: true);

        uiRect = uiImage ? uiImage.rectTransform : null;
        mainCam = Camera.main;

        if (uiImage) uiBaseColor = uiImage.color;
        if (spriteRenderer) spriteBaseColor = spriteRenderer.color;
    }

    void Update()
    {
        if (sprites == null || sprites.Length == 0) return;

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

        if (clickAnywhere) return true;

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
        if (isFading || sprites == null || sprites.Length == 0) return;

        int next = index + 1;

        // 마지막 다음
        if (next >= sprites.Length)
        {
            if (!loop)
            {
                // 완료 처리
                onFinished?.Invoke();

                if (!string.IsNullOrEmpty(nextSceneName))
                    StartCoroutine(LoadSceneAfterDelay(nextSceneName, sceneDelay));

                return; // 더 이상 진행 X
            }
            next = 0;
        }

        index = next;

        if (fadeDuration > 0f && (uiImage != null || spriteRenderer != null))
            StartCoroutine(FadeTo(sprites[index], fadeDuration));
        else
            ApplySprite(sprites[index], instant: true);
    }

    private void ApplySprite(Sprite s, bool instant = false)
    {
        if (uiImage != null)
        {
            uiImage.sprite = s;
            if (instant && fadeDuration > 0f)
                uiImage.color = new Color(uiBaseColor.r, uiBaseColor.g, uiBaseColor.b, 1f);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.sprite = s;
            if (instant && fadeDuration > 0f)
                spriteRenderer.color = new Color(spriteBaseColor.r, spriteBaseColor.g, spriteBaseColor.b, 1f);
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
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}
