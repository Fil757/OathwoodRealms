using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomInspector; // Asset: Custom Inspector

public enum CardSign { None, Heart, Spade, Club, Diamond }
public enum TargetedPlayer { None, Self, Opponent }
public enum ChangedIntegerStats { None, Figure_Health, Player_Health, Figure_Load, Player_Load, Attack, Defense, Cost }
public enum ChangedBoolStats { None, Is_Defending, Gets_Killed }
public enum ChangedStringStats { None, Target_Sign }
public enum FigureParticleRoot { None, Figure, DefaultFigurePosition, FixScenePosition }

// NEU: CustomTransform + BackToDefault
public enum FigureAnimation { None, CustomTransform, BackToDefault, Attack, Hover, Stomp, Shake }

// NEU: Partikelbewegung
public enum ParticleMovementMode { None, Snap, Move }

[System.Serializable]
public class SpecialMoveStats
{
#if UNITY_EDITOR
    [HideInInspector] public Vector2 editor_NodePosition;
    [HideInInspector] public Vector2 editor_NodeSize;
#endif

    [TextArea]
    public string description = "";

    [HorizontalLine(1, FixedColor.Green, 2)]
    [Space(3)]

    [LabelSettings("Use Stats")]
    public bool useStats = true;

    // Target-Filter
    [LabelSettings("Target Sign"), ShowIf(nameof(useStats))]
    public CardSign SPECIALMOVE_TARGET_SIGN;

    [LabelSettings("Target Player"), ShowIf(nameof(useStats))]
    public TargetedPlayer SPECIALMOVE_TARGET_PLAYER;

    // Integer-Änderung
    [LabelSettings("Int Stat"), ShowIf(nameof(useStats))]
    public ChangedIntegerStats SPECIALMOVE_CHANGE_STATS_INTEGER;

    [LabelSettings("Δ Int"), ShowIf(nameof(HasIntegerChange))]
    public int SPECIALMOVE_CHANGE_STATS_INTEGER_CHANGE;

    // String-Änderung
    [LabelSettings("String Stat"), ShowIf(nameof(useStats))]
    public ChangedStringStats SPECIALMOVE_CHANGE_STATS_STRING;

    [LabelSettings("New String"), ShowIf(nameof(HasStringChange))]
    public string SPECIALMOVE_CHANGE_STATS_STRING_CHANGE;

    // Bool-Änderung
    [LabelSettings("Bool Stat"), ShowIf(nameof(useStats))]
    public ChangedBoolStats SPECIALMOVE_CHANGE_STATS_BOOL;

    [LabelSettings("New Bool"), ShowIf(nameof(HasBoolChange))]
    public bool SPECIALMOVE_CHANGE_STATS_BOOL_CHANGE;

    // Draw-Effekte
    [LabelSettings("Poker Draw"), ShowIf(nameof(useStats))]
    public int SPECIALMOVE_POKERCARDS_DRAWING;

    [LabelSettings("Deck Draw"), ShowIf(nameof(useStats))]
    public int SPECIALMOVE_DECKCARDS_DRAWING;

    // Sequenz-Timing
    [LabelSettings("Next Gap (s)"), ShowIf(nameof(useStats))]
    public float TIME_GAP_TO_NEXT;

    // Animationsliste
    [LabelSettings("Animations")]
    public SpecialMoveAnimations[] SpecialAnimations;

    // ---- ShowIf-Gates (nur Logik) ----
    public bool HasIntegerChange() => useStats && SPECIALMOVE_CHANGE_STATS_INTEGER != ChangedIntegerStats.None;
    public bool HasStringChange()  => useStats && SPECIALMOVE_CHANGE_STATS_STRING  != ChangedStringStats.None;
    public bool HasBoolChange()    => useStats && SPECIALMOVE_CHANGE_STATS_BOOL    != ChangedBoolStats.None;
}

[System.Serializable]
public class SpecialMoveAnimations
{
    [Space(20)]
    [HorizontalLine(1, FixedColor.Green, 2)]
    [Space(3)]

    // ------------------- Ziel-Figur(en) (optional) -------------------
    [LabelSettings("Use Figure")]
    public bool useFigure = false;

    [LabelSettings("Target Player"), ShowIf(nameof(useFigure))]
    public TargetedPlayer SPECIALMOVE_ANIMATION_TARGETED_PLAYER;

    [LabelSettings("Target Sign"), ShowIf(nameof(useFigure))]
    public CardSign SPECIALMOVE_ANIMATION_TARGETED_SIGN;

    [Space(3)]
    [HorizontalLine(1, FixedColor.Yellow, 0)]
    [Space(3)]

    // ------------------- Partikel -------------------
    [LabelSettings("Use Particle")]
    public bool useParticle = false;

    [LabelSettings("Particle System"), ShowIf(nameof(useParticle))]
    public ParticleSystem SPECIALMOVE_ANIMATION;

    [LabelSettings("Mirror POS for other Player"), ShowIf(nameof(useParticle))]
    public bool use_pos_mirror_effect_for_other_side;

    [LabelSettings("Mirror ROT for other Player"), ShowIf(nameof(useParticle))]
    public bool use_rot_mirror_effect_for_other_side;

    // Ziel-Root (Ende / Snap-Ziel)
    [LabelSettings("Target-Pos-Root"), ShowIf(nameof(useParticle))]
    public FigureParticleRoot SPECIALMOVE_PARTICLE_ROOT;

    // Ziel-Transform (gilt für Snap und Move)
    [LabelSettings("Target-Pos-Offset"), ShowIf(nameof(NeedsParticleTransform))]
    public Vector3 SPECIALMOVE_ANIMATION_POSITION;

    [LabelSettings("Target-Scale"), ShowIf(nameof(NeedsParticleTransform))]
    public Vector3 SPECIALMOVE_ANIMATION_SCALE_BY;

    [LabelSettings("Target-Rotation"), ShowIf(nameof(NeedsParticleTransform))]
    public Vector3 SPECIALMOVE_ANIMATION_ROTATION;

    // Bewegungsmodus für Partikel
    [LabelSettings("Particle Move Mode"), ShowIf(nameof(useParticle))]
    public ParticleMovementMode SPECIALMOVE_PARTICLE_MOVEMENT = ParticleMovementMode.Snap;

    // Start-Root & Offset (nur für Move)
    [LabelSettings("Start-Pos-Root"), ShowIf(nameof(UsesParticleMove))]
    public FigureParticleRoot SPECIALMOVE_PARTICLE_FROM_ROOT = FigureParticleRoot.None;

    [LabelSettings("Start-Pos-Offset"), ShowIf(nameof(UsesParticleMove))]
    public Vector3 SPECIALMOVE_PARTICLE_FROM_POSITION = Vector3.zero;

    // Tween-Parameter (nur für Move)
    [LabelSettings("Move Time (s)"), ShowIf(nameof(UsesParticleMove))]
    public float SPECIALMOVE_PARTICLE_MOVE_TIME = 0.5f;

    [LabelSettings("Move Tween"), ShowIf(nameof(UsesParticleMove))]
    public LeanTweenType SPECIALMOVE_PARTICLE_MOVE_TWEEN = LeanTweenType.easeOutQuad;

    [Space(3)]
    [HorizontalLine(1, FixedColor.Yellow, 0)]
    [Space(3)]

    // ------------------- Figuren-Animation -------------------
    [LabelSettings("Figure Anim")]
    [Hook(nameof(OnFigureAnimationChanged))]
    public FigureAnimation SPECIALMOVE_ANIMATION_FIGURE_ANIMATION;

    // --- Custom Transform-Params (Figur) ---
    // Positions-/Rotations-/Scale-Ziele nur bei CustomTransform sichtbar
    [LabelSettings("CT Pos"), ShowIf(nameof(IsCustomTransform))]
    public Vector3 FIGURE_ct_targetPosition = Vector3.zero;

    [LabelSettings("CT Scale"), ShowIf(nameof(IsCustomTransform))]
    public Vector3 FIGURE_ct_targetScale = Vector3.one;

    [LabelSettings("CT Rotation"), ShowIf(nameof(IsCustomTransform))]
    public Vector3 FIGURE_ct_targetEuler = Vector3.zero;

    // Zeit & Tween sollen bei CustomTransform *und* BackToDefault sichtbar bleiben
    [LabelSettings("CT Time (s)"), ShowIf(nameof(IsCTTimingVisible))]
    public float FIGURE_ct_time = 0.35f;

    [LabelSettings("CT Tween"), ShowIf(nameof(IsCTTimingVisible))]
    public LeanTweenType FIGURE_ct_tweenType = LeanTweenType.easeOutQuad;

    // Hover-Params
    [LabelSettings("Hover Dist"), ShowIf(nameof(IsHover))]
    public float FIGURE_hoverDistance = 45f;

    [LabelSettings("Hover Time"), ShowIf(nameof(IsHover))]
    public float FIGURE_hoverTime = 0.5f;

    [LabelSettings("Hover Stay"), ShowIf(nameof(IsHover))]
    public float FIGURE_hover_stayTime = 0.5f;

    [LabelSettings("Hover Sway Mag"), ShowIf(nameof(IsHover))]
    public float FIGURE_hover_swayMagnitude = 10f;

    [LabelSettings("Hover Sway Time"), ShowIf(nameof(IsHover))]
    public float FIGURE_hover_swayTime = 0.3f;

    // Stomp-Params
    [LabelSettings("Stomp H"), ShowIf(nameof(IsStomp))]
    public float FIGURE_stompHeight = 120f;

    [LabelSettings("Stomp T"), ShowIf(nameof(IsStomp))]
    public float FIGURE_stompTime = 0.3f;

    // Attack-Params
    [LabelSettings("Attack Dist"), ShowIf(nameof(IsAttack))]
    public float FIGURE_attackDistance = 300f;

    [LabelSettings("Attack Time"), ShowIf(nameof(IsAttack))]
    public float FIGURE_attackTime = 0.2f;

    // Shake-Params
    [LabelSettings("Shake Mag"), ShowIf(nameof(IsShake))]
    public float FIGURE_shakeMagnitude = 15f;

    [LabelSettings("Shake Time"), ShowIf(nameof(IsShake))]
    public float FIGURE_shakeTime = 0.35f;

    [Space(3)]
    [HorizontalLine(1, FixedColor.Yellow, 0)]
    [Space(3)]

    // ------------------- Kamera/UI-Shake -------------------
    [LabelSettings("Use Cam/UI Shake")]
    public bool SPECIALMOVE_USE_SHAKE = false;

    [LabelSettings("Shake Intensity"), ShowIf(nameof(SPECIALMOVE_USE_SHAKE))]
    public float SPECIALMOVE_SHAKE_INTENSITY = 5f;

    [LabelSettings("Shake Duration"), ShowIf(nameof(SPECIALMOVE_USE_SHAKE))]
    public float SPECIALMOVE_SHAKE_DURATION = 0.35f;

    [Space(3)]
    [HorizontalLine(1, FixedColor.Yellow, 0)]
    [Space(3)]

    // Schrittinterner Delay
    [LabelSettings("Step Gap (s)")]
    public float SPECIALMOVE_ANIMATION_TIMEGAP;

    // ------------------- AudioTrack -------------------

    [Space(8)]
    [HorizontalLine(1, FixedColor.Cyan, 0)]
    [Space(3)]

    [LabelSettings("Use Sound")]
    public bool useSound = false;

    [LabelSettings("Sound Profile"), ShowIf(nameof(useSound))]
    public SoundProfile SPECIALMOVE_SOUNDPROFILE;

    [LabelSettings("Attach To Figure"), ShowIf(nameof(useSound))]
    public bool attachSoundToFigure = true;

    // ------------------- ShowIf-Gates (nur Logik) -------------------
    public bool IsHover()           => SPECIALMOVE_ANIMATION_FIGURE_ANIMATION == FigureAnimation.Hover;
    public bool IsStomp()           => SPECIALMOVE_ANIMATION_FIGURE_ANIMATION == FigureAnimation.Stomp;
    public bool IsAttack()          => SPECIALMOVE_ANIMATION_FIGURE_ANIMATION == FigureAnimation.Attack;
    public bool IsShake()           => SPECIALMOVE_ANIMATION_FIGURE_ANIMATION == FigureAnimation.Shake;
    public bool IsCustomTransform() => SPECIALMOVE_ANIMATION_FIGURE_ANIMATION == FigureAnimation.CustomTransform;
    public bool IsBackToDefault()   => SPECIALMOVE_ANIMATION_FIGURE_ANIMATION == FigureAnimation.BackToDefault;

    // Zeit & Tween: sichtbar bei CT und BackToDefault
    public bool IsCTTimingVisible() => IsCustomTransform() || IsBackToDefault();

    public bool NeedsParticleTransform()
        => useParticle && SPECIALMOVE_ANIMATION != null &&
           SPECIALMOVE_PARTICLE_ROOT != FigureParticleRoot.None;

    public bool UsesParticleMove()
        => useParticle && SPECIALMOVE_ANIMATION != null &&
           SPECIALMOVE_PARTICLE_MOVEMENT == ParticleMovementMode.Move;

    // ------------------- Hook: Umschalt-Reaktion -------------------
    // Bei BackToDefault NUR Pos/Rot/Scale neutralisieren; Zeit/Tween bleiben unangetastet.
    void OnFigureAnimationChanged(FigureAnimation oldValue, FigureAnimation newValue)
    {
        if (newValue == FigureAnimation.BackToDefault)
        {
            FIGURE_ct_targetPosition = Vector3.zero;
            FIGURE_ct_targetScale    = Vector3.one;
            FIGURE_ct_targetEuler    = Vector3.zero;
            // FIGURE_ct_time und FIGURE_ct_tweenType bleiben absichtlich unverändert
        }
    }
}


[CreateAssetMenu(fileName = "New Specialmove", menuName = "Specialmove")]
public class SpecialMove : ScriptableObject
{
    [Header("Base Information")]
    [LabelSettings("ID")]
    public string SPECIALMOVE_ID;

    [LabelSettings("Title")]
    public string SPECIALMOVE_TITLE;

    [LabelSettings("Description"), TextArea]
    public string SPECIALMOVE_DESCRIPTION;

    [LabelSettings("Load Cost")]
    public int SPECIALMOVE_LOAD_COST;

    [LabelSettings("Sound Profile")]
    public SoundProfile SPECIALMOVE_SOUND;

    [LabelSettings("MakeSense Condition (for Bot)")]
    public ConditionBlock MakeSenseEvaluation_Block;

    [Header("Steps")]
    [LabelSettings("Stats")]
    public SpecialMoveStats[] Stats;
}
