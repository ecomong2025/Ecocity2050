using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;

namespace Ecocity.News
{
    [Serializable] class OpenAIKeyConfig { public string openai_api_key; }

    // Chat 파싱
    [Serializable] class ChatMessage { public string role; public string content; }
    [Serializable] class ChatChoice { public ChatMessage message; }
    [Serializable] class ChatResp { public List<ChatChoice> choices; }

    // 뉴스 JSON
    [Serializable] public class NewsPayload
    {
        public string headline;
        public string blurb;
    }

    // 이미지 응답 (URL 기반)
    [Serializable] class ImageDatum { public string url; }
    [Serializable] class ImageResp { public List<ImageDatum> data; }

    public class NewsOverlayManager : MonoBehaviour
    {
        [Header("UI")]
        public GameObject root;         // NewsOverlayCanvas (전체 오브젝트)
        public CanvasGroup canvasGroup; // 페이드용
        public RawImage newsImage;      // 최종 표시

        [Header("Timing")]
        public float holdSeconds = 4f;
        public float fadeSeconds = 0.35f;

        [Header("Image")]
        public FilterMode imageFilter = FilterMode.Bilinear;

        string apiKey;

        void Awake()
        {
            var ta = Resources.Load<TextAsset>("api_key");
            if (ta != null)
            {
                try { apiKey = JsonUtility.FromJson<OpenAIKeyConfig>(ta.text).openai_api_key; }
                catch { Debug.LogError("[News] api_key.json 파싱 실패"); }
            }
            else Debug.LogError("[News] Resources/api_key.json 없음");
        }

        public void RequestAndShowNews()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[News] OpenAI API 키가 비어 있음");
                return;
            }
            StopAllCoroutines();
            StartCoroutine(Flow());
        }

        IEnumerator Flow()
        {
            // 1) 한글 헤드라인/블럽 생성
            NewsPayload payload = null;
            yield return StartCoroutine(FetchNewsJson(p => payload = p));
            if (payload == null) yield break;

            // 2) 헤드라인을 포함한 뉴스 전체 이미지 생성 → URL 다운로드
            Texture2D tex = null;
            yield return StartCoroutine(GenerateNewsImage(payload, t => tex = t));
            if (tex == null) yield break;

            // 3) 오버레이 표시/페이드
            ApplyUI(tex);
            yield return StartCoroutine(Fade(true, fadeSeconds));
            yield return new WaitForSeconds(holdSeconds);
            yield return StartCoroutine(Fade(false, fadeSeconds));

            root.SetActive(false);
            Destroy(tex);
        }

        // ---------- (A) Chat: 헤드라인/블럽 생성(JSON 강제) ----------
        IEnumerator FetchNewsJson(Action<NewsPayload> onDone)
        {
            var system = new ChatMessage {
                role = "system",
                content = "You are a Korean climate news editor. " +
                          "Output ONLY compact JSON with keys: headline, blurb. No markdown."
            };

            var user = new ChatMessage {
                role = "user",
                content =
                "탄소중립·재생에너지·온실가스 감축 주제로 한국 로컬 톤의 뉴스 헤드라인(26자 내외)과 " +
                "짧은 보조 설명(blurb, 30자 내외)을 만들어주세요."
            };

            string body =
$@"{{
  ""model"": ""gpt-4o-mini"",
  ""temperature"": 0.8,
  ""messages"": [
    {{""role"": ""system"", ""content"": {JsonEscape(system.content)}}},
    {{""role"": ""user"",   ""content"": {JsonEscape(user.content)}}}
  ],
  ""response_format"": {{""type"": ""json_object""}}
}}";

            var req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[News] Chat API 실패: " + req.responseCode + " / " + req.error + "\n" + req.downloadHandler.text);
                onDone?.Invoke(null);
                yield break;
            }

            try
            {
                var resp = JsonUtility.FromJson<ChatResp>(req.downloadHandler.text);
                var content = resp.choices[0].message.content;
                var news = JsonUtility.FromJson<NewsPayload>(content);
                onDone?.Invoke(news);
            }
            catch (Exception e)
            {
                Debug.LogError("[News] JSON 파싱 실패: " + e.Message + "\n" + req.downloadHandler.text);
                onDone?.Invoke(null);
            }
        }

        // ---------- (B) Image: 헤드라인이 포함된 뉴스 전체 이미지 (URL 응답) ----------
        IEnumerator GenerateImageRequest(string prompt, Action<Texture2D> onDone)
        {
            // 허용 사이즈(정사각): 256x256, 512x512, 1024x1024
            const string size = "1024x1024";

            string body =
$@"{{
  ""model"": ""gpt-image-1"",
  ""prompt"": {JsonEscape(prompt)},
  ""n"": 1,
  ""size"": ""{size}""
}}";

            var req = new UnityWebRequest("https://api.openai.com/v1/images/generations", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[News] Image API 실패: {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
                onDone?.Invoke(null);
                yield break;
            }

            // URL 파싱 후 텍스처 다운로드
            string url = null;
            try
            {
                var resp = JsonUtility.FromJson<ImageResp>(req.downloadHandler.text);
                url = resp.data != null && resp.data.Count > 0 ? resp.data[0].url : null;
            }
            catch (Exception e)
            {
                Debug.LogError("[News] 이미지 응답 파싱 실패: " + e.Message + "\n" + req.downloadHandler.text);
            }

            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[News] 이미지 URL이 비어 있습니다.");
                onDone?.Invoke(null);
                yield break;
            }

            using (var texReq = UnityWebRequestTexture.GetTexture(url))
            {
                yield return texReq.SendWebRequest();
                if (texReq.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[News] 이미지 다운로드 실패: " + texReq.responseCode + " / " + texReq.error);
                    onDone?.Invoke(null);
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(texReq);
                tex.filterMode = imageFilter;
                onDone?.Invoke(tex);
            }
        }

        IEnumerator GenerateNewsImage(NewsPayload p, Action<Texture2D> onDone)
        {
            string prompt =
                "Create a realistic Korean TV news screenshot. 16:9 composition inside a 1024x1024 canvas. " +
                "Anchor on right, photo box on left (offshore wind farm or solar panels). " +
                "Professional studio lighting, crisp UI. " +
                "Render a bold Korean lower-third banner; put the EXACT headline (Korean) as given. " +
                "Exact headline: \"" + Safe(p.headline) + "\". " +
                "Optional small subline: \"" + Safe(p.blurb) + "\". " +
                "Avoid real logos/watermarks; allow only a generic 'NEWS' label.";
            yield return GenerateImageRequest(prompt, onDone);
        }

        // ---------- UI 표시/페이드 ----------
        void ApplyUI(Texture2D tex)
        {
            if (!root.activeSelf) root.SetActive(true);
            canvasGroup.alpha = 0f;
            newsImage.texture = tex;
        }

        IEnumerator Fade(bool show, float sec)
        {
            float t = 0f;
            float start = canvasGroup.alpha;
            float end = show ? 1f : 0f;

            while (t < sec)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, end, t / sec);
                yield return null;
            }
            canvasGroup.alpha = end;
        }

        // ---------- util ----------
        static string JsonEscape(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        }
        static string Safe(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\"", "'");
    }
}