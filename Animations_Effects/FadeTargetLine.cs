using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class FadeTargetLine : MonoBehaviour
{
    public float fadeDuration = 0.5f;
    public float blinkInterval = 1.0f;
    public float visibleAlpha = 1f;
    public float hiddenAlpha = 0f;

    private Image image;
    private Coroutine blinkCoroutine;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void OnEnable()
    {
        StartBlinking();
    }

    void OnDisable()
    {
        StopBlinking();
    }

    public void StartBlinking()
    {
        if (blinkCoroutine == null)
            blinkCoroutine = StartCoroutine(BlinkLoop());
    }

    public void StopBlinking()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
    }

    private IEnumerator BlinkLoop()
    {
        bool visible = false;

        while (true)
        {
            float targetAlpha = visible ? hiddenAlpha : visibleAlpha;
            yield return StartCoroutine(FadeToAlpha(targetAlpha));
            visible = !visible;
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    private IEnumerator FadeToAlpha(float targetAlpha)
    {
        Color startColor = image.color;
        float startAlpha = startColor.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            image.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        image.color = new Color(startColor.r, startColor.g, startColor.b, targetAlpha);
    }
}
