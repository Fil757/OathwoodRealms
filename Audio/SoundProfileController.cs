using UnityEngine;
using UnityEngine.Audio;

public class SoundProfileController : MonoBehaviour
{
    public static SoundProfileController current;

    [Header("Default-Audio-Einstellungen")]
    [Tooltip("Standard-Mixer für alle Profile, sofern der Layer keinen Override nutzt.")]
    public AudioMixerGroup defaultMixerGroup;

    [Tooltip("0 = reines 2D-Audio, 1 = reines 3D-Audio")]
    [Range(0f, 1f)]
    public float defaultSpatialBlend = 0f;

    [Tooltip("Wenn true, bleibt der Controller zwischen Szenenwechseln erhalten.")]
    public bool dontDestroyOnLoad = true;

    private void Awake()
    {
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }

        current = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Kernmethode: Startet ein SoundProfile.
    /// attachTo = Transform, an dem die Runner-Instanz hängt (z.B. Figur).
    /// is3D = true -> spatialBlend 1, sonst defaultSpatialBlend.
    /// </summary>
    public SoundProfileRunner PlayProfile(SoundProfile profile, Transform attachTo, bool is3D)
    {
        if (profile == null)
        {
            Debug.LogWarning("[SoundProfileController] PlayProfile aufgerufen mit null-Profil.");
            return null;
        }

        if (profile.layers == null || profile.layers.Length == 0)
        {
            Debug.LogWarning($"[SoundProfileController] Profil '{profile.profileName}' hat keine Layer.");
            return null;
        }

        var go = new GameObject($"SoundProfileRunner_{profile.profileName}");
        if (attachTo != null)
        {
            go.transform.SetParent(attachTo, false);
            go.transform.localPosition = Vector3.zero;
        }

        var runner = go.AddComponent<SoundProfileRunner>();
        runner.Init(profile, this, is3D);

        return runner;
    }

    // --- PUBLIC API SHORTCUTS -------------------------------------------------

    // Einfache API – 2D Audio ohne Attach-Target
    public SoundProfileRunner PlaySound(SoundProfile profile)
    {
        return PlayProfile(profile, null, false);
    }

    // 3D Sound direkt an Objekt koppeln
    public SoundProfileRunner PlaySound(SoundProfile profile, Transform attachTo)
    {
        return PlayProfile(profile, attachTo, true);
    }

    // Frei wählbar ob 2D oder 3D
    public SoundProfileRunner PlaySound(SoundProfile profile, Transform attachTo, bool is3D)
    {
        return PlayProfile(profile, attachTo, is3D);
    }
}
