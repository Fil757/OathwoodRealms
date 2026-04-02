using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class GIF_Play : MonoBehaviour
{
    [Header("Frames")]
    public Sprite[] frames;

    [Header("Timing")]
    [Tooltip("Zeit pro Frame in Sekunden")]
    public float frameDuration = 0.1f;

    private Image image;
    private int currentIndex = 0;

    void Awake()
    {
        image = GetComponent<Image>();

        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("GIF_Play: Keine Frames gesetzt.");
            enabled = false;
            return;
        }

        StartCoroutine(PlayLoop());
    }

    IEnumerator PlayLoop()
    {
        while (true)
        {
            image.sprite = frames[currentIndex];
            currentIndex = (currentIndex + 1) % frames.Length;
            yield return new WaitForSeconds(frameDuration);
        }
    }
}
