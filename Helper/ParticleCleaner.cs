using UnityEngine;

public class ParticleCleaner : MonoBehaviour
{
    public static ParticleCleaner current;

    void Awake()
    {
        current = this;
    }

    public void DestroyFinishedParticles()
    {
        ParticleSystem[] particles = FindObjectsOfType<ParticleSystem>(true);

        foreach (var ps in particles)
        {
            if (!ps.IsAlive())
            {
                Destroy(ps.gameObject);
            }
        }
    }
}
