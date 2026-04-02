using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HoverFadeImages : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Images die eingeblendet werden sollen")]
    public Image[] fadeTargets;

    [Header("Fade Einstellungen")]
    public float fadeDuration = 0.25f;
    [Range(0f, 1f)] public float visibleAlpha = 1f;

    private float[] originalAlphas;

    private void Awake()
    {
        // Originalalphas merken
        originalAlphas = new float[fadeTargets.Length];
        for(int i = 0; i < fadeTargets.Length; i++)
        {
            if (fadeTargets[i] != null)
            {
                originalAlphas[i] = fadeTargets[i].color.a;
                // Hidden initialisieren
                SetAlpha(fadeTargets[i], 0f);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        for (int i = 0; i < fadeTargets.Length; i++)
        {
            if (fadeTargets[i] == null) continue;
            float targetA = visibleAlpha * originalAlphas[i];
            fadeTargets[i].CrossFadeAlpha(targetA, fadeDuration, false);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        for (int i = 0; i < fadeTargets.Length; i++)
        {
            if (fadeTargets[i] == null) continue;
            fadeTargets[i].CrossFadeAlpha(0f, fadeDuration, false);
        }
    }

    private void SetAlpha(Image img, float alpha)
    {
        var c = img.color;
        c.a = alpha;
        img.color = c;
    }
}
