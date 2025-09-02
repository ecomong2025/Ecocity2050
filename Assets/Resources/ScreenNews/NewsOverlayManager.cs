// Assets/Resources/ScreenNews/NewsOverlayManager.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Ecocity.News
{
    // -------- 키 구성 (api_key.json) --------
    [Serializable] class KeyConfig
    {
        public string openai_api_key;      // OpenAI
        public string naver_client_id;     // Naver Search
        public string naver_client_secret; // Naver Search
    }

    // -------- OpenAI Chat 파싱 --------
    [Serializable] class ChatMessage { public string role; public string content; }
    [Serializable] class ChatChoice { public ChatMessage message; }
    [Serializable] class ChatResp { public List<ChatChoice> choices; }

    // -------- 게임에서 쓰는 Payload --------
    [Serializable] public class NewsPayload { public string headline; public string blurb; }

    // -------- OpenAI Image 파싱(둘 다 대비) --------
    [Serializable] class ImageDatum { public string url; public string b64_json; }
    [Serializable] class ImageResp { public List<ImageDatum> data; }

    // -------- 네이버 뉴스 응답 --------
    [Serializable] class NaverNewsResp { public string lastBuildDate; public int total; public int start; public int display; public List<NaverItem> items; }
    [Serializable] class NaverItem { public string title; public string originallink; public string link; public string description; public string pubDate; }

    public class NewsOverlayManager : MonoBehaviour
    {
        [Header("Overlay (screen space)")]
        public GameObject overlayRoot;         // NewsOverlayCanvas
        public CanvasGroup overlayGroup;       // NewsOverlayCanvas의 CanvasGroup
        public RawImage overlayImage;          // 화면 팝업에 보여줄 RawImage

        [Header("Billboard (world space)")]
        public GameObject billboardRoot;       // NewsBillboardCanvas
        public RawImage billboardImage;        // 전광판 앞 RawImage (World Space)

        [Header("Toggles")]
        public bool showOverlay = true;        // 큰 팝업 띄우기
        public bool showBillboard = true;      // 전광판에도 출력하기

        public float holdSeconds = 4f;
        public float fadeSeconds = 0.35f;

        string _openaiKey;
        string _naverId, _naverSecret;

        void Awake()
        {
            var ta = Resources.Load<TextAsset>("api_key");
            if (ta != null)
            {
                var k = JsonUtility.FromJson<KeyConfig>(ta.text);
                _openaiKey   = k.openai_api_key;
                _naverId     = k.naver_client_id;
                _naverSecret = k.naver_client_secret;
            }
            if (string.IsNullOrEmpty(_openaiKey)) Debug.LogError("[News] OpenAI 키 누락");
        }

        public void RequestAndShowNews()
        {
            if (string.IsNullOrEmpty(_openaiKey)) return;
            StopAllCoroutines();
            StartCoroutine(Flow());
        }

        // Flow 내부 표시 구간만 수정
        IEnumerator Flow()
        {
            // (1) 뉴스 텍스트 데이터 가져오기
            NewsPayload payload = null;
            bool gotNews = false;

            // 네이버 뉴스 우선, 실패 시 GPT 폴백
            yield return StartCoroutine(FetchFromNaver(p => { payload = p; gotNews = true; }));
            if (!gotNews || payload == null)
            {
                yield return StartCoroutine(FetchFromGPT(p => { payload = p; }));
                if (payload == null) yield break;
            }

            // (2) 이미지 생성
            Texture2D tex = null;
            yield return StartCoroutine(GenerateNewsImage(payload, t => tex = t));
            if (tex == null) yield break;

            // ---- Billboard: 월드 캔버스에 즉시 적용 (페이드 없음) ----
            if (showBillboard && billboardRoot != null && billboardImage != null)
            {
                if (!billboardRoot.activeSelf) billboardRoot.SetActive(true);
                billboardImage.texture = tex;  // 전광판에 계속 유지할 거면 아래 Destroy 금지
            }

            // ---- Overlay: 화면 팝업 페이드 인/아웃 ----
            if (showOverlay && overlayRoot != null && overlayGroup != null && overlayImage != null)
            {
                if (!overlayRoot.activeSelf) overlayRoot.SetActive(true);
                overlayGroup.alpha = 0f;
                overlayImage.texture = tex;

                yield return StartCoroutine(Fade(true, fadeSeconds, overlayGroup));
                yield return new WaitForSeconds(holdSeconds);
                yield return StartCoroutine(Fade(false, fadeSeconds, overlayGroup));
                overlayRoot.SetActive(false);
            }

            // 전광판에도 쓰고 있으면 텍스처 파괴하지 않음
            if (!(showBillboard && billboardImage != null))
                Destroy(tex);
        }



        // -------------------- (A) 네이버 뉴스 --------------------
        IEnumerator FetchFromNaver(Action<NewsPayload> done)
        {
            if (string.IsNullOrEmpty(_naverId) || string.IsNullOrEmpty(_naverSecret))
            { done(null); yield break; }

            // 검색어 수정: OR 조건 추가
            string q = UnityWebRequest.EscapeURL("탄소중립 OR 재생에너지 OR 온실가스 OR 지구온난화 OR 기후재난");
            string url = $"https://openapi.naver.com/v1/search/news.json?query={q}&display=1&sort=date";
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("X-Naver-Client-Id", _naverId);
            req.SetRequestHeader("X-Naver-Client-Secret", _naverSecret);

            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            { Debug.LogWarning("[News] Naver 실패: " + req.responseCode); done(null); yield break; }

            try
            {
                var resp = JsonUtility.FromJson<NaverNewsResp>(req.downloadHandler.text);
                if (resp?.items == null || resp.items.Count == 0) { done(null); yield break; }

                var it = resp.items[0];
                string title = CleanHtml(it.title);
                string desc  = CleanHtml(it.description);
                string src   = TryExtractHost(it.link);

                done(new NewsPayload {
                    headline = title,
                    blurb    = string.IsNullOrEmpty(desc) ? src : desc
                });
            }
            catch (Exception e)
            { Debug.LogWarning("[News] Naver 파싱 실패: " + e.Message); done(null); }
        }

        static string CleanHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, "<.*?>", ""); // <b> 등 제거
            return System.Net.WebUtility.HtmlDecode(s).Trim();
        }
        static string TryExtractHost(string link)
        {
            try { var u = new Uri(link); return u.Host.Replace("www.", ""); } catch { return ""; }
        }

        // -------------------- (B) GPT 텍스트 폴백 --------------------
        IEnumerator FetchFromGPT(Action<NewsPayload> done)
        {
            string body =
$@"{{
  ""model"": ""gpt-4o-mini"",
  ""temperature"": 0.7,
  ""messages"": [
    {{""role"": ""system"", ""content"": ""You are a Korean climate news editor. Output ONLY JSON with keys: headline, blurb."" }},
    {{""role"": ""user"", ""content"": ""탄소중립·재생에너지·온실가스 감축 주제로 한국 언론 톤의 헤드라인(26자 내외)과 보조문장(30자 내외)을 JSON으로 작성해줘."" }}
  ],
  ""response_format"": {{""type"": ""json_object""}}
}}";

            var req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _openaiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            { Debug.LogWarning("[News] GPT 텍스트 실패"); done(null); yield break; }

            try
            {
                var resp = JsonUtility.FromJson<ChatResp>(req.downloadHandler.text);
                var content = resp.choices[0].message.content;
                done(JsonUtility.FromJson<NewsPayload>(content));
            }
            catch { done(null); }
        }

        // -------------------- (C) 이미지 생성 --------------------
        IEnumerator GenerateNewsImage(NewsPayload p, Action<Texture2D> done)
        {
            // 정사각 1024 생성 / 캐릭터 앵커
            string prompt =
                "Design a Korean TV NEWS screenshot inside a 1024x1024 canvas. " +
                "Show a stylized 3D cartoon anchor on the right (no real human photo). " +
                "On the left side, generate a photo box with an image that visually represents the news topic and headline: \"" + Safe(p.headline) + "\". " +
                "Render a bold Korean lower-third banner; write the EXACT headline: \"" + Safe(p.headline) + "\". " +
                "Do NOT include any summary or blurb in the image. " +
                "Use clean, high-contrast typography. " +
                "Do NOT use real TV channel logos or watermarks; only a generic 'NEWS' label. " +
                "Professional studio lighting, crisp broadcast look.";

            string body =
$@"{{
  ""model"": ""gpt-image-1"",
  ""prompt"": {JsonEscape(prompt)},
  ""n"": 1,
  ""size"": ""1024x1024""
}}";

            var req = new UnityWebRequest("https://api.openai.com/v1/images/generations", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _openaiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            { Debug.LogWarning("[News] Image API 실패: " + req.responseCode); done(null); yield break; }

            string raw = req.downloadHandler.text;
            string url = null, b64 = null;
            try
            {
                var r = JsonUtility.FromJson<ImageResp>(raw);
                if (r?.data != null && r.data.Count > 0) { url = r.data[0].url; b64 = r.data[0].b64_json; }
            }
            catch { /* ignore */ }

            if (!string.IsNullOrEmpty(url))
            {
                using (var texReq = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return texReq.SendWebRequest();
                    if (texReq.result != UnityWebRequest.Result.Success) { done(null); yield break; }
                    var tex = DownloadHandlerTexture.GetContent(texReq);
                    done(tex); yield break;
                }
            }
            if (!string.IsNullOrEmpty(b64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(b64);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(bytes); done(tex); yield break;
                }
                catch { done(null); yield break; }
            }

            done(null);
        }

        // -------------------- 공통 --------------------
        // CanvasGroup을 파라미터로 받도록 변경
        IEnumerator Fade(bool show, float sec, CanvasGroup group)
        {
            float t = 0f, from = group.alpha, to = show ? 1f : 0f;
            while (t < sec) { t += Time.deltaTime; group.alpha = Mathf.Lerp(from, to, t / sec); yield return null; }
            group.alpha = to;
        }

        static string JsonEscape(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        static string Safe(string s)       => string.IsNullOrEmpty(s) ? "" : s.Replace("\"", "'");
    }
}