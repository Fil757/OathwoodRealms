using UnityEngine;
using TMPro;

public class TrapCardDisplay : MonoBehaviour
{
    [Header("TrapCard Stats")]
    [Tooltip("Aktuelle Lebenspunkte der TrapCard")]
    public int lifepoints = 3;

    [Header("UI")]
    [Tooltip("TextMeshPro-Feld zur Anzeige der Lifepoints")]
    public TMP_Text lifepointsText;

    [Tooltip("RectTransform des LifePoint-Frames")]
    public RectTransform lifepointframe;

    [Header("LifePointFrame Position – Player 1")]
    public Vector2 lifepointframeAnchoredPos_P1 = Vector2.zero;
    public float lifepointframeLocalZ_P1 = 0f;

    [Header("LifePointFrame Position – Player 2")]
    public Vector2 lifepointframeAnchoredPos_P2 = Vector2.zero;
    public float lifepointframeLocalZ_P2 = 0f;

    [Tooltip("Wenn aktiv: Position/Z wird im Update permanent erzwungen (maximale Robustheit).")]
    public bool enforceFrameTransformInUpdate = true;

    // -------------------------------------------------
    // VFX / Shake beim Verschwinden
    // -------------------------------------------------

    [Header("VFX beim Zerstören (Dust + Shake)")]
    [Tooltip("Wenn gesetzt, wird diese Position als Dust-Spawn genutzt (World).")]
    public Transform dustPlaceTransform;

    [Tooltip("Optional: Wenn du eine feste World-Position erzwingen willst (ignoriert dustPlaceTransform).")]
    public bool useDustPlaceOverride = false;
    public Vector3 dustPlaceOverride = Vector3.zero;

    [Tooltip("ParticleController: EffectId / Index (bei dir: 6)")]
    public int dustEffectId = 6;

    public Vector3 dustScale = new Vector3(20f, 20f, 20f);
    public Vector3 dustEuler = new Vector3(-90f, 0f, 0f);

    [Tooltip("CameraShake Dauer")]
    public float shakeDuration = 0.5f;

    [Tooltip("CameraShake Stärke")]
    public float shakeMagnitude = 3f;

    [Tooltip("Wenn aus: kein VFX/Shake beim Zerstören.")]
    public bool playVfxOnDestroy = true;

    private const string FIELD_CONTROLLER_GO_NAME = "FieldCardController";

    private enum Side { Unknown, P1, P2 }
    private Side _cachedSide = Side.Unknown;

    private bool _destroyRequested = false;

    [Header("Audio beim Zerstören")]
    [Tooltip("Sound, der beim Verschwinden der Trap abgespielt wird")]
    public AudioClip destroyClip;

    [Range(0f, 1f)]
    public float destroyClipVolume = 0.8f;

    [Tooltip("Optionaler AudioMixerGroup (kann null sein)")]
    public UnityEngine.Audio.AudioMixerGroup destroyMixerGroup;

    // -------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------

    void OnEnable()
    {
        _destroyRequested = false;
        UpdateLifePointsText();
        ApplyLifePointFrameTransform(force: true);
    }

    void Update()
    {
        // bewusst im Update -> robust gegen externe Änderungen
        UpdateLifePointsText();

        if (enforceFrameTransformInUpdate)
            ApplyLifePointFrameTransform(force: false);

        // Robust: automatisch zerstören sobald 0 erreicht (aber nur einmal)
        if (!_destroyRequested && lifepoints <= 0)
        {
            DestroySelf();
        }
    }

    private void UpdateLifePointsText()
    {
        if (lifepointsText == null)
            return;

        lifepointsText.text = lifepoints.ToString();
    }

    // -------------------------------------------------
    // LifePointFrame – Position + Z
    // -------------------------------------------------

    private void ApplyLifePointFrameTransform(bool force)
    {
        if (lifepointframe == null)
            return;

        Side side = ResolveSideFromHierarchy();
        if (!force && side == _cachedSide)
            return;

        _cachedSide = side;

        if (side == Side.P1)
        {
            ApplyFrameTransform(lifepointframeAnchoredPos_P1, lifepointframeLocalZ_P1);
            return;
        }

        if (side == Side.P2)
        {
            ApplyFrameTransform(lifepointframeAnchoredPos_P2, lifepointframeLocalZ_P2);
            return;
        }
    }

    private void ApplyFrameTransform(Vector2 anchoredPos, float localZ)
    {
        // X/Y über Anchors
        lifepointframe.anchoredPosition = anchoredPos;

        // Z explizit über LocalPosition
        Vector3 lp = lifepointframe.localPosition;
        lp.z = localZ;
        lifepointframe.localPosition = lp;
    }

    private Side ResolveSideFromHierarchy()
    {
        Transform t = transform;
        while (t != null)
        {
            string n = t.name;

            if (n == "Traps_P1" || n == "Player1" || n == "P1")
                return Side.P1;

            if (n == "Traps_P2" || n == "Player2" || n == "P2")
                return Side.P2;

            t = t.parent;
        }

        return Side.Unknown;
    }

    // -------------------------------------------------
    // Öffentliche API
    // -------------------------------------------------

    public void SetLifePoints(int value)
    {
        lifepoints = Mathf.Max(0, value);
        UpdateLifePointsText();

        if (!_destroyRequested && lifepoints <= 0)
            DestroySelf();
    }

    public void ModifyLifePoints(int delta)
    {
        lifepoints = Mathf.Max(0, lifepoints + delta);
        UpdateLifePointsText();

        if (!_destroyRequested && lifepoints <= 0)
            DestroySelf();
    }

    /// <summary>
    /// Zerstört die TrapCard (GameObject).
    /// Entfernt vorher den Eintrag aus ActiveTrapCard anhand der SiblingOrder.
    /// + Dust/Shake an der Position, wo sie verschwindet.
    /// </summary>
    public void DestroySelf()
    {
        if (_destroyRequested)
            return;

        _destroyRequested = true;

        // 1) VFX/Shake an der aktuellen "Verschwindeposition"
        if (playVfxOnDestroy)
            PlayDestroyVfxAndShake();

        // 2) Controller-Abmeldung
        UnregisterFromController_BySiblingOrder();

        // 3) Destroy
        Destroy(gameObject);
    }

    private void PlayDestroyVfxAndShake()
    {
        Vector3 dustPos = ResolveDustWorldPosition();

        // Particle (soft-guard)
        if (ParticleController.Instance != null)
        {
            ParticleController.Instance.PlayParticleEffect(
                dustPos,
                dustEffectId,
                dustScale,
                Quaternion.Euler(dustEuler)
            );
        }
        else
        {
            Debug.LogWarning($"{name} -> PlayDestroyVfxAndShake: ParticleController.Instance ist null.");
        }

        // Shake (soft-guard)
        if (CameraShake.current != null)
        {
            CameraShake.current.Shake(shakeDuration, shakeMagnitude);
        }
        else
        {
            Debug.LogWarning($"{name} -> PlayDestroyVfxAndShake: CameraShake.current ist null.");
        }

        // --------------------
        // Audio (soft-guard)
        // --------------------
        PlayDestroyAudio(dustPos);
    }

    private Vector3 ResolveDustWorldPosition()
    {
        if (useDustPlaceOverride)
            return dustPlaceOverride;

        if (dustPlaceTransform != null)
            return dustPlaceTransform.position;

        // Fallback: Trap selbst
        return transform.position;
    }

    // -------------------------------------------------
    // Intern: Controller-Abmeldung
    // -------------------------------------------------

    private void UnregisterFromController_BySiblingOrder()
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            Debug.LogWarning($"{name} -> DestroySelf: Kein Parent vorhanden.");
            return;
        }

        int myOrder = transform.GetSiblingIndex();
        string parentName = parent.name;

        GameObject controllerGO = GameObject.Find(FIELD_CONTROLLER_GO_NAME);
        if (controllerGO == null)
        {
            Debug.LogWarning($"{name} -> DestroySelf: '{FIELD_CONTROLLER_GO_NAME}' nicht gefunden.");
            return;
        }

        if (parentName == "Traps_P1")
        {
            FieldCardController p1 = controllerGO.GetComponent<FieldCardController>();
            if (p1 == null)
            {
                Debug.LogWarning($"{name} -> DestroySelf: Kein FieldCardController (P1).");
                return;
            }

            RemoveAtOrder(p1.ActiveTrapCard, myOrder, "P1");
            return;
        }

        if (parentName == "Traps_P2")
        {
            FieldCardController_P2 p2 = controllerGO.GetComponent<FieldCardController_P2>();
            if (p2 == null)
            {
                Debug.LogWarning($"{name} -> DestroySelf: Kein FieldCardController_P2 (P2).");
                return;
            }

            RemoveAtOrder(p2.ActiveTrapCard, myOrder, "P2");
            return;
        }

        Debug.LogWarning($"{name} -> DestroySelf: Unerwarteter Parent '{parentName}'.");
    }

    private void RemoveAtOrder(
        System.Collections.Generic.List<FieldCard> list,
        int order,
        string sideLabel
    )
    {
        if (list == null)
        {
            Debug.LogWarning($"{name} -> ActiveTrapCard ist null ({sideLabel}).");
            return;
        }

        if (order < 0 || order >= list.Count)
        {
            Debug.LogWarning($"{name} -> Order={order} außerhalb Count={list.Count} ({sideLabel}).");
            return;
        }

        list.RemoveAt(order);
    }
    
    private void PlayDestroyAudio(Vector3 worldPos)
    {
        if (destroyClip == null)
            return;

        GameObject audioGO = new GameObject("Trap_DestroyAudio");
        audioGO.transform.position = worldPos;

        AudioSource src = audioGO.AddComponent<AudioSource>();
        src.clip = destroyClip;
        src.volume = destroyClipVolume;
        src.spatialBlend = 1f;          // 3D-Sound
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 15f;
        src.playOnAwake = false;

        if (destroyMixerGroup != null)
            src.outputAudioMixerGroup = destroyMixerGroup;

        src.Play();

        Destroy(audioGO, destroyClip.length + 0.1f);
    }

}
