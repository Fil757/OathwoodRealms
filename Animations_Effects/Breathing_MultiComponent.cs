using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIBreathingScale : MonoBehaviour
{
    [Header("Aktive Achsen")]
    public bool animateX = true;
    public bool animateY = true;
    public bool animateZ = false;

    [Header("Atmung")]
    [Tooltip("Amplitude der Skalierung (0.05 = 5% Dehnung)")]
    public float amplitude = 0.05f;

    [Tooltip("Frequenz der Atmung (Zyklen pro Sekunde)")]
    public float frequency = 1.2f;

    [Header("Zufallsvariation")]
    [Tooltip("Maximale zeitliche Verschiebung, damit mehrere Objekte nicht synchron atmen")]
    public float maxPhaseOffset = 1.5f;

    private RectTransform rect;
    private Vector3 baseScale;
    private float phaseOffset;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        baseScale = rect.localScale;

        // Zufällige Phasenverschiebung pro Instanz
        phaseOffset = Random.Range(0f, maxPhaseOffset);
    }

    private void OnEnable()
    {
        rect.localScale = baseScale;
    }

    private void Update()
    {
        float t = Mathf.Sin((Time.time * frequency) + phaseOffset) * amplitude;

        float sx = animateX ? baseScale.x + t : baseScale.x;
        float sy = animateY ? baseScale.y + t : baseScale.y;
        float sz = animateZ ? baseScale.z + t : baseScale.z;

        rect.localScale = new Vector3(sx, sy, sz);
    }
}
