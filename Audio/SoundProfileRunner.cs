using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class SoundProfileRunner : MonoBehaviour
{
    private SoundProfile profile;
    private SoundProfileController controller;
    private AudioSource[] sources;
    private bool initialized;

    public void Init(SoundProfile profile, SoundProfileController controller, bool is3D)
    {
        this.profile = profile;
        this.controller = controller;

        if (profile == null || profile.layers == null || profile.layers.Length == 0)
        {
            Debug.LogWarning("[SoundProfileRunner] Init mit leerem Profil. Zerstöre Runner.", this);
            Destroy(gameObject);
            return;
        }

        sources = new AudioSource[profile.layers.Length];

        for (int i = 0; i < profile.layers.Length; i++)
        {
            var layer = profile.layers[i];
            if (!layer.clip)
                continue;

            var src = gameObject.AddComponent<AudioSource>();
            src.clip = layer.clip;
            src.playOnAwake = false;
            src.loop = layer.loop;
            src.volume = layer.volume * profile.masterVolume;
            src.pitch = Mathf.Approximately(layer.pitch, 0f) ? 1f : layer.pitch;

            src.spatialBlend = is3D ? 1f : controller.defaultSpatialBlend;

            if (layer.mixerGroupOverride != null)
                src.outputAudioMixerGroup = layer.mixerGroupOverride;
            else if (controller.defaultMixerGroup != null)
                src.outputAudioMixerGroup = controller.defaultMixerGroup;

            sources[i] = src;
        }

        initialized = true;
        StartCoroutine(PlayAllLayersRoutine());
    }

    private IEnumerator PlayAllLayersRoutine()
    {
        if (!initialized)
            yield break;

        float maxEndTime = 0f;
        bool hasInfiniteLoop = false;

        for (int i = 0; i < profile.layers.Length; i++)
        {
            var layer = profile.layers[i];
            var src = sources[i];

            if (src == null || layer.clip == null)
                continue;

            // Start jede Spur in eigener Coroutine mit ihrem individuellen Offset.
            StartCoroutine(PlaySingleLayer(src, layer));

            if (src.loop)
            {
                hasInfiniteLoop = true;
            }
            else
            {
                float pitch = Mathf.Approximately(layer.pitch, 0f) ? 1f : layer.pitch;
                float endTime = layer.startTime + (layer.clip.length / Mathf.Abs(pitch));
                if (endTime > maxEndTime)
                    maxEndTime = endTime;
            }
        }

        // Auto-Cleanup nur, wenn kein Loop vorhanden ist und autoCleanup aktiviert wurde
        if (profile.autoCleanup && !hasInfiniteLoop && maxEndTime > 0f)
        {
            yield return new WaitForSeconds(maxEndTime);
            Destroy(gameObject);
        }
    }

    private IEnumerator PlaySingleLayer(AudioSource src, SoundProfile.SoundLayer layer)
    {
        if (layer.startTime > 0f)
            yield return new WaitForSeconds(layer.startTime);

        src.Play();
    }

    /// <summary>
    /// Sofort alles stoppen und Runner zerstören (z.B. bei Szenenwechsel oder Abbruch).
    /// </summary>
    public void StopAndDestroy()
    {
        StopAllCoroutines();

        if (sources != null)
        {
            foreach (var s in sources)
            {
                if (s != null)
                    s.Stop();
            }
        }

        Destroy(gameObject);
    }
}
