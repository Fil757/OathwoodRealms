using System.Collections;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    public static ParticleController Instance { get; private set; }

    [Tooltip("Liste der Partikelsystem-Vorlagen")]
    public ParticleSystem[] particleEffect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Mehr als ein ParticleController vorhanden. Alte Instanz wird behalten.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public void PlayParticleEffect(Vector3 worldPosition, int particleIndex, Vector3 scale, Quaternion rotation, Transform parent = null)
    {
        ParticleSystem prefab = particleEffect[particleIndex];
        ParticleSystem instance = Instantiate(prefab, worldPosition, rotation);

        if (parent != null)
        {
            // Parent setzen und Weltposition beibehalten
            instance.transform.SetParent(parent, true);
        }

        instance.transform.localScale = scale;

        instance.Play();
        StartCoroutine(DestroyWhenFinished(instance));
    }

    private IEnumerator DestroyWhenFinished(ParticleSystem ps)
    {
        // Warte bis keine lebenden Partikel mehr da sind (inkl. Sub-Emitter)
        yield return new WaitUntil(() => !ps.IsAlive(true));
        Destroy(ps.gameObject);
    }
}

