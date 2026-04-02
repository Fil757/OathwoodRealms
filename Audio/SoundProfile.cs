using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(
    fileName = "New_SoundProfile",
    menuName = "Audio/SoundProfile",
    order = 10)]
public class SoundProfile : ScriptableObject
{
    [System.Serializable]
    public class SoundLayer
    {
        [Header("AudioClip der abgespielt werden soll")]
        public AudioClip clip;

        [Header("Startzeit in Sekunden bezogen auf Profil-Start")]
        [Tooltip("0 = direkt starten, >0 = Verzögerung")]
        public float startTime = 0f;

        [Header("Lautstärke 0–1")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Header("Pitch/Tonhöhe (1 = normal)")]
        [Range(-3f, 3f)]
        public float pitch = 1f;

        [Header("Looping dieses einzelnen Layers")]
        public bool loop = false;

        [Header("Optionaler Mixer-Override für diesen Layer")]
        public AudioMixerGroup mixerGroupOverride;
    }

    [Header("Eindeutige ID / Anzeigename")]
    public string profileName;

    [Header("Alle Sounds, die dieses Profil ergeben")]
    public SoundLayer[] layers;

    [Header("Globale Einstellungen")]
    [Tooltip("Wird mit Layer-Volume multipliziert")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Tooltip("Wenn true, wird die Instanz nach Abspielende automatisch zerstört (sofern keine Loops).")]
    public bool autoCleanup = true;

    [TextArea(2, 4)]
    public string notes;
}
