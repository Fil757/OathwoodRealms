using UnityEngine;

public class YourTurnTextAnimator : MonoBehaviour
{
    public static YourTurnTextAnimator current_yt;

    [Header("Target (wird animiert)")]
    [Tooltip("Das eigentliche Your-Turn-Text-Objekt (initial inactive).")]
    public GameObject targetObject;

    [Tooltip("RectTransform des Target-Objekts (optional, sonst automatisch).")]
    public RectTransform targetRect;

    [Tooltip("CanvasGroup des Target-Objekts (optional, für Fade).")]
    public CanvasGroup canvasGroup;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip playSfx;
    [Range(0f, 1f)] public float playSfxVolume = 1f;

    [Header("Timing")]
    public float popInDuration = 0.18f;
    public float holdDuration = 0.22f;
    public float dipDuration = 0.12f;
    public float flyUpDuration = 0.45f;

    [Header("Scale")]
    public float startScale = 0.05f;
    public float fullScale = 1.0f;

    [Header("Motion")]
    public Vector3 startLocalPos = Vector3.zero;
    public float dipDownDistance = 35f;
    public float flyUpDistance = 280f;
    public float flySideDistance = 30f;

    [Header("Behaviour")]
    public bool useUnscaledTime = true;
    public bool restartIfPlaying = true;

    private Vector3 _defaultLocalPos;
    private Vector3 _defaultScale;
    private Quaternion _defaultLocalRot;
    private bool _isPlaying;

    void Awake()
    {
        current_yt = this;
        
        if (targetObject == null)
        {
            Debug.LogError("[YourTurnTextAnimator] No targetObject assigned.");
            enabled = false;
            return;
        }

        if (targetRect == null)
            targetRect = targetObject.GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = targetObject.GetComponent<CanvasGroup>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _defaultLocalPos = targetRect.localPosition;
        _defaultScale = targetRect.localScale;
        _defaultLocalRot = targetRect.localRotation;

        ResetVisuals();
        targetObject.SetActive(false);
    }

    /// <summary>
    /// Vom TurnManager aufrufen, wenn der Bot seinen Zug beendet.
    /// </summary>
    public void Play()
    {
        if (_isPlaying && !restartIfPlaying) return;

        // Tweens auf dem TARGET killen, nicht auf diesem Objekt
        LeanTween.cancel(targetObject);

        _isPlaying = true;

        // Sound sofort
        if (audioSource != null && playSfx != null)
            audioSource.PlayOneShot(playSfx, playSfxVolume);

        targetObject.SetActive(true);
        targetObject.transform.SetAsLastSibling();

        ResetVisualsForStart();

        // 1) Pop-In
        LeanTween.scale(targetRect, Vector3.one * fullScale, popInDuration)
            .setEaseOutBack()
            .setIgnoreTimeScale(useUnscaledTime);

        // 2) Dip + Fly
        float dipDelay = popInDuration + holdDuration;

        LeanTween.delayedCall(dipDelay, () =>
        {
            Vector3 start = targetRect.localPosition;
            Vector3 dipPos = start + Vector3.down * dipDownDistance;

            // Dip
            LeanTween.moveLocal(targetObject, dipPos, dipDuration)
                .setEaseInQuad()
                .setIgnoreTimeScale(useUnscaledTime)
                .setOnComplete(() =>
                {
                    Vector3 flyTarget =
                        start + new Vector3(flySideDistance, flyUpDistance, 0f);

                    // Hauptflug
                    LeanTween.moveLocal(targetObject, flyTarget, flyUpDuration)
                        .setEaseOutCubic()
                        .setIgnoreTimeScale(useUnscaledTime);

                    LeanTween.scale(targetRect,
                        Vector3.one * (fullScale * 0.97f), flyUpDuration)
                        .setEaseInQuad()
                        .setIgnoreTimeScale(useUnscaledTime);

                    if (canvasGroup != null)
                    {
                        LeanTween.value(targetObject, 1f, 0f, flyUpDuration * 0.75f)
                            .setDelay(flyUpDuration * 0.1f)
                            .setIgnoreTimeScale(useUnscaledTime)
                            .setOnUpdate(a => canvasGroup.alpha = a);
                    }

                    // 3) Cleanup
                    LeanTween.delayedCall(flyUpDuration, EndAnimation)
                        .setIgnoreTimeScale(useUnscaledTime);
                });

        }).setIgnoreTimeScale(useUnscaledTime);
    }

    private void EndAnimation()
    {
        ResetVisuals();
        targetObject.SetActive(false);
        _isPlaying = false;
    }

    private void ResetVisualsForStart()
    {
        targetRect.localPosition = startLocalPos;
        targetRect.localRotation = Quaternion.identity;
        targetRect.localScale = Vector3.one * startScale;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void ResetVisuals()
    {
        targetRect.localPosition = _defaultLocalPos;
        targetRect.localRotation = _defaultLocalRot;
        targetRect.localScale = _defaultScale;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }
}
