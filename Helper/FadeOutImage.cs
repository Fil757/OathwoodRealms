using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class FadeOutImage : MonoBehaviour
{
    [Header("Timing")]
    public float fadeDuration = 1.5f;

    [Header("Curve (optional)")]
    [Tooltip("Wenn aktiv, wird diese Kurve statt linearer Interpolation verwendet")]
    public bool useCurve = false;

    [Tooltip("X = Zeit (0–1), Y = Alpha-Faktor (0–1)")]
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private Image img;

    private void Awake()
    {
        img = GetComponent<Image>();
    }

    private void Start()
    {
        FadeOut();
    }

    /// <summary>
    /// Startet ein Fade-Out von Alpha 1 → 0
    /// </summary>
    public void FadeOut()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float time = 0f;
        Color c = img.color;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / fadeDuration);

            float eval = useCurve ? fadeCurve.Evaluate(t) : t;

            // eval = 0 → Alpha 1 | eval = 1 → Alpha 0
            c.a = Mathf.Lerp(1f, 0f, eval);
            img.color = c;

            yield return null;
        }

        c.a = 0f;
        img.color = c;
    }
}
