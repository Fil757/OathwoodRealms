using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ClickableFigure : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referenzen")]
    public FigureButtons figureButtons;
    public Image hoverShadow;

    [Header("Weitere Fade-Images beim Hover (sofortiger Fade)")]
    [Tooltip("Hier können allgemeine Hover-Images eingetragen werden (inkl. SignFrames, wenn sie mitfaden sollen).")]
    public Image[] extraHoverFadeImages;

    [Header("Sign Frames (Suit-Rahmen, Active wird von Display_Figure gesteuert)")]
    [Tooltip("Die drei Suit-Rahmen (Heart/Spade/Club), die von Display_Figure aktiv/inaktiv geschaltet werden. " +
             "ClickableFigure ändert hier nur die Alpha, niemals SetActive.")]
    public Image[] signFrames;

    [Header("Weitere Fade-Texte beim Hover (TextMeshPro, sofortiger Fade)")]
    public TextMeshProUGUI[] extraHoverFadeTexts;

    [Header("Hover-Fade")]
    [Range(0f, 1f)] public float targetAlpha = 0.4f;
    public float fadeDuration = 0.25f;

    [Header("Click-Feedback (Alpha)")]
    [Range(1f, 3f)] public float clickDarkenFactor = 1.5f;
    public float clickFlashDuration = 0.12f;

    [Header("Click-Feedback (Scale)")]
    public Vector2 clickScaleMultiplier = new Vector2(1.05f, 1.08f);

    [Header("Click-Feedback (Zusätzlicher Pump)")]
    public GameObject extraPumpObject;
    public Vector2 extraPumpMultiplier = new Vector2(1.08f, 1.08f);

    [Header("Unhover: Verzögertes Ausfaden (nur Images, z.B. Info-Button)")]
    [Tooltip("Diese Images bleiben nach Unhover noch kurz sichtbar und faden erst dann aus.")]
    public Image[] delayedFadeImages;
    public float delayedFadeDelay = 1f;
    public float delayedFadeDuration = 0.25f;

    private Color _baseColor;
    private Coroutine _fadeRoutine;
    private Coroutine _clickRoutine;
    private Coroutine _delayedFadeRoutine;

    private RectTransform _shadowRect;
    private Vector3 _baseShadowScale = Vector3.one;

    private Transform _extraPumpTransform;
    private Vector3 _baseExtraScale = Vector3.one;

    private void Awake()
    {
        if (hoverShadow != null)
        {
            _shadowRect = hoverShadow.rectTransform;
            _baseColor = hoverShadow.color;
            _baseColor.a = 1f;

            if (_shadowRect != null)
                _baseShadowScale = _shadowRect.localScale;

            SetShadowAlpha(0f);
            SetShadowScale(_baseShadowScale);
            hoverShadow.gameObject.SetActive(false);
        }

        if (extraPumpObject != null)
        {
            _extraPumpTransform = extraPumpObject.transform;
            _baseExtraScale = _extraPumpTransform.localScale;
        }

        // Extra-Hover-Images vorbereiten (Alpha 0, normale Hover-Images deaktiviert,
        // SignFrames bleiben aktiv, weil Display_Figure deren Active-Status steuert)
        if (extraHoverFadeImages != null)
        {
            foreach (var img in extraHoverFadeImages)
            {
                if (img == null) continue;

                Color c = img.color;
                c.a = 0f;
                img.color = c;

                if (!IsSignFrame(img))
                {
                    img.gameObject.SetActive(false);
                }
                // SignFrames: nicht deaktivieren, nur Alpha = 0 setzen.
            }
        }

        if (extraHoverFadeTexts != null)
        {
            foreach (var txt in extraHoverFadeTexts)
            {
                if (txt == null) continue;
                Color c = txt.color;
                c.a = 0f;
                txt.color = c;
                txt.gameObject.SetActive(false);
            }
        }
    }

    // ---------------- Klick ----------------
    public void OnPointerClick(PointerEventData eventData)
    {
        // Right-Click: Info-Board
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Transform grandParent = transform.parent != null && transform.parent.parent != null
                ? transform.parent.parent
                : null;

            if (grandParent == null) return;

            var handler = InformationBoardHandler.current;
            if (handler == null)
            {
                // Fallback: suche irgendeinen Handler in der Szene (besser: gezielt am GUI-Canvas)
                handler = GameObject.FindObjectOfType<InformationBoardHandler>(true);
                if (handler != null)
                    InformationBoardHandler.current = handler; // optional "self-heal"
            }

            if (handler == null)
            {
                Debug.LogWarning("[ClickableFigure] No InformationBoardHandler.current found (right-click ignored).");
                return;
            }

            handler.Spawn_FigureInfoComplete(grandParent.gameObject);
        }

        // Left-Click: Buttons / TurnManager
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Transform grandParent = transform.parent != null && transform.parent.parent != null
                ? transform.parent.parent
                : null;

            AudioManager.Instance?.PlaySFX2D("Click_Figure");

            if (figureButtons != null && grandParent != null &&
                grandParent.gameObject.transform.parent != null &&
                grandParent.gameObject.transform.parent.name == "P1_Figures")
            {
                figureButtons.ActivateButtons();
                TurnManager.current.current_figure_P1 = grandParent.gameObject;
            }
        }

        PlayClickFeedback();
    }

    // ---------------- Hover ----------------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverShadow != null)
        {
            hoverShadow.gameObject.SetActive(true);
            StartFade(targetAlpha);
        }

        if (extraHoverFadeImages != null)
        {
            foreach (var img in extraHoverFadeImages)
            {
                if (img == null) continue;
                if (IsDelayedImage(img)) continue; // Delayed-Images hier ignorieren

                // Normale Hover-Images aktivieren.
                // SignFrames: Active-State wird von Display_Figure gesteuert -> nicht anfassen.
                if (!IsSignFrame(img))
                {
                    img.gameObject.SetActive(true);
                }
            }
        }

        if (extraHoverFadeTexts != null)
        {
            foreach (var txt in extraHoverFadeTexts)
            {
                if (txt == null) continue;
                txt.gameObject.SetActive(true);
            }
        }

        // Verzögert auszublendende Images (z.B. Info-Button):
        if (_delayedFadeRoutine != null)
        {
            StopCoroutine(_delayedFadeRoutine);
            _delayedFadeRoutine = null;
        }

        if (delayedFadeImages != null)
        {
            foreach (var img in delayedFadeImages)
            {
                if (img == null) continue;
                img.gameObject.SetActive(true);
                Color c = img.color;
                c.a = targetAlpha; // direkt auf Ziel-Alpha -> kein Blinzeln
                img.color = c;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverShadow != null)
            StartFade(0f);

        if (delayedFadeImages != null && delayedFadeImages.Length > 0 && delayedFadeDuration > 0f)
        {
            if (_delayedFadeRoutine != null)
                StopCoroutine(_delayedFadeRoutine);

            _delayedFadeRoutine = StartCoroutine(DelayedFadeOutImagesRoutine());
        }
    }

    // ---------------- Fade-Logik (Shadow + Extra-Gruppen) ----------------
    private void StartFade(float target)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        if (_clickRoutine != null)
        {
            StopCoroutine(_clickRoutine);
            _clickRoutine = null;
        }

        _fadeRoutine = StartCoroutine(FadeShadowAndExtras(target));
    }

    private System.Collections.IEnumerator FadeShadowAndExtras(float target)
    {
        float startAlpha = hoverShadow != null ? hoverShadow.color.a : 0f;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / fadeDuration);

            if (hoverShadow != null)
                SetShadowAlpha(Mathf.Lerp(startAlpha, target, t));

            if (extraHoverFadeImages != null)
            {
                foreach (var img in extraHoverFadeImages)
                {
                    if (img == null) continue;
                    if (IsDelayedImage(img)) continue; // Delayed-Images NICHT hier anfassen

                    Color c = img.color;
                    c.a = Mathf.Lerp(startAlpha, target, t);
                    img.color = c;
                }
            }

            if (extraHoverFadeTexts != null)
            {
                foreach (var txt in extraHoverFadeTexts)
                {
                    if (txt == null) continue;
                    Color c = txt.color;
                    c.a = Mathf.Lerp(startAlpha, target, t);
                    txt.color = c;
                }
            }

            yield return null;
        }

        if (hoverShadow != null)
        {
            SetShadowAlpha(target);
            if (Mathf.Approximately(target, 0f))
                hoverShadow.gameObject.SetActive(false);
        }

        if (extraHoverFadeImages != null)
        {
            foreach (var img in extraHoverFadeImages)
            {
                if (img == null) continue;
                if (IsDelayedImage(img)) continue; // Delayed-Images bleiben aktiv

                Color c = img.color;
                c.a = target;
                img.color = c;

                // Nur normale Hover-Bilder deaktivieren, nicht die SignFrames
                if (Mathf.Approximately(target, 0f) && !IsSignFrame(img))
                {
                    img.gameObject.SetActive(false);
                }
            }
        }

        if (extraHoverFadeTexts != null)
        {
            foreach (var txt in extraHoverFadeTexts)
            {
                if (txt == null) continue;
                Color c = txt.color;
                c.a = target;
                txt.color = c;
                if (Mathf.Approximately(target, 0f))
                    txt.gameObject.SetActive(false);
            }
        }

        _fadeRoutine = null;
    }

    private void SetShadowAlpha(float alpha)
    {
        if (hoverShadow == null) return;
        Color c = hoverShadow.color;
        c.r = _baseColor.r;
        c.g = _baseColor.g;
        c.b = _baseColor.b;
        c.a = alpha;
        hoverShadow.color = c;
    }

    private void SetShadowScale(Vector3 scale)
    {
        if (_shadowRect != null)
            _shadowRect.localScale = scale;
    }

    private void SetExtraScale(Vector3 scale)
    {
        if (_extraPumpTransform != null)
            _extraPumpTransform.localScale = scale;
    }

    // ---------------- Click-Flash ----------------
    private void PlayClickFeedback()
    {
        if (hoverShadow == null || !hoverShadow.gameObject.activeSelf)
            return;

        if (_clickRoutine != null)
            StopCoroutine(_clickRoutine);

        _clickRoutine = StartCoroutine(ClickFlashRoutine());
    }

    private System.Collections.IEnumerator ClickFlashRoutine()
    {
        float startAlpha = hoverShadow.color.a;
        float peakAlpha = Mathf.Clamp01(startAlpha * clickDarkenFactor);

        Vector3 peakShadowScale = new Vector3(
            _baseShadowScale.x * clickScaleMultiplier.x,
            _baseShadowScale.y * clickScaleMultiplier.y,
            _baseShadowScale.z
        );

        Vector3 peakExtraScale = extraPumpObject == null
            ? _baseExtraScale
            : new Vector3(
                _baseExtraScale.x * extraPumpMultiplier.x,
                _baseExtraScale.y * extraPumpMultiplier.y,
                _baseExtraScale.z
            );

        float half = Mathf.Max(0.01f, clickFlashDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);

            SetShadowAlpha(Mathf.Lerp(startAlpha, peakAlpha, k));
            SetShadowScale(Vector3.Lerp(_baseShadowScale, peakShadowScale, k));
            if (_extraPumpTransform != null)
                SetExtraScale(Vector3.Lerp(_baseExtraScale, peakExtraScale, k));

            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);

            SetShadowAlpha(Mathf.Lerp(peakAlpha, startAlpha, k));
            SetShadowScale(Vector3.Lerp(peakShadowScale, _baseShadowScale, k));
            if (_extraPumpTransform != null)
                SetExtraScale(Vector3.Lerp(peakExtraScale, _baseExtraScale, k));

            yield return null;
        }

        _clickRoutine = null;
    }

    // ---------------- Verzögertes Ausfaden (nur Images) ----------------
    private System.Collections.IEnumerator DelayedFadeOutImagesRoutine()
    {
        if (delayedFadeDelay > 0f)
            yield return new WaitForSeconds(delayedFadeDelay);

        if (delayedFadeImages == null || delayedFadeImages.Length == 0)
        {
            _delayedFadeRoutine = null;
            yield break;
        }

        float[] startAlphas = new float[delayedFadeImages.Length];
        for (int i = 0; i < delayedFadeImages.Length; i++)
        {
            var img = delayedFadeImages[i];
            if (img == null)
            {
                startAlphas[i] = 0f;
                continue;
            }
            startAlphas[i] = img.color.a;
        }

        float time = 0f;
        while (time < delayedFadeDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / delayedFadeDuration);

            for (int i = 0; i < delayedFadeImages.Length; i++)
            {
                var img = delayedFadeImages[i];
                if (img == null) continue;

                Color c = img.color;
                c.a = Mathf.Lerp(startAlphas[i], 0f, t);
                img.color = c;
            }

            yield return null;
        }

        foreach (var img in delayedFadeImages)
        {
            if (img == null) continue;
            Color c = img.color;
            c.a = 0f;
            img.color = c;
            img.gameObject.SetActive(false);
        }

        _delayedFadeRoutine = null;
    }

    // ---------------- Helper ----------------
    private bool IsDelayedImage(Image img)
    {
        if (img == null || delayedFadeImages == null) return false;
        for (int i = 0; i < delayedFadeImages.Length; i++)
        {
            if (delayedFadeImages[i] == img)
                return true;
        }
        return false;
    }

    private bool IsSignFrame(Image img)
    {
        if (img == null || signFrames == null) return false;
        for (int i = 0; i < signFrames.Length; i++)
        {
            if (signFrames[i] == img)
                return true;
        }
        return false;
    }
}
