using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpecialMoveIntroLauncher : MonoBehaviour
{
    public static SpecialMoveIntroLauncher Instance;

    [Header("Cam-Object")]
    public GameObject camObject;

    [Header("Cam-Bewegungsparameter (1. Fahrt)")]
    public float moveDistance = 5f;
    public float moveDuration = 2f;
    public LeanTweenType easeType = LeanTweenType.easeOutQuad;


    // TODO: Doesn't really work yet.
    [Header("Cam-Zweifahrt (langsamer Drift nach der 1. Bewegung)")]
    [Tooltip("Zusätzliche Distanz der 2. Fahrt. 0 = keine zweite Fahrt.")]
    public float secondMoveDistance = 2.5f;
    [Tooltip("Dauer der 2. Bewegung (langsamer Drift).")]
    public float secondMoveDuration = 3.0f;
    public LeanTweenType secondMoveEase = LeanTweenType.linear;

    [Header("Figure-Hauptrotation (nur 3D-Model)")]
    public GameObject defaultTarget;         // = TurnManager.current.current_figure_P1
    public Vector3 defaultAxis = Vector3.up; // Achse der Hauptrotation
    public float defaultAngle = 90f;         // Winkel der Hauptrotation (hin)
    public float defaultDuration = 1f;       // Dauer der Hauptrotation

    [Header("Rückkehr zur Ausgangslage (nach Hold)")]
    [Tooltip("Komplette Rückrotation zur Ausgangslage (alle Achsen).")]
    public float returnDuration = 0.8f;
    public LeanTweenType returnEase = LeanTweenType.easeInOutSine;

    [Header("Figure Pre-Tilt (Y-Schwenk)")]
    [Tooltip("Yaw-Schwenk (Y) vor der Hauptrotation. Wird während der Hauptrotation wieder auf 0 zurückgeführt.")]
    public float preTiltAngleY = 12f;
    public float preTiltTime = 0.2f;
    [Tooltip("Wann der Pre-Tilt relativ zum Rotationsstart beginnt.")]
    public float preTiltStartDelay = 0.0f;
    public LeanTweenType preTiltEase = LeanTweenType.easeInOutSine;

    [Header("Standbild nach Drehung")]
    [Tooltip("Zeit in Sekunden, wie lange nach der Rotation gehalten wird, bevor Reset greift.")]
    public float holdAfterRotate = 0.6f;

    [Header("Rotation Timing (ab Intro-Start)")]
    [Tooltip("Wann die Figur-Rotation relativ zum Aufruf von MoveSpecialIntroCam starten soll.")]
    public float rotationStartAfterIntro = 0.35f;
    private LTDescr rotationDelayDescr; // Handle zum Abbrechen des Rotations-Delays

    [Header("Space-Modus")]
    [Tooltip("Auto: UI (RectTransform) -> anchoredPosition, sonst World. Wenn aus, nutze 'useLocalSpace'.")]
    public bool autoDetectUISpace = true;
    [Tooltip("Wenn Auto aus ist: true = LocalSpace, false = WorldSpace.")]
    public bool useLocalSpace = false;

    [Header("Intro-Fill-Image (optional)")]
    [Tooltip("UI-Image (Type=Filled, Method=Horizontal). Startfill = 0.")]
    public Image introFillImage;
    [Tooltip("Wann das Füllen relativ zum Intro-Start losgeht.")]
    public float fillStartDelay = 0.0f;
    [Tooltip("Fill-Geschwindigkeit in Einheiten/Sekunde (0..1/s). Beispiel: 2.0 füllt in ~0.5 s.")]
    public float fillSpeed = 1.5f;
    [Tooltip("Ziel-Füllstand (0..1). Normal: 1.0")]
    [Range(0f, 1f)] public float fillTarget = 1f;
    [Tooltip("Ease-Kurve für den Fill-Value.")]
    public LeanTweenType fillEase = LeanTweenType.linear;

    // ---- interne Zustände für Figure-Rotation (nur 3D-Model!) ----
    private Transform targetModel;           // == defaultTarget.transform.Find("Model/3D-Model")
    private Quaternion modelStartRot;
    private bool hasModelStartRot = false;

    private GameObject lastTargetRef;
    private bool isPlaying = false;

    // ---- Spark + Fill Handles ----
    private GameObject sparkGO;
    private bool sparkInitialActive = false;
    private float imageStartFillAmount = 0f;
    private bool hasImageStartFill = false;
    private LTDescr fillDelayDescr;
    private LTDescr fillTweenDescr;

    // ---- Camera Anchor Cache (drift-sicher) ----
    private struct CamAnchor
    {
        public bool isUI;
        public Vector2 anchoredPos;   // falls UI
        public Vector3 localPos;      // falls 3D
        public Quaternion localRot;   // falls 3D
        public Vector3 worldPos;      // Info
        public Quaternion worldRot;   // Info
    }
    private readonly Dictionary<int, CamAnchor> _camAnchors = new(); // key: camObject.GetInstanceID()

    private void Awake()
    {
        Instance = this;
        PrepareIntroFillImage(); // falls im Inspector gesetzt
    }

    // ======================================================================
    // ===============          PUBLIC ENTRY POINT            ===============
    // ======================================================================

    /// <summary>
    /// Startet die Intro-Sequenz: Cam raus (+ optional 2. Drift) -> (zeitgesteuert) Y-PreTilt → Hauptrotation → Hold → Reset.
    /// </summary>
    public void MoveSpecialIntroCam()
    {
        // 1) aktuelle Figur + deren Child-Objekte ermitteln
        if (TurnManager.current == null || 
        (TurnManager.current.current_figure_P1 == null && TurnManager.current.activePlayer == "P1") || 
        (TurnManager.current.current_figure_P2 == null && TurnManager.current.activePlayer == "P2"))
        {
            Debug.LogWarning("[SpecialMoveIntroLauncher] Kein current_figure_P1/P2 vorhanden.");
            return;
        }

        if (TurnManager.current.activePlayer == "P1"){defaultTarget = TurnManager.current.current_figure_P1;}
        if (TurnManager.current.activePlayer == "P2"){defaultTarget = TurnManager.current.current_figure_P2;}

        // Nur das 3D-Model rotieren
        targetModel = defaultTarget.transform.Find("Model/3D-Model");
        if (targetModel == null)
        {
            Debug.LogWarning("[SpecialMoveIntroLauncher] 'Model/3D-Model' wurde nicht gefunden.");
            return;
        }

        // Kamera & Spark als Geschwister unter 'Model'
        Transform modelRoot = defaultTarget.transform.Find("Model");
        if (modelRoot == null)
        {
            Debug.LogWarning("[SpecialMoveIntroLauncher] 'Model' wurde nicht gefunden.");
            return;
        }

        camObject = modelRoot.Find("SpecialMoveIntro-Camera")?.gameObject;
        if (camObject == null)
        {
            Debug.LogWarning("[SpecialMoveIntroLauncher] Keine 'SpecialMoveIntro-Camera' gefunden.");
            return;
        }

        sparkGO = modelRoot.Find("SpecialMoveIntro-Spark")?.gameObject;
        introFillImage = sparkGO ? sparkGO.GetComponent<Image>() : null;

        // Zielwechsel -> Model-Startrot neu erlauben
        if (lastTargetRef != defaultTarget)
        {
            hasModelStartRot = false;
            lastTargetRef = defaultTarget;
        }

        // 2) Kamera-Anker einmalig einfrieren (nie überschreiben) & sofort zum Anker snappen
        EnsureCamAnchor(camObject);
        SnapCamToAnchorImmediate(camObject);

        // 3) Spark aktivieren und Initialzustände sichern
        if (sparkGO != null)
        {
            sparkInitialActive = sparkGO.activeSelf;
            sparkGO.SetActive(true);
        }

        // 4) Fill vorbereiten
        PrepareIntroFillImage();
        CacheImageStartState();
        ScheduleImageFill();

        // 5) Flag + Cam-Objekt aktivieren
        if (isPlaying) return;
        isPlaying = true;
        camObject.SetActive(true);

        // 6) Clean cancels
        LeanTween.cancel(camObject);
        if (targetModel) LeanTween.cancel(targetModel.gameObject);
        if (rotationDelayDescr != null)
        {
            LeanTween.cancel(rotationDelayDescr.uniqueId);
            rotationDelayDescr = null;
        }

        // 7) Rotation zeitgesteuert (unabhängig von Cam-Fahrten)
        rotationDelayDescr = LeanTween.delayedCall(gameObject, Mathf.Max(0f, rotationStartAfterIntro), () =>
        {
            StartFigureRotateSequence();
        });

        // 8) 1. + 2. Kamerafahrt via SEQUENCE
        var anchor = _camAnchors[camObject.GetInstanceID()];
        bool isUI = anchor.isUI;
        if (!autoDetectUISpace) isUI = false;

        if (isUI)
        {
            var rect = camObject.GetComponent<RectTransform>();
            if (rect == null) StartCamSequence3D(anchor);
            else StartCamSequenceUI(rect, anchor);
        }
        else
        {
            StartCamSequence3D(anchor);
        }
    }

    // ======================================================================
    // ===============             CORE ACTIONS               ===============
    // ======================================================================

    /// <summary>
    /// Y-PreTilt (mit StartDelay) → Hauptrotation (gleichzeitig Y zurück) → Hold → kompletter Reset zur Startrotation.
    /// </summary>
    private void StartFigureRotateSequence()
    {
        if (targetModel == null)
        {
            HoldThenInstantReset();
            return;
        }

        LeanTween.cancel(targetModel.gameObject);

        if (!hasModelStartRot)
        {
            modelStartRot = targetModel.rotation;
            hasModelStartRot = true;
        }

        Quaternion startRot = modelStartRot;

        // ---------- Phase A: Pre-Tilt (Y-Schwenk) mit Delay ----------
        LeanTween.delayedCall(targetModel.gameObject, Mathf.Max(0f, preTiltStartDelay), () =>
        {
            LeanTween.value(targetModel.gameObject, 0f, preTiltAngleY, Mathf.Max(0.0001f, preTiltTime))
                .setEase(preTiltEase)
                .setOnUpdate((float y) =>
                {
                    // Nur Y-Schwenk aufstarten, keine Hauptrotation in Phase A
                    Quaternion yRot = Quaternion.Euler(0f, y, 0f);
                    targetModel.rotation = startRot * yRot;
                })
                .setOnComplete(() =>
                {
                    // ---------- Phase B: Hauptrotation + gleichzeitige Y-Rückführung ----------
                    LeanTween.value(targetModel.gameObject, 0f, 1f, Mathf.Max(0.0001f, defaultDuration))
                        .setEase(LeanTweenType.easeInOutQuad)
                        .setOnUpdate((float v) =>
                        {
                            float mainAngle = Mathf.Lerp(0f, defaultAngle, v);  // Hauptrotation fortschreiten
                            float y = Mathf.Lerp(preTiltAngleY, 0f, v);         // Y wieder auf 0 zurück
                            Quaternion yRot = Quaternion.Euler(0f, y, 0f);
                            Quaternion mainRot = Quaternion.AngleAxis(mainAngle, defaultAxis);

                            // Reihenfolge: erst Y-PreTilt, dann Hauptrotation — alles relativ zu startRot
                            targetModel.rotation = startRot * yRot * mainRot;
                        })
                        .setOnComplete(() =>
                        {
                            // ---------- Phase C: Hold + vollständiger Reset auf Start ----------
                            HoldThenInstantReset();
                        });
                });
        });
    }

    // ---------- Kamerafahrten: Sequenzen (ohne Rotations-Append!) ----------

    private void StartCamSequenceUI(RectTransform rect, CamAnchor anchor)
    {
        if (!rect) return;

        rect.anchoredPosition = anchor.anchoredPos;

        Vector2 end1 = anchor.anchoredPos + new Vector2(moveDistance, 0f);
        Vector2 end2 = end1 + new Vector2(secondMoveDistance, 0f);

        var seq = LeanTween.sequence();
        seq.append(LeanTween.move(rect, end1, Mathf.Max(0.0001f, moveDuration)).setEase(easeType));

        if (secondMoveDistance > 0f && secondMoveDuration > 0f)
            seq.append(LeanTween.move(rect, end2, Mathf.Max(0.0001f, secondMoveDuration)).setEase(secondMoveEase));

        // KEINE Rotation hier anhängen – sie startet zeitgesteuert ab Intro-Start.
    }

    private void StartCamSequence3D(CamAnchor anchor)
    {
        if (useLocalSpace)
        {
            camObject.transform.localPosition = anchor.localPos;
            camObject.transform.localRotation = anchor.localRot;

            Vector3 end1 = anchor.localPos + Vector3.right * moveDistance;
            Vector3 end2 = end1 + Vector3.right * secondMoveDistance;

            var seq = LeanTween.sequence();
            seq.append(LeanTween.moveLocal(camObject, end1, Mathf.Max(0.0001f, moveDuration)).setEase(easeType));

            if (secondMoveDistance > 0f && secondMoveDuration > 0f)
                seq.append(LeanTween.moveLocal(camObject, end2, Mathf.Max(0.0001f, secondMoveDuration)).setEase(secondMoveEase));
        }
        else
        {
            camObject.transform.position = anchor.worldPos;
            camObject.transform.rotation = anchor.worldRot;

            Vector3 end1 = anchor.worldPos + Vector3.right * moveDistance;
            Vector3 end2 = end1 + Vector3.right * secondMoveDistance;

            var seq = LeanTween.sequence();
            seq.append(LeanTween.move(camObject, end1, Mathf.Max(0.0001f, moveDuration)).setEase(easeType));

            if (secondMoveDistance > 0f && secondMoveDuration > 0f)
                seq.append(LeanTween.move(camObject, end2, Mathf.Max(0.0001f, secondMoveDuration)).setEase(secondMoveEase));
        }
    }

    private void HoldThenInstantReset()
    {
        float delay = Mathf.Max(0f, holdAfterRotate);
        LeanTween.delayedCall(gameObject, delay, () =>
        {
            // Alles stoppen (inkl. möglichem 2. Drift)
            LeanTween.cancel(camObject);
            if (targetModel) LeanTween.cancel(targetModel.gameObject);

            if (rotationDelayDescr != null)
            {
                LeanTween.cancel(rotationDelayDescr.uniqueId);
                rotationDelayDescr = null;
            }

            CancelImageFillTween();

            // Cam zurück auf ANKER
            SnapCamToAnchorImmediate(camObject);

            // 3D-Model vollständig zurück auf Startrotation (alle Achsen)
            if (targetModel != null && hasModelStartRot)
                targetModel.rotation = modelStartRot;

            // Spark auf Ursprungszustand
            if (sparkGO != null) sparkGO.SetActive(sparkInitialActive);

            if (camObject) camObject.SetActive(false);
            isPlaying = false;
        });
    }

    // ======================================================================
    // ===============          CAMERA ANCHOR LOGIK           ===============
    // ======================================================================

    private void EnsureCamAnchor(GameObject cam)
    {
        int id = cam.GetInstanceID();
        if (_camAnchors.ContainsKey(id)) return;

        CamAnchor anchor = new CamAnchor();
        RectTransform rt = cam.GetComponent<RectTransform>();

        if (autoDetectUISpace && rt != null)
        {
            anchor.isUI = true;
            anchor.anchoredPos = rt.anchoredPosition;
            anchor.localPos = cam.transform.localPosition;
            anchor.localRot = cam.transform.localRotation;
            anchor.worldPos = cam.transform.position;
            anchor.worldRot = cam.transform.rotation;
        }
        else
        {
            anchor.isUI = false;
            anchor.localPos = cam.transform.localPosition;
            anchor.localRot = cam.transform.localRotation;
            anchor.worldPos = cam.transform.position;
            anchor.worldRot = cam.transform.rotation;
        }

        _camAnchors.Add(id, anchor);
    }

    private void SnapCamToAnchorImmediate(GameObject cam)
    {
        if (cam == null) return;
        int id = cam.GetInstanceID();
        if (!_camAnchors.TryGetValue(id, out var anchor)) return;

        if (anchor.isUI)
        {
            var rt = cam.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = anchor.anchoredPos;
        }
        else
        {
            if (useLocalSpace)
            {
                cam.transform.localPosition = anchor.localPos;
                cam.transform.localRotation = anchor.localRot;
            }
            else
            {
                cam.transform.position = anchor.worldPos;
                cam.transform.rotation = anchor.worldRot;
            }
        }
    }

    // ======================================================================
    // ===============         FILL / SPARK HANDLING          ===============
    // ======================================================================

    private void PrepareIntroFillImage()
    {
        if (introFillImage == null) return;

        introFillImage.type = Image.Type.Filled;
        introFillImage.fillMethod = Image.FillMethod.Horizontal;
        introFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        introFillImage.fillClockwise = true;

        introFillImage.fillAmount = 0f;
        hasImageStartFill = false;
    }

    private void CacheImageStartState()
    {
        if (introFillImage == null) return;
        if (!hasImageStartFill)
        {
            imageStartFillAmount = introFillImage.fillAmount; // sollte 0 sein
            hasImageStartFill = true;
        }
        introFillImage.fillAmount = 0f;
    }

    private void ScheduleImageFill()
    {
        if (introFillImage == null) return;

        float delay = Mathf.Max(0f, fillStartDelay);

        if (fillDelayDescr != null)
        {
            LeanTween.cancel(fillDelayDescr.uniqueId);
            fillDelayDescr = null;
        }

        fillDelayDescr = LeanTween.delayedCall(gameObject, delay, () =>
        {
            StartImageFillTween();
        });
    }

    private void StartImageFillTween()
    {
        if (introFillImage == null) return;

        if (fillTweenDescr != null)
        {
            LeanTween.cancel(fillTweenDescr.uniqueId);
            fillTweenDescr = null;
        }
        LeanTween.cancel(introFillImage.gameObject);

        float current = introFillImage.fillAmount;
        float target = Mathf.Clamp01(fillTarget);
        float delta = Mathf.Max(0.0001f, Mathf.Abs(target - current));
        float dur = Mathf.Max(0.0001f, delta / Mathf.Max(0.0001f, fillSpeed));

        if (Mathf.Approximately(delta, 0f)) return;

        fillTweenDescr = LeanTween.value(introFillImage.gameObject, current, target, dur)
            .setEase(fillEase)
            .setOnUpdate(v =>
            {
                if (introFillImage != null)
                    introFillImage.fillAmount = v;
            })
            .setOnComplete(() =>
            {
                fillTweenDescr = null;
            });
    }

    private void CancelImageFillTween()
    {
        if (fillTweenDescr != null)
        {
            LeanTween.cancel(fillTweenDescr.uniqueId);
            fillTweenDescr = null;
        }
        if (fillDelayDescr != null)
        {
            LeanTween.cancel(fillDelayDescr.uniqueId);
            fillDelayDescr = null;
        }
        if (introFillImage != null)
            LeanTween.cancel(introFillImage.gameObject);
    }

    private void ResetImageToStart()
    {
        if (introFillImage == null) return;
        introFillImage.fillAmount = hasImageStartFill ? imageStartFillAmount : 0f;
    }

    public void TriggerImageFillNow()
    {
        StartImageFillTween();
    }

    // ======================================================================
    // ===============         ABRÄUMEN / FAILSAFE            ===============
    // ======================================================================

    public void ForceResetNow()
    {
        if(camObject != null){LeanTween.cancel(camObject);}
        if (targetModel) LeanTween.cancel(targetModel.gameObject);

        if (rotationDelayDescr != null)
        {
            LeanTween.cancel(rotationDelayDescr.uniqueId);
            rotationDelayDescr = null;
        }

        CancelImageFillTween();

        SnapCamToAnchorImmediate(camObject);

        if (targetModel != null && hasModelStartRot)
            targetModel.rotation = modelStartRot;

        if (sparkGO != null) sparkGO.SetActive(sparkInitialActive);
        if (camObject) camObject.SetActive(false);
        isPlaying = false;
    }

    private void OnDisable()
    {
        ForceResetNow();
    }
}
