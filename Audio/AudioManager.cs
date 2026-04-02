using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Serializable]
    public class SoundEntry
    {
        public string id;                 // z.B. "CardPlace", "Attack", "WinFanfares"
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = false;
        public AudioMixerGroup outputMixerGroup;
    }

    [Header("Datenbank")]
    public SoundEntry[] sounds;

    [Header("AudioSources")]
    public AudioSource musicSource;      // Für BGM
    public AudioSource sfx2DSource;      // Für UI- und globale SFX

    [Header("3D SFX Pool")]
    public int sfx3DPoolSize = 5;
    public AudioSource sfx3DPrefab;      // Ein AudioSource-Prefab mit 3D-Settings
    private AudioSource[] sfx3DPool;

    private Dictionary<string, SoundEntry> soundLookup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Dictionary bauen (O(1)-Lookup statt Schleife)
        soundLookup = new Dictionary<string, SoundEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sounds)
        {
            if (!string.IsNullOrEmpty(s.id) && s.clip != null)
            {
                soundLookup[s.id] = s;
            }
        }

        // 3D-Pool bauen
        if (sfx3DPrefab != null && sfx3DPoolSize > 0)
        {
            sfx3DPool = new AudioSource[sfx3DPoolSize];
            for (int i = 0; i < sfx3DPoolSize; i++)
            {
                var src = Instantiate(sfx3DPrefab, transform);
                sfx3DPool[i] = src;
            }
        }
    }

    private void Start()
    {
        //Start des Musictracks
        PlayMusic("Music");
    }

    // --------- Public API ---------

    public void PlayMusic(string id)
    {
        if (!TryGetSound(id, out var s)) return;

        musicSource.clip = s.clip;
        musicSource.volume = s.volume;
        musicSource.loop = s.loop;
        if (s.outputMixerGroup != null)
            musicSource.outputAudioMixerGroup = s.outputMixerGroup;

        musicSource.Play();
    }

    public void PlaySFX2D(string id)
    {
        if (!TryGetSound(id, out var s)) return;

        sfx2DSource.PlayOneShot(s.clip, s.volume);
    }

    public void PlaySFX3D(string id, Vector3 worldPos)
    {
        if (!TryGetSound(id, out var s)) return;
        if (sfx3DPool == null || sfx3DPool.Length == 0) return;

        var src = GetFree3DSource();
        if (src == null) return;

        src.transform.position = worldPos;
        src.clip = s.clip;
        src.volume = s.volume;
        src.loop = false;
        if (s.outputMixerGroup != null)
            src.outputAudioMixerGroup = s.outputMixerGroup;

        src.Play();
    }

    // --------- Helpers ---------

    private bool TryGetSound(string id, out SoundEntry sound)
    {
        if (soundLookup != null && soundLookup.TryGetValue(id, out sound))
            return true;

        Debug.LogWarning($"[AudioManager] Sound-ID '{id}' nicht gefunden.");
        sound = null;
        return false;
    }

    private AudioSource GetFree3DSource()
    {
        foreach (var src in sfx3DPool)
        {
            if (!src.isPlaying)
                return src;
        }
        // Optional: ältesten Sound überschreiben statt nichts zu spielen
        return sfx3DPool[0];
    }
}
