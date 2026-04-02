using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TCG;

public class GameOverController : MonoBehaviour
{
    public static GameOverController current;

    #region Inspector References (Scene Objects)

    [Header("UI (Bases & Hands)")]
    public GameObject Player_Base_1; // UI Ornament P1
    public GameObject Player_Hand_1; // UI Hand P1
    public GameObject Player_Base_2; // UI Ornament P2
    public GameObject Player_Hand_2; // UI Hand P2

    [Header("Game Layout (Effect Positions)")]
    public GameObject Player_EffectPosition_1; // VFX anchor if P1 loses
    public GameObject Player_EffectPosition_2; // VFX anchor if P2 loses

    [Header("Game Layout (Fields)")]
    public GameObject Figure_Field_1;
    public GameObject Artefact_Field_1;
    public GameObject Trap_Field_1;

    public GameObject Figure_Field_2;
    public GameObject Artefact_Field_2;
    public GameObject Trap_Field_2;

    [Header("GameOver UI")]
    public GameObject GameOverScreen;

    public GameObject RayCastBlocker;

    #endregion

    #region GameOver Text (NEW)

    [Header("GameOver Text (NEW)")]
    public TMP_Text MainText;
    public TMP_Text SubText;

    [Header("Text Content")]
    public string P1Win_Main = "VICTORY!";
    public string P1Win_Sub = "WELL PLAYED";

    public string P1Lose_Main = "DEFEAT";
    public string P1Lose_Sub = "TRY AGAIN";

    #endregion

    #region GameOver Check

    // Health P1/P2 via PlayerBaseController.current.current_P1_Health / current_P2_Health

    [Header("GameOver Check")]
    public bool AutoCheckInUpdate = true;
    public int LoseWhenHealthAtOrBelow = 0;

    [Header("Update Start Delay")]
    public float UpdateStartDelay = 6f; // Update starts after X seconds

    #endregion

    #region Sequence Timing

    [Header("Sequence Timing")]
    public float StartDelay = 0.15f;
    public float StepDelay = 0.35f;
    public float EndDelayBeforeUI = 0.25f;

    #endregion

    #region GameOver Screen Grow

    [Header("GameOver Screen Grow")]
    public bool AnimateGameOverScale = true;
    public float GameOverScaleStartDelay = 0.10f;
    public float GameOverScaleDuration = 0.25f;
    public Vector3 GameOverTargetScale = Vector3.one;

    public enum EaseMode { Linear, SmoothStep, EaseOutBack }
    public EaseMode GameOverScaleEase = EaseMode.SmoothStep;

    #endregion

    #region Particles (optional)

    [Header("Particles (optional)")]
    public GameObject DestructionParticlePrefab;
    public bool DestroyParticleAfterPlay = true;
    public float ParticleFallbackLifetime = 3f;

    [Tooltip("Position offset relative to EffectPosition (or Transform fallback).")]
    public Vector3 ParticleOffset = Vector3.zero;

    [Tooltip("Local scale for particle object.")]
    public Vector3 ParticleScale = Vector3.one;

    [Tooltip("Additional rotation (Euler) relative to spawn rotation.")]
    public Vector3 ParticleRotationEuler = Vector3.zero;

    #endregion

    #region Audio (optional)

    [Header("Audio (optional)")]
    public AudioSource SfxSource;
    public AudioClip SfxDestruction; // one clip for everything
    public bool PlaySfxOnlyOnce = true;

    #endregion

    #region Camera Shake (optional)

    [Header("Camera Shake (optional)")]
    public bool ShakeOnDestruction = true;
    public float ShakeDuration = 2f;
    public float ShakeIntensity = 15f;

    #endregion

    #region Interaction Block (optional)

    [Header("Interaction Block (optional)")]
    public CanvasGroup BlockCanvasGroup;
    public bool FreezeTimeAfterSequence = false;

    #endregion

    #region Post GameOver Camera Sequence (optional)

    [Header("Post GameOver Camera Sequence (optional)")]
    [Tooltip("If enabled, runs AFTER ShowGameOverScreen().")]
    public bool EnablePostCameraSequence = false;

    [Tooltip("Camera to animate. If null, uses Camera.main.")]
    public Camera TargetCamera;

    [Header("FOV")]
    public bool CameraChangeFOV = true;
    public float CameraTargetFOV = 35f;
    [Tooltip("Higher = faster interpolation.")]
    public float CameraFOVLerpSpeed = 2.5f;

    [Header("Position (optional)")]
    public bool CameraMovePosition = false;
    public Vector3 CameraTargetPosition;
    [Tooltip("Higher = faster interpolation.")]
    public float CameraPositionLerpSpeed = 2.0f;

    [Header("Rotation (optional)")]
    public bool CameraRotate = false;
    public Vector3 CameraTargetEulerRotation;
    [Tooltip("Higher = faster interpolation.")]
    public float CameraRotationLerpSpeed = 2.0f;

    [Header("General")]
    [Tooltip("If true, snaps to exact target at the very end (can cause visible 'click' if not extremely close).")]
    public bool CameraSnapAtEnd = false;

    [Header("General")]
    [Tooltip("When true, camera interpolation uses unscaledDeltaTime (still runs if timeScale=0). Recommended.")]
    public bool CameraUseUnscaledTime = true;

    [Tooltip("Stop condition tolerance for ending the camera routine.")]
    public float CameraEndFovEpsilon = 0.05f;

    [Tooltip("Stop condition tolerance for ending the camera routine.")]
    public float CameraEndPosEpsilon = 0.01f;

    [Tooltip("Stop condition tolerance for ending the camera routine (degrees).")]
    public float CameraEndRotEpsilonDeg = 0.25f;

    [Tooltip("Safety timeout in seconds (unscaled) to avoid infinite loops if something is off.")]
    public float CameraMaxDuration = 6f;

    #endregion

    #region Internal State

    private bool _gameOverTriggered;
    private bool _updateEnabled;

    private bool _sfxPlayedOnce;
    private bool _shakePlayedOnce;

    private Coroutine _gameOverScaleRoutine;
    private Coroutine _postCameraRoutine;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        current = this;
    }

    private void Start()
    {
        StartCoroutine(EnableUpdateAfterDelay());

        // If screen is accidentally active in editor, keep it hidden by scale
        if (GameOverScreen != null && GameOverScreen.activeSelf)
        {
            GameOverScreen.transform.localScale = Vector3.zero;
        }
    }

    private IEnumerator EnableUpdateAfterDelay()
    {
        yield return new WaitForSeconds(UpdateStartDelay);
        _updateEnabled = true;
    }

    private void Update()
    {
        if (!_updateEnabled) return;
        if (!AutoCheckInUpdate) return;
        if (_gameOverTriggered) return;

        if (PlayerBaseController.current == null) return;

        int hp1 = PlayerBaseController.current.current_P1_Health;
        int hp2 = PlayerBaseController.current.current_P2_Health;

        if (hp1 <= LoseWhenHealthAtOrBelow)
        {
            TriggerGameOver("P1"); // P1 loses
        }
        else if (hp2 <= LoseWhenHealthAtOrBelow)
        {
            TriggerGameOver("P2"); // P2 loses
        }
    }

    #endregion

    #region Public Entry

    public void TriggerGameOver(string Player)
    {
        if (_gameOverTriggered) return;
        _gameOverTriggered = true;

        _sfxPlayedOnce = false;
        _shakePlayedOnce = false;

        if (BlockCanvasGroup != null)
        {
            BlockCanvasGroup.interactable = false;
            BlockCanvasGroup.blocksRaycasts = true;
        }

        StartCoroutine(GameOverSequence(Player));
    }

    #endregion

    #region GameOver Sequence

    private IEnumerator GameOverSequence(string loser)
    {
        if (StartDelay > 0f) yield return new WaitForSeconds(StartDelay);

        // 0) Turn Off Full Ray Cast Blocker
        RayCastBlocker.SetActive(false);

        // 1) Bases & Hands: ALWAYS disable both (independent of loser)
        BaseAndHandsDestructionAlways(loser);
        if (StepDelay > 0f) yield return new WaitForSeconds(StepDelay);

        // 2) Figure field: ONLY for loser
        FiguresDestructionLoserOnly(loser);
        if (StepDelay > 0f) yield return new WaitForSeconds(StepDelay);

        // 3) Artefact + Trap: ONLY for loser
        FieldTrapDestructionLoserOnly(loser);
        if (EndDelayBeforeUI > 0f) yield return new WaitForSeconds(EndDelayBeforeUI);

        // 4) UI (now includes text assignment)
        ShowGameOverScreen(loser);

        // 5) Optional post camera sequence (runs after GameOverScreen)
        if (EnablePostCameraSequence)
        {
            if (_postCameraRoutine != null)
            {
                StopCoroutine(_postCameraRoutine);
                _postCameraRoutine = null;
            }
            _postCameraRoutine = StartCoroutine(PostGameOverCameraSequence());
        }

        if (FreezeTimeAfterSequence)
        {
            Time.timeScale = 0f;
        }
    }

    /// <summary>
    /// Requirement:
    /// - Always disable BOTH bases and BOTH hands, regardless of who lost.
    /// - Still spawn VFX at the LOSER's effect position.
    /// - SFX + Shake: guarded (once per GameOver).
    /// </summary>
    private void BaseAndHandsDestructionAlways(string loser)
    {
        SafeSetActive(Player_Base_1, false);
        SafeSetActive(Player_Hand_1, false);
        SafeSetActive(Player_Base_2, false);
        SafeSetActive(Player_Hand_2, false);

        Transform anchor = null;
        if (loser == "P1")
        {
            anchor = Player_EffectPosition_1 != null ? Player_EffectPosition_1.transform : null;
        }
        else if (loser == "P2")
        {
            anchor = Player_EffectPosition_2 != null ? Player_EffectPosition_2.transform : null;
        }

        SpawnDestructionVFX(anchor);

        PlayOneShotGuarded(SfxDestruction);
        DoCameraShakeGuarded();
    }

    private void FiguresDestructionLoserOnly(string loser)
    {
        if (loser == "P1")
        {
            SafeSetActive(Figure_Field_1, false);
        }
        else if (loser == "P2")
        {
            SafeSetActive(Figure_Field_2, false);
        }
    }

    private void FieldTrapDestructionLoserOnly(string loser)
    {
        if (loser == "P1")
        {
            SafeSetActive(Artefact_Field_1, false);
            SafeSetActive(Trap_Field_1, false);
        }
        else if (loser == "P2")
        {
            SafeSetActive(Artefact_Field_2, false);
            SafeSetActive(Trap_Field_2, false);
        }
    }

    #endregion

    #region GameOver Screen

    // NEW: loser passed in so we can set main/sub text correctly
    public void ShowGameOverScreen(string loser)
    {
        if (GameOverScreen == null) return;

        // Set texts BEFORE showing / scaling (so it’s already correct on first frame)
        ApplyGameOverTexts(loser);

        if (_gameOverScaleRoutine != null)
        {
            StopCoroutine(_gameOverScaleRoutine);
            _gameOverScaleRoutine = null;
        }

        GameOverScreen.SetActive(true);

        if (!AnimateGameOverScale)
        {
            GameOverScreen.transform.localScale = GameOverTargetScale;
            return;
        }

        GameOverScreen.transform.localScale = Vector3.zero;
        _gameOverScaleRoutine = StartCoroutine(AnimateScale(
            GameOverScreen.transform,
            Vector3.zero,
            GameOverTargetScale,
            GameOverScaleStartDelay,
            GameOverScaleDuration,
            GameOverScaleEase
        ));
    }

    private void ApplyGameOverTexts(string loser)
    {
        // Winner = the other player
        bool p1Wins = (loser == "P2");

        if (MainText != null)
            MainText.text = p1Wins ? P1Win_Main : P1Lose_Main;

        if (SubText != null)
            SubText.text = p1Wins ? P1Win_Sub : P1Lose_Sub;
    }

    private IEnumerator AnimateScale(Transform t, Vector3 from, Vector3 to, float delay, float duration, EaseMode ease)
    {
        if (t == null) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (duration <= 0f)
        {
            t.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float e = ApplyEase(u, ease);

            t.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }

        t.localScale = to;
    }

    private float ApplyEase(float t, EaseMode ease)
    {
        switch (ease)
        {
            case EaseMode.Linear:
                return t;

            case EaseMode.SmoothStep:
                return t * t * (3f - 2f * t);

            case EaseMode.EaseOutBack:
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float x = t - 1f;
                return 1f + c3 * (x * x * x) + c1 * (x * x);

            default:
                return t;
        }
    }

    #endregion

    #region Post GameOver Camera Sequence

    private IEnumerator PostGameOverCameraSequence()
    {
        Camera cam = TargetCamera != null ? TargetCamera : Camera.main;
        if (cam == null) yield break;

        float startFov = cam.fieldOfView;
        Vector3 startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;

        float targetFov = CameraChangeFOV ? CameraTargetFOV : startFov;
        Vector3 targetPos = CameraMovePosition ? CameraTargetPosition : startPos;
        Quaternion targetRot = CameraRotate ? Quaternion.Euler(CameraTargetEulerRotation) : startRot;

        if (!CameraChangeFOV && !CameraMovePosition && !CameraRotate) yield break;

        float elapsed = 0f;
        bool timedOut = false;

        while (true)
        {
            float dt = CameraUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            if (CameraChangeFOV)
            {
                cam.fieldOfView = Mathf.Lerp(
                    cam.fieldOfView,
                    targetFov,
                    1f - Mathf.Exp(-CameraFOVLerpSpeed * dt)
                );
            }

            if (CameraMovePosition)
            {
                cam.transform.position = Vector3.Lerp(
                    cam.transform.position,
                    targetPos,
                    1f - Mathf.Exp(-CameraPositionLerpSpeed * dt)
                );
            }

            if (CameraRotate)
            {
                cam.transform.rotation = Quaternion.Slerp(
                    cam.transform.rotation,
                    targetRot,
                    1f - Mathf.Exp(-CameraRotationLerpSpeed * dt)
                );
            }

            bool fovDone = !CameraChangeFOV || Mathf.Abs(cam.fieldOfView - targetFov) <= CameraEndFovEpsilon;
            bool posDone = !CameraMovePosition || Vector3.Distance(cam.transform.position, targetPos) <= CameraEndPosEpsilon;
            bool rotDone = !CameraRotate || Quaternion.Angle(cam.transform.rotation, targetRot) <= CameraEndRotEpsilonDeg;

            if (fovDone && posDone && rotDone)
                break;

            if (CameraMaxDuration > 0f && elapsed >= CameraMaxDuration)
            {
                timedOut = true;
                break;
            }

            yield return null;
        }

        if (!CameraSnapAtEnd) yield break;
        if (timedOut) yield break;

        bool fovClose = !CameraChangeFOV || Mathf.Abs(cam.fieldOfView - targetFov) <= CameraEndFovEpsilon;
        bool posClose = !CameraMovePosition || Vector3.Distance(cam.transform.position, targetPos) <= CameraEndPosEpsilon;
        bool rotClose = !CameraRotate || Quaternion.Angle(cam.transform.rotation, targetRot) <= CameraEndRotEpsilonDeg;

        if (fovClose && posClose && rotClose)
        {
            if (CameraChangeFOV) cam.fieldOfView = targetFov;
            if (CameraMovePosition) cam.transform.position = targetPos;
            if (CameraRotate) cam.transform.rotation = targetRot;
        }
    }

    #endregion

    #region Helpers (VFX / SFX / Shake / SafeSetActive)

    private void SafeSetActive(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf == active) return;
        go.SetActive(active);
    }

    private void PlayOneShotGuarded(AudioClip clip)
    {
        if (clip == null) return;
        if (SfxSource == null) return;

        if (PlaySfxOnlyOnce)
        {
            if (_sfxPlayedOnce) return;
            _sfxPlayedOnce = true;
        }

        SfxSource.PlayOneShot(clip);
    }

    private void DoCameraShakeGuarded()
    {
        if (!ShakeOnDestruction) return;
        if (CameraShake.current == null) return;

        if (_shakePlayedOnce) return;
        _shakePlayedOnce = true;

        CameraShake.current.Shake(ShakeDuration, ShakeIntensity);
    }

    private void SpawnDestructionVFX(Transform spawnAt)
    {
        if (DestructionParticlePrefab == null) return;

        Transform t = spawnAt != null ? spawnAt : transform;

        Vector3 pos = t.position + ParticleOffset;
        Quaternion rot = t.rotation * Quaternion.Euler(ParticleRotationEuler);

        GameObject fx = Instantiate(DestructionParticlePrefab, pos, rot);
        fx.transform.localScale = ParticleScale;

        if (!DestroyParticleAfterPlay) return;

        float lifetime = ParticleFallbackLifetime;
        var ps = fx.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            lifetime = Mathf.Max(0.1f, ps.main.duration + ps.main.startLifetime.constantMax);
        }

        Destroy(fx, lifetime);
    }

    #endregion
}
