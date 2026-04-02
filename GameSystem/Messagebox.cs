using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Messagebox : MonoBehaviour
{
    [Header("Settings")]
    public static Messagebox current;

    [Tooltip("Dauer für Ein- und Ausblenden")]
    public float fadeDuration = 0.5f;

    [Tooltip("Wie lange bleibt die Box voll sichtbar")]
    public float visibleTime = 1f;

    [Tooltip("Maximaler zufälliger Versatz in Pixeln (X/Y)")]
    public float randomOffset = 0f;

    private const string DEFAULT_TEXT = "Not enough Mana for this";

    private Image image;
    private TextMeshProUGUI tmp;
    private Vector3 originalPos;

    // Merker für die laufende Routine, damit wir sie stoppen können
    private Coroutine fadeRoutine;

    private void Awake()
    {
        current = this;

        image = GetComponent<Image>();
        tmp = GetComponentInChildren<TextMeshProUGUI>();
        originalPos = transform.localPosition;

        // Anfangszustand transparent
        SetAlpha(0f);
    }

    /// <summary>
    /// Optionales Overload zum Text setzen.
    /// </summary>
    public void ShowMessageBox(string text)
    {
        // Nur setzen, wenn wirklich Text übergeben wurde.
        // (So überschreibt ein leerer/whitespace String nichts.)
        if (tmp != null && !string.IsNullOrWhiteSpace(text))
            tmp.text = text;

        ShowMessageBox();
    }

    public void ShowMessageBox()
    {
        // Wenn KEIN Text angegeben/gesetzt wurde: Default nutzen,
        // aber NICHT überschreiben, wenn schon etwas drinsteht.
        if (tmp != null && string.IsNullOrWhiteSpace(tmp.text))
            tmp.text = DEFAULT_TEXT;

        // Wenn schon ein Ablauf läuft: hart stoppen und sauber zurücksetzen
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        // Zustand zurücksetzen (wichtig, damit alte Offsets/Alpha nicht „durchbluten“)
        transform.localPosition = originalPos;
        SetAlpha(0f);

        // Neuer zufälliger Versatz
        float randX = Random.Range(-randomOffset, randomOffset);
        float randY = Random.Range(-randomOffset, randomOffset);
        transform.localPosition = originalPos + new Vector3(randX, randY, 0f);

        // Frisch starten
        fadeRoutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // WICHTIG: KEIN StartCoroutine(...) hier; so sind die Subroutines an diese Routine „gelinkt“
        yield return Fade(0f, 1f, fadeDuration);
        yield return new WaitForSeconds(visibleTime);
        yield return Fade(1f, 0f, fadeDuration);

        // Aufräumen / Reset
        transform.localPosition = originalPos;
        fadeRoutine = null;
    }

    private IEnumerator Fade(float start, float end, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(end);
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            float t = time / duration;
            float alpha = Mathf.Lerp(start, end, t);
            SetAlpha(alpha);
            time += Time.deltaTime;
            yield return null;
        }
        SetAlpha(end);
    }

    private void SetAlpha(float a)
    {
        if (image != null)
        {
            var c = image.color;
            c.a = a;
            image.color = c;
        }
        if (tmp != null)
        {
            var tc = tmp.color;
            tc.a = a;
            tmp.color = tc;
        }
    }
}
