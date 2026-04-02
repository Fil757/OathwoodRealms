using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// UltraSimpleCardHover
/// ---------------------------
/// - OnPointerEnter: Karte leicht nach oben + skaliert.
/// - OnPointerExit: Zurück in Ausgangsposition/-scale.
/// - Keine Drag-Unterstützung, kein Layout-/Sorting-Gefummel.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UltraSimpleCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referenzen")]
    [Tooltip("UI-Element, das bewegt wird. Wenn leer, wird das eigene RectTransform verwendet.")]
    public RectTransform visual;

    [Header("Hover Effekt")]
    [Tooltip("Verschiebung in Y-Richtung (Canvas-Einheiten).")]
    public float liftY = 40f;

    [Tooltip("Skalierung beim Hover (1 = keine Änderung).")]
    public float hoverScale = 1.05f;

    [Tooltip("Animationsdauer in Sekunden.")]
    public float duration = 0.12f;

    [Header("Optional: Jitter reduzieren")]
    [Tooltip("Wenn an, werden Positionen auf Canvas-Pixel gerundet (hilft gegen leichtes Jittern).")]
    public bool pixelSnap = true;

    // Cache
    private RectTransform rt;
    private Canvas rootCanvas;
    private Vector2 restPos;
    private Vector3 restScale;

    private Coroutine animCo;
    private bool isHovered;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (!visual) visual = rt;

        rootCanvas = GetComponentInParent<Canvas>();

        restPos = visual.anchoredPosition;
        restScale = visual.localScale;
    }

    private void OnEnable()
    {
        // Falls Layout / Position sich geändert hat, neu einlesen
        restPos = visual.anchoredPosition;
        restScale = visual.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isHovered) return;
        isHovered = true;

        AudioManager.Instance?.PlaySFX2D("Hover_Card");

        Vector2 targetPos = restPos + new Vector2(0f, liftY);
        Vector3 targetScale = restScale * hoverScale;

        StartAnim(targetPos, targetScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovered) return;
        isHovered = false;

        StartAnim(restPos, restScale);
    }

    private void StartAnim(Vector2 targetPos, Vector3 targetScale)
    {
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(AnimRoutine(targetPos, targetScale));
    }

    private IEnumerator AnimRoutine(Vector2 targetPos, Vector3 targetScale)
    {
        Vector2 startPos = visual.anchoredPosition;
        Vector3 startScale = visual.localScale;

        float time = 0f;
        float sf = rootCanvas ? rootCanvas.scaleFactor : 1f;
        float sfSafe = Mathf.Max(sf, 0.0001f);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float e = 1f - (1f - t) * (1f - t); // easeOutQuad

            Vector2 pos = Vector2.LerpUnclamped(startPos, targetPos, e);

            if (pixelSnap)
            {
                Vector2 px = pos * sf;
                px.x = Mathf.Round(px.x);
                px.y = Mathf.Round(px.y);
                pos = px / sfSafe;
            }

            visual.anchoredPosition = pos;
            visual.localScale = Vector3.LerpUnclamped(startScale, targetScale, e);

            yield return null;
        }

        if (pixelSnap)
        {
            Vector2 px = targetPos * sf;
            px.x = Mathf.Round(px.x);
            px.y = Mathf.Round(px.y);
            visual.anchoredPosition = px / sfSafe;
        }
        else
        {
            visual.anchoredPosition = targetPos;
        }

        visual.localScale = targetScale;
        animCo = null;
    }
}
