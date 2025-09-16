using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

namespace TutorialScene
{
    [System.Serializable]
    public class TutorialPage
    {
        public Sprite sprite;          // 페이지 이미지
        public Vector2 skipButtonPos;  // Skip 버튼 위치
        public bool skipButtonActive;  // 활성화 여부
    }

    public class ClickTo : MonoBehaviour
    {
        [Header("Target (둘 중 하나만 지정)")]
        [SerializeField] private Image uiImage;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Sprites")]
        [SerializeField] private TutorialPage[] pages;

        [Header("Options")]
        [SerializeField] private bool loop = true;
        [SerializeField, Range(0f, 2f)] private float fadeDuration = 0f;
        [SerializeField] private bool clickAnywhere = true;

        [Header("Finish Action")]
        [SerializeField] private UnityEvent onFinished;
        [SerializeField] private string nextSceneName = "";
        [SerializeField, Range(0f, 3f)] private float sceneDelay = 0f;

        [Header("UI References")]
        [SerializeField] private RectTransform skipButton;

        private int index = 0;
        private bool isFading = false;
        private RectTransform uiRect;
        private Camera mainCam;
        private Color uiBaseColor = Color.white;
        private Color spriteBaseColor = Color.white;

        void Awake()
        {
            if ((uiImage == null && spriteRenderer == null) || (uiImage != null && spriteRenderer != null))
                Debug.LogWarning("[ClickTo] uiImage 또는 spriteRenderer 중 하나만 지정하세요.");

            if (pages == null || pages.Length == 0)
            {
                Debug.LogWarning("[ClickTo] pages가 비어있습니다.");
                return;
            }

            ApplyPage(0);
            uiRect = uiImage ? uiImage.rectTransform : null;
            mainCam = Camera.main;
            if (uiImage) uiBaseColor = uiImage.color;
            if (spriteRenderer) spriteBaseColor = spriteRenderer.color;
        }

        void Update()
        {
            if (pages == null || pages.Length == 0) return;
            if (GetClickedThisFrame()) Next();
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


            int next = index + 1;
            if (next >= pages.Length)
            {
                if (!loop)
                {
                    onFinished?.Invoke();
                    if (!string.IsNullOrEmpty(nextSceneName))
                        StartCoroutine(LoadSceneAfterDelay(nextSceneName, sceneDelay));
                    return;
                }
                next = 0;
            }

            index = next;

            if (SFXPlayer.Instance != null)
                SFXPlayer.Instance.PlayClick();
            if (fadeDuration > 0f && (uiImage != null || spriteRenderer != null))
                StartCoroutine(FadeTo(pages[index].sprite, fadeDuration));
            else
                ApplyPage(index);
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
            ApplyPage(index);
        }

        private IEnumerator LoadSceneAfterDelay(string GameScene, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(GameScene);
        }
    }
}
