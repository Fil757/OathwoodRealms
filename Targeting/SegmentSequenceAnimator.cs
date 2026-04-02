using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SegmentSequenceAnimator : MonoBehaviour
{
    [Header("Order = Abspielreihenfolge")]
    public RectTransform[] segments;                    // 0: Heart, 1: Spade, 2: Club (oder beliebig sortiert)

    [Header("Intro (Fly-in)")]
    public float flyInOffsetY = 220f;                   // Start: so viel über der Zielposition
    public float flyInTime = 0.35f;
    public float flyInStagger = 0.06f;                  // Staffelung pro Segment
    public LeanTweenType flyInEase = LeanTweenType.easeOutCubic;

    [Header("Scale-Pulse")]
    [Range(1f, 2f)] public float scaleUpFactor = 1.15f;
    public float scaleUpTime = 0.18f;
    public float holdTime = 0.08f;
    public float scaleDownTime = 0.18f;

    [Header("Blink (optional)")]
    public bool doBlink = true;
    [Range(0f, 1f)] public float minAlpha = 0.35f;
    public float fadeTime = 0.12f;

    [Header("Loop / Sequenz")]
    public float gapBetweenSegments = 0.06f;
    public bool loop = true;
    public float loopPause = 0.25f;

    [Header("Auswahl / Ziel")]
    public float selectAfterSeconds = 2.0f;             // wann wird gewählt (ab Start dieses Scripts)
    public int forcedSelectIndex = -1;                  // -1 = random, sonst 0..n
    public RectTransform selectionTarget;               // wohin das gewählte Segment fliegt (UI-RectTransform)
    public float chosenFlyTime = 0.45f;
    public LeanTweenType chosenFlyEase = LeanTweenType.easeInCubic;
    public float othersFadeScaleTime = 0.20f;

    [Header("Zwischenfokus (Mitte)")]
    public RectTransform midPoint;                         // optional: Anker in der Mitte (gleiches Canvas/Parent!)
    public float toMidTime = 0.25f;
    public LeanTweenType toMidEase = LeanTweenType.easeInOutCubic;
    [Range(1f, 2f)] public float midScaleFactor = 1.35f;  // Größe in der Mitte
    public float midHoldTime = 0.10f;                     // kurze Pause in der Mitte

    // intern
    Vector3[] originalScale;
    Vector2[] originalAnchoredPos;
    CanvasGroup[] canvasGroups;
    Coroutine loopRunner;
    bool selectionTriggered = false;

    void Awake()
    {
        if (segments == null || segments.Length == 0) return;

        originalScale = new Vector3[segments.Length];
        originalAnchoredPos = new Vector2[segments.Length];
        canvasGroups = new CanvasGroup[segments.Length];

        for (int i = 0; i < segments.Length; i++)
        {
            var rt = segments[i];
            if (!rt) continue;

            originalScale[i] = rt.localScale;
            originalAnchoredPos[i] = rt.anchoredPosition;

            var cg = rt.GetComponent<CanvasGroup>();
            if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; // Start: unsichtbar, damit Fly-In sauber wirkt
            canvasGroups[i] = cg;
        }
    }

    void OnEnable()
    {
        selectionTriggered = false;

        // 1) Intro
        StartCoroutine(IntroFlyIn());

        // 2) Loop SOFORT starten (parallel zum Fly-In)
        if (loop && !selectionTriggered && loopRunner == null)
            loopRunner = StartCoroutine(RunLoop());

        // 3) Auswahl (Timer unabhängig vom Intro/Loop)
        Invoke(nameof(TriggerSelection), selectAfterSeconds);
    }

    void OnDisable()
    {
        CancelInvoke();
        if (loopRunner != null) StopCoroutine(loopRunner);
        loopRunner = null;

        // Reset aller Tweens & Zustände
        for (int i = 0; i < segments.Length; i++)
        {
            if (!segments[i]) continue;

            LeanTween.cancel(segments[i].gameObject);

            // Ursprungszustände
            segments[i].anchoredPosition = originalAnchoredPos[i];
            segments[i].localScale = originalScale[i];

            // Für sauberen nächsten Start wieder unsichtbar
            if (canvasGroups[i]) canvasGroups[i].alpha = 0f;

            // Falls beim letzten Durchlauf disabled wurde, wieder aktiv schalten
            segments[i].gameObject.SetActive(true);
        }
    }

    IEnumerator IntroFlyIn()
    {
        AudioManager.Instance?.PlaySFX2D("Start_Targeting");
        // Setze Start-Offset
        for (int i = 0; i < segments.Length; i++)
        {
            if (!segments[i]) continue;
            segments[i].anchoredPosition = originalAnchoredPos[i] + new Vector2(0f, flyInOffsetY);
            segments[i].localScale = originalScale[i] * 0.95f;
        }


        // Einfliegen + Fade-in gestaffelt
        for (int i = 0; i < segments.Length; i++)
        {
            var idx = i;
            var rt = segments[idx];
            if (!rt) continue;

            LeanTween.move(rt, originalAnchoredPos[idx], flyInTime)
                    .setEase(flyInEase)
                    .setDelay(flyInStagger * i);

            var cg = canvasGroups[idx];
            LeanTween.value(rt.gameObject, 0f, 1f, flyInTime * 0.9f)
                    .setOnUpdate(a => { if (cg) cg.alpha = a; })
                    .setDelay(flyInStagger * i);
        }

        // warte bis alle rein sind
        yield return new WaitForSeconds(flyInTime + flyInStagger * (segments.Length - 1) + 0.02f);
    }

    IEnumerator RunLoop()
    {
        do
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (selectionTriggered) yield break; // stoppt sofort, wenn Auswahl kommt
                AnimateOne(i);
                yield return new WaitForSeconds(gapBetweenSegments);
            }

            if (loopPause > 0f) yield return new WaitForSeconds(loopPause);
        }
        while (loop && !selectionTriggered);
    }

    void AnimateOne(int idx)
    {
        var rt = segments[idx];
        if (!rt) return;

        // Scale-Puls
        LeanTween.scale(rt, originalScale[idx] * scaleUpFactor, scaleUpTime)
                 .setEase(LeanTweenType.easeOutQuad)
                 .setOnComplete(() =>
                 {
                     LeanTween.delayedCall(rt.gameObject, holdTime, () =>
                     {
                         LeanTween.scale(rt, originalScale[idx], scaleDownTime)
                                  .setEase(LeanTweenType.easeInQuad);
                     });
                 });

        // Blink
        if (doBlink && canvasGroups[idx] != null)
        {
            var cg = canvasGroups[idx];
            LeanTween.value(rt.gameObject, cg.alpha, minAlpha, fadeTime)
                     .setOnUpdate(a => cg.alpha = a)
                     .setEase(LeanTweenType.easeOutQuad)
                     .setOnComplete(() =>
                     {
                         LeanTween.value(rt.gameObject, cg.alpha, 1f, fadeTime)
                                  .setOnUpdate(a => cg.alpha = a)
                                  .setEase(LeanTweenType.easeInQuad);
                     });
        }
    }

    // ---------------- Auswahl / Abschluss ----------------
    public void TriggerSelection()
    {
        if (selectionTriggered) return;
        selectionTriggered = true;

        AudioManager.Instance?.PlaySFX2D("Decision_Targeting");

        // Loop sofort beenden
        if (loopRunner != null) StopCoroutine(loopRunner);
        loopRunner = null;

        // Index bestimmen
        int chosen = forcedSelectIndex >= 0 && forcedSelectIndex < segments.Length
            ? forcedSelectIndex
            : Random.Range(0, segments.Length);

        // andere ausblenden
        for (int i = 0; i < segments.Length; i++)
        {
            if (i == chosen || !segments[i]) continue;
            var rt = segments[i];
            var cg = canvasGroups[i];

            LeanTween.cancel(rt.gameObject);
            // kleines „weg“-Feedback
            LeanTween.scale(rt, originalScale[i] * 0.85f, othersFadeScaleTime);
            LeanTween.value(rt.gameObject, cg ? cg.alpha : 1f, 0f, othersFadeScaleTime)
                     .setOnUpdate(a => { if (cg) cg.alpha = a; })
                     .setOnComplete(() => rt.gameObject.SetActive(false));
        }

        // gewähltes fliegt zum Ziel
        // gewähltes fliegt erst in die Mitte (Pop-Up), dann schrumpfend zum Ziel
        if (selectionTarget)
        {
            var chosenRT = segments[chosen];
            var chosenCG = canvasGroups[chosen];
            LeanTween.cancel(chosenRT.gameObject);

            // Ziel-Positionen vorbereiten
            // Mitte = midPoint (falls gesetzt), sonst (0,0) im gleichen Parent-Rect
            Vector2 midPos = midPoint ? midPoint.anchoredPosition : Vector2.zero;

            // 1) Flug in die Mitte + Pop-Up
            LeanTween.move(chosenRT, midPos, toMidTime)
                    .setEase(toMidEase);

            LeanTween.scale(chosenRT, originalScale[chosen] * midScaleFactor, toMidTime)
                    .setEase(LeanTweenType.easeOutBack);

            // 2) kurze Haltezeit in der Mitte, dann Abflug zum Ziel
            LeanTween.delayedCall(chosenRT.gameObject, toMidTime + midHoldTime, () =>
            {
                // Parallel: Move zum Ziel + Shrink + Fade-Out
                LeanTween.move(chosenRT, selectionTarget.anchoredPosition, chosenFlyTime)
                        .setEase(chosenFlyEase);

                LeanTween.scale(chosenRT, originalScale[chosen] * 0.4f, chosenFlyTime)
                        .setEase(LeanTweenType.easeInCubic);

                if (chosenCG)
                {
                    LeanTween.value(chosenRT.gameObject, chosenCG.alpha, 0f, chosenFlyTime)
                            .setOnUpdate(a => chosenCG.alpha = a)
                            .setEase(LeanTweenType.easeInCubic)
                            .setOnComplete(() => chosenRT.gameObject.SetActive(false));
                }
                else
                {
                    // Fallback, falls kein CanvasGroup existiert: trotzdem ausblenden
                    LeanTween.delayedCall(chosenRT.gameObject, chosenFlyTime, () =>
                    {
                        chosenRT.gameObject.SetActive(false);
                    });
                }

                // *** Reset NACH dem Ziel-Flug (nur noch chosenFlyTime + kleiner Puffer) ***
                LeanTween.delayedCall(chosenRT.gameObject, chosenFlyTime + 0.05f, () =>
                {
                    ResetAndDisableAll();
                });
            });
        }
        else
        {
            // Kein Ziel gesetzt: trotzdem sauber resetten
            LeanTween.delayedCall(gameObject, 0.25f, () => ResetAndDisableAll());
        }
    }

    // ---------------- Reset & Disable ----------------
    private void ResetAndDisableAll()
    {
        CancelInvoke();
        if (loopRunner != null)
        {
            StopCoroutine(loopRunner);
            loopRunner = null;
        }

        for (int i = 0; i < segments.Length; i++)
        {
            var rt = segments[i];
            if (!rt) continue;

            LeanTween.cancel(rt.gameObject);

            // Ursprungspos/scale
            rt.anchoredPosition = originalAnchoredPos[i];
            rt.localScale = originalScale[i];

            // wieder sichtbar machen fürs nächste Mal? -> Alpha 0 (Intro fade-in übernimmt)
            var cg = canvasGroups[i];
            if (cg) cg.alpha = 0f;

            // sicherstellen, dass sie beim nächsten Start aktiv sind
            rt.gameObject.SetActive(true);
        }

        selectionTriggered = false;

        // Ganze Einheit deaktivieren – beim nächsten Enable startet alles frisch
        gameObject.SetActive(false);
    }
}

