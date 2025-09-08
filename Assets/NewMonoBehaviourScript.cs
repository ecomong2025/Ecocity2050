using UnityEngine;

public enum OverlayState { Normal, Available, Blocked, Selected }

public class OverlayController : MonoBehaviour
{
    [Header("Refs")]
    public MeshRenderer fill;      // FillÀÇ MeshRenderer
    public Renderer border;        // LineRendererµµ Renderer Ãë±Þ
    public ParticleSystem beam;    // À§·Î ÆÛÁö´Â ºû

    [Header("Colors (HDR ±ÇÀå)")]
    public Color normal = new(0.2f, 0.6f, 1f, 0.30f); // ÆÄ¶û
    public Color available = new(1f, 0.9f, 0.2f, 0.35f); // ³ë¶û
    public Color blocked = new(1f, 0.2f, 0.2f, 0.30f); // »¡°­
    public Color selected = new(1f, 0.5f, 0.1f, 0.40f); // ÁÖÈ²

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock mpbFill, mpbBorder;
    float pulseT; public float pulseSpeed = 2f; public bool pulse = false;

    void Awake()
    {
        mpbFill = new MaterialPropertyBlock();
        mpbBorder = new MaterialPropertyBlock();
        SetState(OverlayState.Available); // ±âº»
    }

    void LateUpdate()
    {
        if (!pulse) return;
        pulseT += Time.deltaTime * pulseSpeed;
        float k = 0.5f + 0.5f * Mathf.Sin(pulseT); // 0~1
        ApplyColor(MixColor(current, 1.5f + 2.5f * k)); // ¹à±â »ìÂ¦ ¼û½¬±â
    }

    Color current;
    public void SetState(OverlayState s)
    {
        current = s switch
        {
            OverlayState.Available => available,
            OverlayState.Blocked => blocked,
            OverlayState.Selected => selected,
            _ => normal
        };
        ApplyColor(current);
        var main = beam.main; main.startColor = new ParticleSystem.MinMaxGradient(current);
        beam.gameObject.SetActive(s != OverlayState.Blocked);
    }

    public void Flash(float peak = 5f, float time = 0.25f)
    {
        StopAllCoroutines();
        StartCoroutine(CoFlash(peak, time));
    }

    System.Collections.IEnumerator CoFlash(float peak, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.PI * (t / time)); // 0¡æ1¡æ0
            ApplyColor(MixColor(current, Mathf.Lerp(1f, peak, k)));
            yield return null;
        }
        ApplyColor(current);
    }

    void ApplyColor(Color c)
    {
        // Fill: UnlitÀº _BaseColor, LitÀº _BaseColor¿Í _EmissionColor ¸ðµÎ ¸Ôµµ·Ï
        if (fill)
        {
            fill.GetPropertyBlock(mpbFill);
            mpbFill.SetColor(BaseColorID, c);
            mpbFill.SetColor(EmissionID, c); // LitÀÏ ¶§ Bloom ´õ ¹ÞÀ½
            fill.SetPropertyBlock(mpbFill);
        }

        if (border)
        {
            border.GetPropertyBlock(mpbBorder);
            mpbBorder.SetColor(BaseColorID, c);
            mpbBorder.SetColor(EmissionID, c);
            border.SetPropertyBlock(mpbBorder);
        }
    }

    // HDR °­µµ °ö
    Color MixColor(Color c, float intensity) => new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);
}
