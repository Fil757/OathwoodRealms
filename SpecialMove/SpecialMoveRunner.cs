using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpecialMoveRunner : MonoBehaviour
{
    #region Kontext vom Controller (pro Instanz gesetzt)

    public SpecialMoveController.ActiveSide activeSide;
    public SpecialMove MoveToPlay;

    public GameObject[] Figures_P1;
    public GameObject[] Figures_P2;

    public GameObject P1_Effect_Position;
    public GameObject P2_Effect_Position;
    public GameObject Central_Effect_Position;

    public GameObject Player1;
    public GameObject Player2;

    public Transform PointZero;
    public CameraShake cameraShake;

    public float euleroffset_multiplikator_x;
    public float euleroffset_multiplikator_y;
    public float euleroffset_multiplikator_z;

    public float euleroffset_extraoffset_x;
    public float euleroffset_extraoffset_y;
    public float euleroffset_extraoffset_z;

    // Callback für genau diese Instanz
    public System.Action OnFinished;

    #endregion

    #region Ongoing SpecialMoves Marker (NEU)

    [Header("Ongoing SpecialMoves Tracking (Marker)")]
    [Tooltip("Wenn gesetzt: Marker wird als Child hiervon angelegt. Wenn leer: versucht er 'OngoingSpecialMoves' per Name zu finden.")]
    public Transform OngoingSpecialMovesRoot;

    private GameObject _ongoingMarker;

    #endregion

    #region Laufzeit-State (war vorher im Controller global)

    private bool _lastAnimExecutedAnything;

    // Ausgangs-WELT-Positionen für DefaultFigurePosition (Partikel)
    private readonly Dictionary<int, Vector3> _defaultFigureWorldPos = new();

    // Ausgangs-LOKALE Transforms (für BackToDefault)
    private struct LocalTRS { public Vector3 pos; public Quaternion rot; public Vector3 scale; }
    private readonly Dictionary<int, LocalTRS> _figureDefaultLocalTRS = new();

    #endregion

    #region Public API

    public void Begin()
    {
        CreateOngoingMarker();
        StartCoroutine(PlaySpecialMoveCoroutine());
    }

    #endregion

    #region Ongoing Marker Helpers (NEU)

    private void CreateOngoingMarker()
    {
        if (_ongoingMarker != null) return;

        Transform root = OngoingSpecialMovesRoot;

        if (root == null)
        {
            var go = GameObject.Find("OngoingSpecialMoves");
            if (go) root = go.transform;
        }

        if (root == null)
        {
            Debug.LogWarning("[SMRunner] OngoingSpecialMoves Root nicht gefunden. Kein Marker wird erstellt.");
            return;
        }

        string moveName = (MoveToPlay != null) ? MoveToPlay.name : "UnknownMove";
        _ongoingMarker = new GameObject($"SpecialMove_{activeSide}_{moveName}_{GetInstanceID()}");
        _ongoingMarker.transform.SetParent(root, false);
    }

    private void DestroyOngoingMarker()
    {
        if (_ongoingMarker != null)
        {
            Destroy(_ongoingMarker);
            _ongoingMarker = null;
        }
    }

    private void OnDestroy()
    {
        // Safety-Net: falls Runner extern zerstört wird, bleibt kein Marker hängen
        DestroyOngoingMarker();
    }

    #endregion

    #region Kern-Pipeline

    private IEnumerator PlaySpecialMoveCoroutine()
    {
        var move = MoveToPlay;

        if (move == null || move.Stats == null || move.Stats.Length == 0)
        {
            DestroyOngoingMarker();
            OnFinished?.Invoke();
            Destroy(gameObject);
            yield break;
        }

        // Session vorbereiten (Cache Default-TRS aller Figuren)
        BeginSpecialMoveSession();

        for (int i = 0; i < move.Stats.Length; i++)
        {
            SpecialMoveStats item = move.Stats[i];
            RefreshFiguresFromScene_SM();

            // 1) Zielauflösung (für Stats)
            bool canExecute = ResolveStatTargets_Sided(
                item,
                out GameObject targetedFigure,
                out GameObject targetedPlayer,
                out GameObject[] figuresTargetedArray);

            // 2) Draw-Cards läuft immer
            if (item.SPECIALMOVE_POKERCARDS_DRAWING > 0)
                SM_DrawPokerCards(targetedPlayer, item.SPECIALMOVE_POKERCARDS_DRAWING);
            if (item.SPECIALMOVE_DECKCARDS_DRAWING > 0)
                SM_DrawDeckCards(item.SPECIALMOVE_DECKCARDS_DRAWING);

            // 3) Stats anwenden
            if (canExecute)
                ApplyStatChanges(item, targetedFigure, targetedPlayer);

            // 4) Animationen
            yield return StartCoroutine(PlayAnimationsForItem_Sided(item));
            bool animsDidRun = _lastAnimExecutedAnything;

            // 5) Gap
            if ((canExecute || animsDidRun) && item.TIME_GAP_TO_NEXT > 0f)
                yield return new WaitForSeconds(item.TIME_GAP_TO_NEXT);
        }

        DestroyOngoingMarker();
        OnFinished?.Invoke();

        Destroy(gameObject);
    }

    #endregion

    #region Side-/Target-Resolver

    private GameObject[] Figures_Self() => activeSide == SpecialMoveController.ActiveSide.P1 ? Figures_P1 : Figures_P2;
    private GameObject[] Figures_Opponent() => activeSide == SpecialMoveController.ActiveSide.P1 ? Figures_P2 : Figures_P1;

    private GameObject Player_Self() => activeSide == SpecialMoveController.ActiveSide.P1 ? Player1 : Player2;
    private GameObject Player_Opponent() => activeSide == SpecialMoveController.ActiveSide.P1 ? Player2 : Player1;

    private GameObject AnchorFor(TargetedPlayer tp)
    {
        switch (tp)
        {
            case TargetedPlayer.Self: return activeSide == SpecialMoveController.ActiveSide.P1 ? P1_Effect_Position : P2_Effect_Position;
            case TargetedPlayer.Opponent: return activeSide == SpecialMoveController.ActiveSide.P1 ? P2_Effect_Position : P1_Effect_Position;
            default: return Central_Effect_Position;
        }
    }

    private bool ResolveStatTargets_Sided(
        SpecialMoveStats item,
        out GameObject targetedFigure,
        out GameObject targetedPlayer,
        out GameObject[] figuresTargetedArray)
    {
        targetedFigure = null;
        targetedPlayer = null;
        figuresTargetedArray = null;

        var sign = item.SPECIALMOVE_TARGET_SIGN;
        var targetPlayer = item.SPECIALMOVE_TARGET_PLAYER;

        // Seite / Spieler bestimmen
        GameObject[] figures = null;
        switch (targetPlayer)
        {
            case TargetedPlayer.Self:
                figures = Figures_Self();
                targetedPlayer = Player_Self();
                break;
            case TargetedPlayer.Opponent:
                figures = Figures_Opponent();
                targetedPlayer = Player_Opponent();
                break;
            default:
                figures = null;
                break;
        }
        figuresTargetedArray = figures;

        // Prüfen ob Figur benötigt wird
        bool affectsFigure =
            item.SPECIALMOVE_CHANGE_STATS_INTEGER == ChangedIntegerStats.Figure_Health ||
            item.SPECIALMOVE_CHANGE_STATS_INTEGER == ChangedIntegerStats.Figure_Load ||
            item.SPECIALMOVE_CHANGE_STATS_INTEGER == ChangedIntegerStats.Attack ||
            item.SPECIALMOVE_CHANGE_STATS_INTEGER == ChangedIntegerStats.Defense ||
            item.SPECIALMOVE_CHANGE_STATS_INTEGER == ChangedIntegerStats.Cost ||
            item.SPECIALMOVE_CHANGE_STATS_STRING == ChangedStringStats.Target_Sign ||
            item.SPECIALMOVE_CHANGE_STATS_BOOL == ChangedBoolStats.Is_Defending ||
            item.SPECIALMOVE_CHANGE_STATS_BOOL == ChangedBoolStats.Gets_Killed;

        bool requiresFigure = affectsFigure && (sign != CardSign.None);

        // Figur anhand Sign suchen
        if (requiresFigure && figures != null)
        {
            string typeName = sign.ToString();
            for (int i = 0; i < figures.Length; i++)
            {
                var go = figures[i];
                if (!go) continue;
                var df = go.GetComponent<Display_Figure>();
                if (df && df.FIGURE_TYPE == typeName)
                {
                    targetedFigure = go;
                    break;
                }
            }
        }

        return !requiresFigure || targetedFigure != null;
    }

    private SpecialMoveController.ActiveSide DetectSideOfFigure(GameObject fig)
    {
        if (!fig) return activeSide; // Fallback

        if (Figures_P1 != null)
        {
            for (int i = 0; i < Figures_P1.Length; i++)
                if (Figures_P1[i] == fig) return SpecialMoveController.ActiveSide.P1;
        }

        if (Figures_P2 != null)
        {
            for (int i = 0; i < Figures_P2.Length; i++)
                if (Figures_P2[i] == fig) return SpecialMoveController.ActiveSide.P2;
        }

        // Wenn nicht gefunden (z.B. Arrays kurz null/alt): Runner-perspektive als Fallback
        return activeSide;
    }

    #endregion

    #region Stat-Anwendung

    private void ApplyStatChanges(SpecialMoveStats item, GameObject targetedFigure, GameObject targetedPlayer)
    {
        // Integer
        switch (item.SPECIALMOVE_CHANGE_STATS_INTEGER)
        {
            case ChangedIntegerStats.None:
                break;

            case ChangedIntegerStats.Figure_Health:
                if (item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE > 0)
                    SM_ApplyHealing(targetedFigure, item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE);
                else if (item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE < 0)
                    SM_ApplyDamage(targetedFigure, -item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE);
                break;

            case ChangedIntegerStats.Player_Health:
                if (item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE > 0)
                    SM_HealPlayer(targetedPlayer, item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE);
                else if (item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE < 0)
                    SM_DamagePlayer(targetedPlayer, -item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE);
                break;

            case ChangedIntegerStats.Figure_Load:
                SM_ApplyLoading(targetedFigure, item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE);
                break;

            case ChangedIntegerStats.Player_Load:
                SM_LoadPlayer(targetedPlayer, item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE);
                break;

            case ChangedIntegerStats.Attack:
                SM_ApplyStatChange(targetedFigure, item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE, "Attack");
                break;

            case ChangedIntegerStats.Defense:
                SM_ApplyStatChange(targetedFigure, item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE, "Defense");
                break;

            case ChangedIntegerStats.Cost:
                SM_ApplyStatChange(targetedFigure, item.SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE, "Cost");
                break;

            default:
                Debug.LogWarning("[SMRunner] Unknown ChangedIntegerStats.");
                break;
        }

        // String
        switch (item.SPECIALMOVE_CHANGE_STATS_STRING)
        {
            case ChangedStringStats.None:
                break;
            case ChangedStringStats.Target_Sign:
                SM_ChangeTargetSign(targetedFigure, item.SPECIALMOVE_CHANGE_STATS_STRING_CHANGE);
                break;
            default:
                Debug.LogWarning("[SMRunner] Unknown ChangedStringStats.");
                break;
        }

        // Bool
        switch (item.SPECIALMOVE_CHANGE_STATS_BOOL)
        {
            case ChangedBoolStats.None:
                break;
            case ChangedBoolStats.Is_Defending:
                SM_FigureDefending(targetedFigure, item.SPECIALMOVE_CHANGE_STATS_BOOL_CHANGE);
                break;
            case ChangedBoolStats.Gets_Killed:
                SM_GetsKilled(targetedFigure);
                break;
            default:
                Debug.LogWarning("[SMRunner] Unknown ChangedBoolStats.");
                break;
        }
    }

    #endregion

    #region Animationen / Effekte (Particles, Figure-Moves, CameraShake)

    private Vector3 MirrorOffsetForSide(Vector3 offset)
    {
        if (activeSide == SpecialMoveController.ActiveSide.P1) return offset;
        return new Vector3(-offset.x, -offset.y, offset.z); // X/Y invertieren für P2
    }

    private Vector3 MirrorOffsetForSide_Particle(Vector3 offset, bool mirrored_or_not)
    {
        if (!mirrored_or_not) return offset;
        if (activeSide == SpecialMoveController.ActiveSide.P1) return offset;
        return new Vector3(-offset.x, -offset.y, offset.z);
    }

    private Vector3 MirrorLocalEulerForSide(Vector3 euler)
    {
        return activeSide == SpecialMoveController.ActiveSide.P1
            ? euler
            : new Vector3(euler.x, euler.y, -euler.z);
    }

    private Vector3 MirrorLocalEulerForSide_Particle(Vector3 euler, bool mirrored_or_not)
    {
        if (!mirrored_or_not) return euler;
        if (activeSide == SpecialMoveController.ActiveSide.P1) return euler;
        return new Vector3(
            euleroffset_multiplikator_x * euler.x + euleroffset_extraoffset_x,
            euleroffset_multiplikator_y * euler.y + euleroffset_extraoffset_y,
            euleroffset_multiplikator_z * euler.z + euleroffset_extraoffset_z
        );
    }

    private Vector3 WorldFromPointZero(Vector3 offsetInPointZeroSpace)
    {
        var off = MirrorOffsetForSide(offsetInPointZeroSpace);
        if (PointZero) return PointZero.TransformPoint(off);
        return off; // Fallback: behandle Offset als Welt-Vektor
    }

    private IEnumerator PlayAnimationsForItem_Sided(SpecialMoveStats item)
    {
        _lastAnimExecutedAnything = false;
        _defaultFigureWorldPos.Clear();

        if (item.SpecialAnimations == null || item.SpecialAnimations.Length == 0)
            yield break;

        for (int m = 0; m < item.SpecialAnimations.Length; m++)
        {
            var anim = item.SpecialAnimations[m];

            ResolveAnimationTargets_Sided(
                anim,
                out GameObject targetEffectAnchor,
                out GameObject targetedFigureForAnim,
                out bool requiresFigureForAnim);

            bool executedThisAnim = false;
            bool figureAvailable = targetedFigureForAnim != null;

            // ------------------- Partikel -------------------
            if (anim.useParticle && anim.SPECIALMOVE_ANIMATION != null)
            {
                Transform anchorTr = targetedFigureForAnim ? GetFigureEffectAnchor(targetedFigureForAnim) : null;

                bool posMirror = anim.use_pos_mirror_effect_for_other_side;
                bool rotMirror = anim.use_rot_mirror_effect_for_other_side;

                Vector3 MPos(Vector3 v) => MirrorOffsetForSide_Particle(v, posMirror);
                Vector3 MEuler(Vector3 e) => MirrorLocalEulerForSide_Particle(e, rotMirror);

                (bool ok, Vector3 world, Transform parent) ResolveRoot(FigureParticleRoot root, Vector3 localOffset)
                {
                    if (root == FigureParticleRoot.Figure)
                    {
                        if (!anchorTr) return (false, Vector3.zero, null);
                        var w = anchorTr.TransformPoint(MPos(localOffset));
                        return (true, w, anchorTr);
                    }

                    bool ok2 = TryResolveParticleRootWorld_Sided(
                        root,
                        targetEffectAnchor,
                        targetedFigureForAnim,
                        anim.SPECIALMOVE_ANIMATION_TARGETED_PLAYER,
                        localOffset,
                        out Vector3 worldPos,
                        out Transform parentForSnap);
                    return (ok2, worldPos, parentForSnap);
                }

                var end = ResolveRoot(anim.SPECIALMOVE_PARTICLE_ROOT, anim.SPECIALMOVE_ANIMATION_POSITION);
                if (!end.ok)
                    goto Particles_End;

                if (anim.SPECIALMOVE_PARTICLE_MOVEMENT == ParticleMovementMode.Move)
                {
                    var start = ResolveRoot(anim.SPECIALMOVE_PARTICLE_FROM_ROOT, anim.SPECIALMOVE_PARTICLE_FROM_POSITION);
                    if (start.ok)
                    {
                        Particle_Animation_Move(
                            start.world,
                            end.world,
                            anim.SPECIALMOVE_ANIMATION,
                            anim.SPECIALMOVE_ANIMATION_SCALE_BY,
                            MEuler(anim.SPECIALMOVE_ANIMATION_ROTATION),
                            anim.SPECIALMOVE_PARTICLE_MOVE_TIME,
                            anim.SPECIALMOVE_PARTICLE_MOVE_TWEEN);

                        executedThisAnim = true;
                    }
                    goto Particles_End;
                }

                // SNAP
                if (anim.SPECIALMOVE_PARTICLE_ROOT == FigureParticleRoot.DefaultFigurePosition)
                {
                    Particle_Animation_World(
                        end.world,
                        anim.SPECIALMOVE_ANIMATION,
                        anim.SPECIALMOVE_ANIMATION_SCALE_BY,
                        MEuler(anim.SPECIALMOVE_ANIMATION_ROTATION),
                        null);
                    executedThisAnim = true;
                    goto Particles_End;
                }

                if (anim.SPECIALMOVE_PARTICLE_ROOT == FigureParticleRoot.Figure && anchorTr)
                {
                    Particle_Animation_Relative_FigureAnchor(
                        anchorTr,
                        anim.SPECIALMOVE_ANIMATION,
                        MPos(anim.SPECIALMOVE_ANIMATION_POSITION),
                        anim.SPECIALMOVE_ANIMATION_SCALE_BY,
                        MEuler(anim.SPECIALMOVE_ANIMATION_ROTATION));
                    executedThisAnim = true;
                    goto Particles_End;
                }

                if (end.parent != null)
                {
                    var localAtParent = end.parent.InverseTransformPoint(end.world);
                    Particle_Animation_Relative(
                        end.parent,
                        anim.SPECIALMOVE_ANIMATION,
                        localAtParent,
                        anim.SPECIALMOVE_ANIMATION_SCALE_BY,
                        MEuler(anim.SPECIALMOVE_ANIMATION_ROTATION));
                }
                else
                {
                    Particle_Animation_World(
                        end.world,
                        anim.SPECIALMOVE_ANIMATION,
                        anim.SPECIALMOVE_ANIMATION_SCALE_BY,
                        MEuler(anim.SPECIALMOVE_ANIMATION_ROTATION),
                        null);
                }
                executedThisAnim = true;

            Particles_End:
                if (executedThisAnim) _lastAnimExecutedAnything = true;
            }

            // ------------------- Figuren-Animationen -------------------
            if (anim.SPECIALMOVE_ANIMATION_FIGURE_ANIMATION != FigureAnimation.None && figureAvailable)
            {
                bool moved = StartCoroutine_FigureAnim_Sided(anim, targetedFigureForAnim);
                executedThisAnim |= moved;
            }

            // ------------------- Kamera/UI-Shake -------------------
            if (anim.SPECIALMOVE_USE_SHAKE)
            {
                bool allowShake = !requiresFigureForAnim || figureAvailable;
                if (allowShake)
                {
                    CameraShake(anim.SPECIALMOVE_SHAKE_INTENSITY, anim.SPECIALMOVE_SHAKE_DURATION);
                    executedThisAnim = true;
                }
            }

            // ------------------- Sound pro Animationsschritt -------------------
            if (anim.useSound && anim.SPECIALMOVE_SOUNDPROFILE != null && SoundProfileController.current != null)
            {
                bool allowSound = !requiresFigureForAnim || figureAvailable;

                if (allowSound)
                {
                    Transform attachTo = null;

                    if (anim.attachSoundToFigure && targetedFigureForAnim != null)
                    {
                        attachTo = targetedFigureForAnim.transform;
                    }
                    else if (targetEffectAnchor != null)
                    {
                        attachTo = targetEffectAnchor.transform;
                    }

                    if (attachTo != null)
                    {
                        SoundProfileController.current.PlaySound(anim.SPECIALMOVE_SOUNDPROFILE, attachTo, false);
                    }
                    else
                    {
                        SoundProfileController.current.PlaySound(anim.SPECIALMOVE_SOUNDPROFILE);
                    }
                }
            }

            if (executedThisAnim && anim.SPECIALMOVE_ANIMATION_TIMEGAP > 0f)
                yield return new WaitForSeconds(anim.SPECIALMOVE_ANIMATION_TIMEGAP);

            _lastAnimExecutedAnything |= executedThisAnim;
        }
    }

    private void ResolveAnimationTargets_Sided(
        SpecialMoveAnimations anim,
        out GameObject targetEffectAnchor,
        out GameObject targetedAnimFigure,
        out bool requiresFigure)
    {
        targetEffectAnchor = null;
        targetedAnimFigure = null;

        requiresFigure =
            (anim.SPECIALMOVE_ANIMATION_TARGETED_SIGN != CardSign.None) ||
            (anim.SPECIALMOVE_ANIMATION_FIGURE_ANIMATION != FigureAnimation.None);

        GameObject[] figures = null;
        switch (anim.SPECIALMOVE_ANIMATION_TARGETED_PLAYER)
        {
            case TargetedPlayer.Self: figures = Figures_Self(); break;
            case TargetedPlayer.Opponent: figures = Figures_Opponent(); break;
            default: break;
        }

        if (figures != null && anim.SPECIALMOVE_ANIMATION_TARGETED_SIGN != CardSign.None)
        {
            string typeName = anim.SPECIALMOVE_ANIMATION_TARGETED_SIGN.ToString();
            for (int i = 0; i < figures.Length; i++)
            {
                var go = figures[i];
                if (!go) continue;
                var df = go.GetComponent<Display_Figure>();
                if (df && df.FIGURE_TYPE == typeName)
                {
                    targetedAnimFigure = go;
                    break;
                }
            }
        }

        if (targetedAnimFigure)
        {
            var ep = targetedAnimFigure.transform.Find("EffectPosition");
            targetEffectAnchor = ep ? ep.gameObject : targetedAnimFigure;
        }
    }

    private bool StartCoroutine_FigureAnim_Sided(SpecialMoveAnimations anim, GameObject targetedFigureForAnim)
    {
        if (!targetedFigureForAnim) return false;

        EnsureDefaultLocalCached(targetedFigureForAnim.transform);

        switch (anim.SPECIALMOVE_ANIMATION_FIGURE_ANIMATION)
        {
            case FigureAnimation.None:
                return false;

            case FigureAnimation.Hover:
                StartCoroutine(Hover(targetedFigureForAnim,
                    anim.FIGURE_hoverDistance,
                    anim.FIGURE_hoverTime,
                    anim.FIGURE_hover_stayTime,
                    anim.FIGURE_hover_swayMagnitude,
                    anim.FIGURE_hover_swayTime));
                return true;

            case FigureAnimation.Stomp:
                StartCoroutine(Stomp(targetedFigureForAnim,
                    anim.FIGURE_stompHeight,
                    anim.FIGURE_stompTime));
                return true;

            case FigureAnimation.Attack:
                StartCoroutine(Attack_Sided(targetedFigureForAnim,
                    anim.FIGURE_attackDistance,
                    anim.FIGURE_attackTime));
                return true;

            case FigureAnimation.Shake:
                StartCoroutine(FigureShakeRoutine(targetedFigureForAnim,
                    anim.FIGURE_shakeMagnitude,
                    anim.FIGURE_shakeTime));
                return true;

            case FigureAnimation.CustomTransform:
            {
                Vector3 worldTarget = WorldFromPointZero(anim.FIGURE_ct_targetPosition);

                var parent = targetedFigureForAnim.transform.parent;
                Vector3 localTarget = parent ? parent.InverseTransformPoint(worldTarget) : worldTarget;

                Vector3 targetEulerLocal = MirrorLocalEulerForSide(anim.FIGURE_ct_targetEuler);

                StartCoroutine(CustomTransformFigure(
                    targetedFigureForAnim,
                    localTarget,
                    anim.FIGURE_ct_targetScale,
                    targetEulerLocal,
                    anim.FIGURE_ct_time,
                    anim.FIGURE_ct_tweenType));
                return true;
            }

            case FigureAnimation.BackToDefault:
                StartCoroutine(BackToDefaultFigure(
                    targetedFigureForAnim,
                    anim.FIGURE_ct_time,
                    anim.FIGURE_ct_tweenType));
                return true;

            default:
                Debug.LogWarning("[SMRunner] Unknown FigureAnimation");
                return false;
        }
    }

    #endregion

    #region Stat-/Spiel-Integration Helper

    public void SM_ApplyDamage(GameObject targeted_figure, int damage)
    {
        if (targeted_figure && damage != 0)
            DamageToFigure.current.ApplyDamage(targeted_figure, damage);
    }

    public void SM_ApplyHealing(GameObject targeted_figure, int healamount)
    {
        if (targeted_figure && healamount != 0)
            HealToFigure.current.HealingToFigure(targeted_figure, healamount);
    }

    public void SM_ApplyLoading(GameObject targeted_figure, int loadamount)
    {
        if (targeted_figure && loadamount != 0)
            LoadToFigure.current.LoadingToFigure(targeted_figure, loadamount);
    }

    public void SM_ApplyStatChange(GameObject targeted_figure, int amount, string stat_type)
    {
        if (targeted_figure && amount != 0)
            BaseStatChange.current.ChangeStatInteger(targeted_figure, amount, stat_type);
    }

    public void SM_DamagePlayer(GameObject targeted_player, int amount)
    {
        if (targeted_player && amount != 0)
            PlayerBaseController.current.SpecialMove_PlayerStatChange(targeted_player, "Damage", amount);
    }

    public void SM_HealPlayer(GameObject targeted_player, int amount)
    {
        if (targeted_player && amount != 0)
            PlayerBaseController.current.SpecialMove_PlayerStatChange(targeted_player, "Healing", amount);
    }

    public void SM_LoadPlayer(GameObject targeted_player, int amount)
    {
        if (targeted_player && amount != 0)
            PlayerBaseController.current.SpecialMove_PlayerStatChange(targeted_player, "Loading", amount);
    }

    public void SM_ChangeTargetSign(GameObject targeted_figure, string targeted_sign)
    {
        if (!targeted_figure)
        {
            Debug.LogWarning("[SMRunner] SM_ChangeTargetSign: targeted_figure (Caster) ist null.");
            return;
        }

        if (TargetingSystem.current == null)
        {
            Debug.LogWarning("[SMRunner] SM_ChangeTargetSign: TargetingSystem.current ist null.");
            return;
        }

        // String -> erwartetes Sign-Format für TargetingSystem
        string chosenSign;
        switch (targeted_sign)
        {
            case "Heart": chosenSign = "Heart"; break;
            case "Spade": chosenSign = "Spade"; break;
            case "Club":  chosenSign = "Club";  break;

            case "Diamond":
                Debug.LogWarning("[SMRunner] SM_ChangeTargetSign: 'Diamond' wird im TargetingSystem aktuell nicht unterstützt (nur Heart/Spade/Club).");
                return;

            default:
                Debug.LogWarning("[SMRunner] SM_ChangeTargetSign: Target_Sign konnte nicht gemappt werden: " + targeted_sign);
                return;
        }

        // NEU: Side des Casters stabil bestimmen (Multi-Caster sicher)
        var casterSide = DetectSideOfFigure(targeted_figure);

        // NEU: Gegner-Ziel eindeutig über Side bestimmen (statt current_figure_*)
        GameObject target = TargetingSystem.current.TargetFigureChoiceForSide(casterSide, chosenSign);

        // Sofort verbinden (kein Delay)
        TargetingSystem.current.ConnectFigureWithTarget(targeted_figure, target);
    }


    public void SM_FigureDefending(GameObject targeted_figure, bool IsDefending)
    {
        if (!targeted_figure) return;
        if (IsDefending) AttackController.current.Activate_Defense(targeted_figure);
        else AttackController.current.Deactivate_Defense(targeted_figure);
    }

    public void SM_GetsKilled(GameObject targeted_figure)
    {
        StartCoroutine(SM_GetsKilled_Enum(targeted_figure));
    }

    public IEnumerator SM_GetsKilled_Enum(GameObject targeted_figure)
    {
        if (!targeted_figure) yield break;

        yield return new WaitForEndOfFrame();
        DamageToFigure.current.KillFigure(targeted_figure);
    }

    public void SM_DrawPokerCards(GameObject targeted_player, int amount)
    {
        if (amount > 0) PokerCard_Animation.current.SpecialMove_DrawPokerCards(amount, targeted_player);
    }

    public void SM_DrawDeckCards(int amount)
    {
        if (amount > 0) PlayerDeck.current.GiveCard(amount);
    }

    #endregion

    #region Partikel / VFX Spawns

    private Transform GetFigureEffectAnchor(GameObject figure)
    {
        if (!figure) return null;
        var ep = figure.transform.Find("EffectPosition");
        return ep ? ep : figure.transform;
    }

    private void Particle_Animation_Relative(Transform parent, ParticleSystem particle, Vector3 localOffset, Vector3 worldScale, Vector3 worldEuler)
    {
        if (!parent || !particle) return;

        Vector3 worldPos = parent.TransformPoint(localOffset);
        Quaternion worldRot = Quaternion.Euler(worldEuler);

        var ps = Instantiate(particle, worldPos, worldRot);
        ps.transform.localScale = worldScale;
        ps.transform.SetParent(parent, true);

        ps.Play();
        StartCoroutine(DestroyWhenFinished(ps));
    }

    private void Particle_Animation_Relative_FigureAnchor(
        Transform figureAnchor,
        ParticleSystem particle,
        Vector3 localOffset,
        Vector3 worldScale,
        Vector3 worldEuler)
    {
        if (!figureAnchor || !particle) return;

        Vector3 worldPos = figureAnchor.TransformPoint(localOffset);
        Quaternion worldRot = Quaternion.Euler(worldEuler);

        var ps = Instantiate(particle, worldPos, worldRot);
        ps.transform.localScale = worldScale;
        ps.transform.SetParent(figureAnchor, true);

        ps.Play();
        StartCoroutine(DestroyWhenFinished(ps));
    }

    private void Particle_Animation_World(Vector3 worldPos, ParticleSystem particle, Vector3 worldScale, Vector3 worldEuler, Transform parent = null)
    {
        if (!particle) return;
        var ps = Instantiate(particle, worldPos, Quaternion.Euler(worldEuler), parent);
        ps.transform.localScale = worldScale;
        ps.Play();
        StartCoroutine(DestroyWhenFinished(ps));
    }

    private void Particle_Animation_Move(
        Vector3 worldStart,
        Vector3 worldEnd,
        ParticleSystem particle,
        Vector3 endScale,
        Vector3 endEuler,
        float moveTime,
        LeanTweenType tween)
    {
        if (!particle) return;

        var ps = Instantiate(particle, worldStart, Quaternion.Euler(endEuler), null);
        ps.transform.localScale = Vector3.one;
        ps.Play();

        float t = Mathf.Max(0.0001f, moveTime);
        LeanTween.move(ps.gameObject, worldEnd, t).setEase(tween);
        LeanTween.scale(ps.gameObject, endScale, t).setEase(tween);

        StartCoroutine(DestroyWhenFinished(ps));
    }

    private IEnumerator DestroyWhenFinished(ParticleSystem ps)
    {
        if (!ps) yield break;
        yield return new WaitUntil(() => !ps.IsAlive(true));
        if (ps) Destroy(ps.gameObject);
    }

    #endregion

    #region CameraShake

    private void CameraShake(float intensity, float duration)
    {
        if (cameraShake != null) cameraShake.Shake(duration, intensity);
    }

    #endregion

    #region Figure Animations (Hover / Stomp / Attack / Shake / Custom / BackToDefault)

    private Vector3 GetOrCacheDefaultWorldPos(GameObject figure)
    {
        if (!figure) return Vector3.zero;
        int id = figure.GetInstanceID();
        if (_defaultFigureWorldPos.TryGetValue(id, out var pos)) return pos;
        pos = figure.transform.position;
        _defaultFigureWorldPos[id] = pos;
        return pos;
    }

    private IEnumerator Hover(GameObject figure, float distance, float moveTime, float stayTime = 0.5f, float swayMagnitude = 10f, float swayTime = 0.3f)
    {
        if (!figure) yield break;
        Vector3 start = figure.transform.localPosition;

        var seq = LeanTween.sequence();

        seq.append(LeanTween.moveLocalZ(figure, start.z + distance, moveTime)
                            .setEase(LeanTweenType.easeOutQuad));

        seq.append(() =>
        {
            int loops = Mathf.Max(1, Mathf.RoundToInt(stayTime / (swayTime * 2f)));
            LeanTween.moveLocalZ(figure, start.z + distance + swayMagnitude, swayTime)
                     .setEase(LeanTweenType.easeInOutSine)
                     .setLoopPingPong(loops);
        });

        seq.append(stayTime);

        yield return new WaitForSeconds(moveTime + stayTime + moveTime);
    }

    private IEnumerator Stomp(GameObject figure, float height, float time)
    {
        if (!figure) yield break;
        Vector3 start = figure.transform.localPosition;

        LeanTween.sequence()
            .append(LeanTween.moveLocalY(figure, start.y + height, time * 0.5f).setEase(LeanTweenType.easeOutQuad))
            .append(LeanTween.moveLocalY(figure, start.y - (height * 0.2f), time * 0.3f).setEase(LeanTweenType.easeInQuad))
            .append(LeanTween.moveLocalY(figure, start.y, time * 0.2f).setEase(LeanTweenType.easeOutQuad));

        yield return new WaitForSeconds(time);
    }

    private IEnumerator Attack(GameObject figure, GameObject target, float distance, float time)
    {
        if (!figure || !target) yield break;

        Transform space = figure.transform.parent;
        Vector3 baseLocal = figure.transform.localPosition;
        Vector3 targetLocal = space.InverseTransformPoint(target.transform.position);

        Vector3 dir = (targetLocal - baseLocal).normalized;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;

        Vector3 hitLocal = baseLocal + dir * distance;

        LeanTween.sequence()
            .append(LeanTween.moveLocal(figure, hitLocal, time).setEase(LeanTweenType.easeOutQuad))
            .append(LeanTween.moveLocal(figure, baseLocal, time * 0.6f).setEase(LeanTweenType.easeInOutQuad));

        yield return new WaitForSeconds(time + time * 0.6f);
    }

    private IEnumerator Attack_Sided(GameObject figure, float distance, float time)
    {
        GameObject target = null;

        if (TurnManager.current)
        {
            target = (activeSide == SpecialMoveController.ActiveSide.P1)
                ? TurnManager.current.current_figures_target_P1
                : TurnManager.current.current_figures_target_P2;
        }

        if (!target) target = figure;

        yield return StartCoroutine(Attack(figure, target, distance, time));
    }

    private IEnumerator FigureShakeRoutine(GameObject figure, float magnitude, float time)
    {
        if (!figure) yield break;
        Vector3 start = figure.transform.localPosition;
        float t = 0f;

        while (t < time)
        {
            float remaining = Mathf.Clamp01(1f - (t / Mathf.Max(0.0001f, time)));
            float amp = magnitude * (0.6f + 0.4f * remaining);

            Vector2 r = Random.insideUnitCircle * amp;
            figure.transform.localPosition = start + new Vector3(r.x, r.y, 0f);

            t += Time.deltaTime;
            yield return null;
        }

        figure.transform.localPosition = start;
    }

    private IEnumerator CustomTransformFigure(
        GameObject figure,
        Vector3 targetLocalPos,
        Vector3 targetLocalScale,
        Vector3 targetLocalEuler,
        float time,
        LeanTweenType tween)
    {
        if (!figure) yield break;

        float t = Mathf.Max(0.0001f, time);
        LeanTween.moveLocal(figure, targetLocalPos, t).setEase(tween);
        LeanTween.scale(figure, targetLocalScale, t).setEase(tween);
        LeanTween.rotateLocal(figure, targetLocalEuler, t).setEase(tween);

        yield return new WaitForSeconds(t);
    }

    private IEnumerator BackToDefaultFigure(GameObject figure, float time, LeanTweenType tween)
    {
        if (!figure) yield break;

        var tr = figure.transform;
        int id = tr.GetInstanceID();

        if (!_figureDefaultLocalTRS.TryGetValue(id, out var def))
        {
            EnsureDefaultLocalCached(tr);
            def = _figureDefaultLocalTRS[id];
        }

        float t = Mathf.Max(0.0001f, time);

        LeanTween.moveLocal(figure, def.pos, t).setEase(tween);
        LeanTween.scale(figure, def.scale, t).setEase(tween);
        LeanTween.rotateLocal(figure, def.rot.eulerAngles, t).setEase(tween);

        yield return new WaitForSeconds(t);

        tr.localPosition = def.pos;
        tr.localRotation = def.rot;
        tr.localScale = def.scale;
    }


    

    #endregion

    #region Particle Root Resolver / Session Handling

    private bool TryResolveParticleRootWorld_Sided(
        FigureParticleRoot root,
        GameObject targetEffectAnchor,
        GameObject targetedFigureForAnim,
        TargetedPlayer animTargetedPlayer,
        Vector3 localOffsetFromPointZero,
        out Vector3 outWorldPos,
        out Transform outParentForSnap)
    {
        outWorldPos = Vector3.zero;
        outParentForSnap = null;

        // 1) Zielposition relativ zu PointZero + Side-Mirroring interpretieren
        Vector3 worldTarget = WorldFromPointZero(localOffsetFromPointZero);

        // 2) Parent-Zuordnung nur für SNAP
        switch (root)
        {
            case FigureParticleRoot.Figure:
            {
                if (targetEffectAnchor == null) return false;
                outParentForSnap = targetEffectAnchor.transform;
                break;
            }
            case FigureParticleRoot.DefaultFigurePosition:
            {
                outParentForSnap = null;
                break;
            }
            case FigureParticleRoot.FixScenePosition:
            {
                if (Central_Effect_Position == null) return false;
                outParentForSnap = Central_Effect_Position.transform;
                break;
            }
            case FigureParticleRoot.None:
            default:
            {
                GameObject anchor = AnchorFor(animTargetedPlayer);
                if (anchor) outParentForSnap = anchor.transform;
                break;
            }
        }

        outWorldPos = worldTarget;
        return true;
    }

    private void BeginSpecialMoveSession()
    {
        _figureDefaultLocalTRS.Clear();
        RefreshFiguresFromScene_SM();
        CacheCurrentLocals(Figures_P1);
        CacheCurrentLocals(Figures_P2);
    }

    private void RefreshFiguresFromScene_SM()
    {
        // P1
        var p1 = GameObject.Find("Game-Canvas/P1_Figures");
        if (p1)
        {
            int n = p1.transform.childCount;
            Figures_P1 = new GameObject[n];
            for (int i = 0; i < n; i++)
                Figures_P1[i] = p1.transform.GetChild(i).gameObject;
        }
        else
        {
            Figures_P1 = null;
            Debug.LogWarning("[SMRunner] 'Game-Canvas/P1_Figures' nicht gefunden.");
        }

        // P2
        var p2 = GameObject.Find("Game-Canvas/P2_Figures");
        if (p2)
        {
            int n = p2.transform.childCount;
            Figures_P2 = new GameObject[n];
            for (int i = 0; i < n; i++)
                Figures_P2[i] = p2.transform.GetChild(i).gameObject;
        }
        else
        {
            Figures_P2 = null;
            Debug.LogWarning("[SMRunner] 'Game-Canvas/P2_Figures' nicht gefunden.");
        }
    }

    private void CacheCurrentLocals(GameObject[] figs)
    {
        if (figs == null) return;
        for (int i = 0; i < figs.Length; i++)
        {
            var go = figs[i];
            if (!go) continue;
            var tr = go.transform;
            int id = tr.GetInstanceID();
            _figureDefaultLocalTRS[id] = new LocalTRS
            {
                pos = tr.localPosition,
                rot = tr.localRotation,
                scale = tr.localScale
            };
        }
    }

    private void EnsureDefaultLocalCached(Transform tr)
    {
        if (!tr) return;
        int id = tr.GetInstanceID();
        if (_figureDefaultLocalTRS.ContainsKey(id)) return;
        _figureDefaultLocalTRS[id] = new LocalTRS
        {
            pos = tr.localPosition,
            rot = tr.localRotation,
            scale = tr.localScale
        };
    }

    #endregion
}
