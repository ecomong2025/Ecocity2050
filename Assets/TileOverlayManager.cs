using System.Collections.Generic;
using UnityEngine;

public class TileOverlayManager : MonoBehaviour
{
    [Header("타일 부모(최대 5개)")]
    public Transform tilesRoot1, tilesRoot2, tilesRoot3, tilesRoot4, tilesRoot5;

    [Header("테두리 머티리얼 (필수)")]
    // URP/Lit Transparent (Emission HDR 추천) 또는 URP/Unlit Transparent
    public Material lineMat;

    [Header("표시 옵션")]
    public float yOffset = 0.02f;                 // z-fighting 방지
    [Range(0.001f, 0.002f)] public float borderPercent = 0.001f; // 타일 변의 % 두께
    public bool drawAllFourEdges = false;          // 내부 겹침 줄이려면 false(상/우만)

    [Header("깜빡임(라인만)")]
    public bool blink = true;
    public float blinkSpeed = 2f;                 // 초당 숨쉬기 횟수
    [Range(0, 1)] public float alphaLow = 0.25f, alphaHigh = 0.55f;
    public float intensityLow = 1.0f, intensityHigh = 2.0f; // Emission 강도 배율

    class Overlay
    {
        public GameObject root;
        public LineRenderer line;
        public Color baseColor;        // 머티리얼 원본 BaseColor/Color
        public Color baseEmission;     // 머티리얼 원본 Emission(없으면 baseColor)
        public readonly MaterialPropertyBlock mpb = new();
    }

    readonly Dictionary<Transform, Overlay> overlays = new();

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        BuildAll();
        if (blink) StartCoroutine(BlinkLoop());
    }

    IEnumerable<Transform> Roots()
    {
        if (tilesRoot1) yield return tilesRoot1;
        if (tilesRoot2) yield return tilesRoot2;
        if (tilesRoot3) yield return tilesRoot3;
        if (tilesRoot4) yield return tilesRoot4;
        if (tilesRoot5) yield return tilesRoot5;
    }

    [ContextMenu("Rebuild Overlays (Hard)")]
    public void BuildAll()
    {
        // 0) 이전 버전에서 만든 오버레이/Fill 남아있다면 전부 정리
        foreach (var p in Roots())
        {
            if (!p) continue;
            foreach (Transform t in p)
            {
                foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                {
                    if (c.name.StartsWith("Overlay_") || c.name == "Fill")
                        DestroyImmediate(c.gameObject);
                }
            }
        }
        overlays.Clear();

        // 1) 새로 생성 (라인만)
        foreach (var parent in Roots())
        {
            if (!parent) continue;

            for (int i = 0; i < parent.childCount; i++)
            {
                var tile = parent.GetChild(i);

                // 타일 Bounds
                var r = tile.GetComponentInChildren<MeshRenderer>(true);
                var b = r ? r.bounds : new Bounds(tile.position, new Vector3(1, 0.1f, 1));

                // 루트
                var root = new GameObject($"Overlay_{tile.name}");
                root.transform.SetParent(tile, true);
                root.transform.position = new Vector3(b.center.x, b.center.y + yOffset, b.center.z);

                // ── 라인(LineRenderer) ───────────────────────────────
                var lr = new GameObject("Border").AddComponent<LineRenderer>();
                lr.transform.SetParent(root.transform, false);
                lr.material = lineMat;
                lr.useWorldSpace = false;
                // ⬇⬇⬇ 여기 네 줄 추가!
                lr.textureMode = LineTextureMode.Stretch;               // 텍스처 반복 금지(점선 방지)
                lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f); // 두께 일정
                lr.numCornerVertices = 0;                               // 모서리/캡 보정(선택)
                lr.numCapVertices = 0;
                // 라인 두께(타일 크기 비율)
                float minSide = Mathf.Min(b.size.x, b.size.z);
                lr.widthMultiplier = Mathf.Clamp(minSide * borderPercent, 0.002f, 0.02f);

                // 모양
                float hx = b.size.x * 0.5f, hz = b.size.z * 0.5f;
                if (drawAllFourEdges)
                {
                    lr.loop = true;
                    lr.positionCount = 4;
                    lr.SetPositions(new[]{
                        new Vector3(-hx,0,-hz), new Vector3(hx,0,-hz),
                        new Vector3(hx,0,hz),   new Vector3(-hx,0,hz)
                    });
                }
                else
                {
                    // 내부 겹침 방지: 상/우만 그림
                    lr.loop = false;
                    lr.positionCount = 4;
                    lr.SetPositions(new[]{
                        new Vector3(-hx,0, hz), new Vector3(hx,0, hz),
                        new Vector3( hx,0,-hz), new Vector3( hx,0, hz)
                    });
                }

                // 라인 틴트/그라디언트 무효화 (머티리얼 색만 쓰기)
                lr.startColor = Color.white;
                lr.endColor = Color.white;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
                lr.colorGradient = grad;

                // 등록 + 원본 머티리얼 값 저장
                var ov = new Overlay
                {
                    root = root,
                    line = lr,
                    baseColor = ReadMatColor(lr.sharedMaterial),
                    baseEmission = ReadEmissionColor(lr.sharedMaterial)
                };
                overlays[tile] = ov;

                // 초기값 적용: 라인만 보이게(면은 생성 안 함)
                ApplyLineBlink(ov, alphaHigh, intensityLow);
            }
        }
    }

    System.Collections.IEnumerator BlinkLoop()
    {
        while (true)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * blinkSpeed);
            float a = Mathf.Lerp(alphaLow, alphaHigh, k);       // 투명도
            float I = Mathf.Lerp(intensityLow, intensityHigh, k); // Emission 강도

            foreach (var ov in overlays.Values)
                if (ov?.line) ApplyLineBlink(ov, a, I);

            yield return null;
        }
    }

    // ── 라인만 깜빡(알파+에미션 강도), 색상(Hue)은 머티리얼 그대로 ────────
    void ApplyLineBlink(Overlay ov, float alpha, float intensity)
    {
        var c = ov.baseColor; c.a = alpha;  // 알파만 변경

        ov.mpb.Clear();
        ov.mpb.SetColor(BaseColorID, c);
        ov.mpb.SetColor(ColorID, c);

        var e = ov.baseEmission;              // Emission 색 유지, 강도만 변경
        ov.mpb.SetColor(EmissionColor, e * intensity);

        ov.line.SetPropertyBlock(ov.mpb);
    }

    // ── helpers ──────────────────────────────────────────────────
    static Color ReadMatColor(Material m)
    {
        if (!m) return Color.white;
        if (m.HasProperty(BaseColorID)) return m.GetColor(BaseColorID);
        if (m.HasProperty(ColorID)) return m.GetColor(ColorID);
        return Color.white;
    }

    static Color ReadEmissionColor(Material m)
    {
        if (!m) return Color.black;
        if (m.HasProperty(EmissionColor)) return m.GetColor(EmissionColor);
        return ReadMatColor(m); // Emission 없으면 BaseColor 사용
    }
}
