using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class Breathing : MonoBehaviour
{
    public enum BreathingAxis
    {
        X,
        Y,
        Z
    }

    [Header("Atmungsbereich")]
    [Tooltip("Achse, entlang der die Atmung stattfindet.")]
    public BreathingAxis axis = BreathingAxis.Y;

    [Tooltip("Nur Vertices oberhalb dieser Grenze entlang der gewählten Achse werden animiert.")]
    public float minCoord = -0.5f;

    [Header("Bewegungseinstellungen")]
    public float amplitude = 0.06f;
    public float frequency = 1.5f;

    [Header("Variation (optional)")]
    [Tooltip("Zufällige Phasenverschiebung pro Instanz, damit nicht alle synchron atmen.")]
    public bool useRandomPhase = true;
    [Range(0f, 2f * Mathf.PI)]
    public float phaseOffset = 0f;  // wird bei Start evtl. randomisiert
    [Tooltip("Kleine zufällige Frequenzabweichung (+/- Prozent).")]
    public float freqJitterPercent = 0f; // z.B. 5 = +/-5%

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] workingVertices;
    private float freqEffective;

    void Awake()
    {
        var mf = GetComponent<MeshFilter>();
        mesh = mf.mesh; // Instanz pro Objekt (kein sharedMesh)
        mesh.MarkDynamic();
    }

    void Start()
    {
        baseVertices = mesh.vertices;
        workingVertices = new Vector3[baseVertices.Length];

        // stabile Zufallsquelle je Instanz (nicht frame-abhängig)
        if (useRandomPhase)
        {
            var seed = gameObject.GetInstanceID();
            var rng = new System.Random(seed);

            // Phase in [0, 2π)
            phaseOffset = (float)(rng.NextDouble() * 2.0 * Mathf.PI);

            // optionale kleine Frequenzstreuung
            if (freqJitterPercent > 0f)
            {
                var p = freqJitterPercent / 100f;
                var jitter = (float)((rng.NextDouble() * 2.0 - 1.0) * p);
                freqEffective = frequency * (1f + jitter);
            }
            else
            {
                freqEffective = frequency;
            }
        }
        else
        {
            freqEffective = frequency;
        }
    }

    void Update()
    {
        // Sinus mit Phasenoffset
        float scale = 1f + Mathf.Sin(Time.time * freqEffective + phaseOffset) * amplitude;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            var v = baseVertices[i];

            // Koordinate entlang der gewählten Achse holen
            float coord = 0f;
            switch (axis)
            {
                case BreathingAxis.X:
                    coord = v.x;
                    break;
                case BreathingAxis.Y:
                    coord = v.y;
                    break;
                case BreathingAxis.Z:
                    coord = v.z;
                    break;
            }

            // nur Vertices oberhalb der Grenze animieren
            if (coord >= minCoord)
            {
                switch (axis)
                {
                    case BreathingAxis.X:
                        v.x *= scale;
                        break;
                    case BreathingAxis.Y:
                        v.y *= scale;
                        break;
                    case BreathingAxis.Z:
                        v.z *= scale;
                        break;
                }
            }

            workingVertices[i] = v;
        }

        mesh.vertices = workingVertices;
        mesh.RecalculateNormals();
        // Optional: mesh.RecalculateBounds();
    }
}
