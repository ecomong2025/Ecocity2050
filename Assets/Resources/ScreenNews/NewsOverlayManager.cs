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
        public TMPro.TMP_Text overlaySummaryText; // 뉴스 요약 텍스트 추가

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

        public Texture2D lastBillboardTexture; // 마지막 생성된 이미지 저장
        public bool isImageReady = false;      // 이미지 준비 상태

        // 주제 키워드 리스트와 인덱스 추가
        readonly string[] newsKeywords = { "탄소중립", "재생에너지", "온실가스", "지구온난화", "기후재난" };
        int keywordIndex = 0;

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

        void Start()
        {
            StartCoroutine(PeriodicNewsRoutine());
        }

        // 5분마다 뉴스 이미지 생성
        IEnumerator PeriodicNewsRoutine()
        {
            while (true)
            {
                yield return StartCoroutine(Flow()); // 이미지 생성 및 billboard 갱신
                yield return new WaitForSeconds(300f); // 5분 대기
            }
        }

        public void RequestAndShowNews()
        {
            if (string.IsNullOrEmpty(_openaiKey)) return;
            StopAllCoroutines();
            StartCoroutine(Flow());
        }

        // Flow: 이미지 생성 및 billboard 갱신
        NewsPayload lastNewsPayload; // 마지막 뉴스 데이터 저장

        IEnumerator Flow()
        {
            // 이전 이미지 백업
            Texture2D prevTexture = lastBillboardTexture;

            isImageReady = false;

            // (1) 뉴스 텍스트 데이터 가져오기
            NewsPayload payload = null;
            bool gotNews = false;

            yield return StartCoroutine(FetchFromNaver(p => { payload = p; gotNews = true; }));
            if (!gotNews || payload == null)
            {
                yield return StartCoroutine(FetchFromGPT(p => { payload = p; }));
                if (payload == null) yield break;
            }

            lastNewsPayload = payload; // 마지막 뉴스 데이터 저장

            // (2) 이미지 생성
            Texture2D tex = null;
            yield return StartCoroutine(GenerateNewsImage(payload, t => tex = t));
            if (tex == null) yield break;

            // ---- Billboard: 월드 캔버스에 즉시 적용 ----
            if (showBillboard && billboardRoot != null && billboardImage != null)
            {
                if (!billboardRoot.activeSelf) billboardRoot.SetActive(true);
                billboardImage.texture = tex;
                lastBillboardTexture = tex;
                isImageReady = true;
            }

            // ---- Overlay: 자동으로 띄우지 않음 (클릭 시만 표시) ----

            // 전광판에도 쓰고 있으면 텍스처 파괴하지 않음
            if (!(showBillboard && billboardImage != null))
                Destroy(tex);

            // 이전 이미지가 있으면 prevTexture를 해제
            if (prevTexture != null && prevTexture != tex)
                Destroy(prevTexture);
        }

        // 전광판 클릭 시 호출: 기존 이미지만 overlay로 보여줌
        public void ShowOverlayWithBillboardImage()
        {
            Texture2D showTexture = lastBillboardTexture;
            if (showTexture == null) return;

            overlayImage.texture = showTexture;
            // 뉴스 요약(블럽) 1줄로 표시
            if (overlaySummaryText != null && lastNewsPayload != null)
                overlaySummaryText.text = lastNewsPayload.blurb;
            StartCoroutine(ShowOverlayRoutine());
        }

        IEnumerator ShowOverlayRoutine()
        {
            if (showOverlay && overlayRoot != null && overlayGroup != null && overlayImage != null)
            {
                if (!overlayRoot.activeSelf) overlayRoot.SetActive(true);
                overlayGroup.alpha = 0f;
                overlayImage.texture = lastBillboardTexture;

                yield return StartCoroutine(Fade(true, fadeSeconds, overlayGroup));
                yield return new WaitForSeconds(holdSeconds);
                yield return StartCoroutine(Fade(false, fadeSeconds, overlayGroup));
                overlayRoot.SetActive(false);
            }
        }

        // -------------------- (A) 네이버 뉴스 --------------------
        IEnumerator FetchFromNaver(Action<NewsPayload> done)
        {
            if (string.IsNullOrEmpty(_naverId) || string.IsNullOrEmpty(_naverSecret))
            { done(null); yield break; }

            // 이번에 사용할 키워드 선택 및 인덱스 순환
            string keyword = newsKeywords[keywordIndex];
            keywordIndex = (keywordIndex + 1) % newsKeywords.Length;

            string q = UnityWebRequest.EscapeURL(keyword);
            string url = $"https://openapi.naver.com/v1/search/news.json?query={q}&display=20&sort=date";
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

                // 일주일 이내 기사만 필터링
                var weekAgo = DateTime.Now.AddDays(-7);
                var validItems = new List<NaverItem>();
                foreach (var it in resp.items)
                {
                    DateTime pub;
                    if (DateTime.TryParse(it.pubDate, out pub) && pub >= weekAgo)
                        validItems.Add(it);
                }
                if (validItems.Count == 0) { done(null); yield break; }

                // 기사 리스트 섞기
                for (int i = validItems.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    var temp = validItems[i];
                    validItems[i] = validItems[j];
                    validItems[j] = temp;
                }
                var selected = validItems[0];

                string title = CleanHtml(selected.title);
                string desc  = CleanHtml(selected.description);
                string src   = TryExtractHost(selected.link);

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
  ""max_tokens"": 256,
  ""messages"": [
    {{""role"": ""system"", ""content"": ""You are a Korean climate news editor. Output ONLY JSON with keys: headline, blurb."" }},
    {{""role"": ""user"", ""content"": ""탄소중립, 재생에너지, 온실가스, 지구온난화, 기후재난 등 환경 주제로 한국 언론 톤의 헤드라인(26자 내외)과, 기사 전체 내용을 중학생도 이해할 수 있도록 한 문장으로 자연스럽게 끝맺으며 요약해줘. 요약에는 이 기사가 왜 환경과 관련있는지 꼭 드러나게 써줘. 예시: 국내 상반기 미세먼지가 작년보다 15% 줄었습니다. 전기차 보급과 석탄발전 축소 덕분이지만, 폭염으로 대기질 악화 우려가 남아 있습니다."" }}
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