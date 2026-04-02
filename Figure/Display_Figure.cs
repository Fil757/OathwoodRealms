using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Display_Figure : MonoBehaviour
{
    [Header("Stammdaten (SO)")]
    public Figure FIGURE;

    [Header("Special Moves (aus Figure SO)")]
    public SpecialMove SPECIAL_A;
    public SpecialMove SPECIAL_B;

    [Header("Runtime-Parameter")]
    public string FIGURE_ID;
    public string FIGURE_NAME;
    public string FIGURE_TYPE;
    public string FIGURE_LEVEL;
    public int FIGURE_ATK;
    public int FIGURE_DEF;
    public int FIGURE_COST;
    public int FIGURE_COST_SPC;
    public int FIGURE_HEALTH;
    public int FIGURE_MAX_HEALTH;
    public int FIGURE_LOAD;
    public GameObject FIGURE_TARGET;
    public GameObject FIGURE_BEING_TARGETED_BY;
    public bool IS_DEFENDING;
    public bool IS_ATTACKING;

    // --- Baselines (Stammwerte) ---
    public int ORIGINAL_ATK;
    public int ORIGINAL_DEF;

    // Neu: harte Baseline für Kosten
    public int ORIGINAL_COST;
    public int ORIGINAL_COST_SPC;

    public int CURRENT_ATK_DIF = 0;

    [Header("Bars")]
    public Image HealthBar;
    public Image LoadBar;

    [Header("Targeting Frames")]
    public GameObject isTargetingParentFrame;
    public GameObject isTargeting_Heart;
    public GameObject isTargeting_Spade;
    public GameObject isTargeting_Club;

    [Header("Sign Frames")]
    public GameObject HeartFrame;
    public GameObject ClubFrame;
    public GameObject SpadeFrame;

    [Header("Targeted by Frames")]
    public GameObject isTargetedByParentFrame;
    public GameObject isTargetedBy_Heart;
    public GameObject isTargetedBy_Spade;
    public GameObject isTargetedBy_Club;

    [Header("UI-Text")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI loadText;

    // ALT: ein Cost-Text
    public TextMeshProUGUI costText;

    // NEU: 2 weitere Cost-Textslots + 1 SpecialCost-Textslot
    [Header("UI-Text (Costs)")]
    public TextMeshProUGUI costText_2;
    public TextMeshProUGUI costText_3;
    public TextMeshProUGUI specialCostText;

    public GameObject atk_stuff;
    public GameObject def_stuff;

    [Header("3D Darstellung")]
    public Transform MeshAnchor;   // leerer Child im Prefab
    private GameObject prefabInstance;

    public GameObject ConnectedStatField;

    [Header("Portrait Stati")]
    public Sprite FigurePortraitSprite;

    void Awake()
    {
        Update_Parameter();
        UpdateTargetFrames();
        InsertFigureFieldEffect();
    }

    void Update()
    {
        // Harte Regeln IMMER durchsetzen (egal wer Stats ändert)
        EnforceHardBaselines();

        if (healthText) healthText.text = FIGURE_HEALTH.ToString();
        if (loadText)   loadText.text   = FIGURE_LOAD.ToString();
        if (defText)    defText.text    = FIGURE_DEF.ToString();

        // Costs: 3 Slots + SpecialCost
        string costStr = FIGURE_COST.ToString();
        if (costText)   costText.text   = costStr;
        if (costText_2) costText_2.text = costStr;
        if (costText_3) costText_3.text = costStr;

        if (specialCostText) specialCostText.text = FIGURE_COST_SPC.ToString();

        int diff_atk = FIGURE_ATK - ORIGINAL_ATK;
        int diff_def = FIGURE_DEF - ORIGINAL_DEF;

        UpdateTargetFrames();
    }

    public void Update_Parameter()
    {
        if (FIGURE == null) return;
        ApplyFigureSO(FIGURE);
    }

    private bool ATK_CHANGED()
    {
        if (FIGURE_ATK != ORIGINAL_ATK) { return true; }
        return false;
    }

    public void ApplyFigureSO(Figure so)
    {
        if (so == null) return;

        FIGURE = so;

        // Stammdaten
        FIGURE_ID    = so.FIGURE_ID;
        FIGURE_NAME  = so.FIGURE_NAME;
        FIGURE_LEVEL = so.FIGURE_LEVEL;

        // Stats
        FIGURE_TYPE   = so.FIGURE_TYPE;

        FIGURE_ATK    = so.FIGURE_ATK;
        ORIGINAL_ATK  = so.FIGURE_ATK;

        FIGURE_DEF    = so.FIGURE_DEF;
        ORIGINAL_DEF  = so.FIGURE_DEF;

        FIGURE_COST   = so.FIGURE_COST;
        ORIGINAL_COST = so.FIGURE_COST;

        FIGURE_COST_SPC   = so.FIGURE_COST_SPC;
        ORIGINAL_COST_SPC = so.FIGURE_COST_SPC;

        FIGURE_HEALTH = so.FIGURE_HEALTH;
        FIGURE_MAX_HEALTH = FIGURE_HEALTH;

        FIGURE_LOAD   = so.FIGURE_LOAD;
        IS_DEFENDING  = false;

        // Specials
        SPECIAL_A = so.SPECIAL_A;
        SPECIAL_B = so.SPECIAL_B;

        // Prefab darstellen
        BuildOrUpdatePrefab(so);

        // Sofort clampen (falls SO Mistwerte enthält oder vorherige Runtime-Werte noch drin waren)
        EnforceHardBaselines();

        // UI updaten
        RefreshUI();

        // Stati Sprite
        FigurePortraitSprite = so.FigurePortraitSprite;

        UpdateOwnSignFrame();
        UpdateTargetFrames();
    }

    private void BuildOrUpdatePrefab(Figure so)
    {
        if (prefabInstance != null) Destroy(prefabInstance);

        if (so != null && so.FIGURE_PREFAB != null && MeshAnchor != null)
        {
            prefabInstance = Instantiate(so.FIGURE_PREFAB, MeshAnchor);
            prefabInstance.transform.localPosition = Vector3.zero;
            prefabInstance.transform.localRotation = Quaternion.identity;
            prefabInstance.transform.localScale    = Vector3.one;
        }
    }

    public void RefreshUI()
    {
        RefreshBasicUI();

        if (HealthBar && FIGURE_MAX_HEALTH > 0)
            HealthBar.fillAmount = Mathf.Clamp01(FIGURE_HEALTH / (float)FIGURE_MAX_HEALTH);
        if (LoadBar)
            LoadBar.fillAmount = Mathf.Clamp01(FIGURE_LOAD / 100f);
    }

    public void RefreshBasicUI()
    {
        if (nameText)   nameText.text   = FIGURE_NAME;
        if (atkText)    atkText.text    = FIGURE_ATK.ToString();
        if (defText)    defText.text    = FIGURE_DEF.ToString();
        if (levelText)  levelText.text  = FIGURE_LEVEL;
        if (healthText) healthText.text = FIGURE_HEALTH.ToString();
        if (loadText)   loadText.text   = FIGURE_LOAD.ToString();

        // Costs: 3 Slots + SpecialCost (sofort korrekt nach ApplyFigureSO/RefreshUI)
        string costStr = FIGURE_COST.ToString();
        if (costText)   costText.text   = costStr;
        if (costText_2) costText_2.text = costStr;
        if (costText_3) costText_3.text = costStr;

        if (specialCostText) specialCostText.text = FIGURE_COST_SPC.ToString();
    }

    public SpecialMove GetSpecialA() => SPECIAL_A;
    public SpecialMove GetSpecialB() => SPECIAL_B;

    // ---------------- Harte Baselines / Clamps ----------------

    /// <summary>
    /// Harte Regeln:
    /// - ATK/DEF dürfen niemals unter ORIGINAL_* fallen (Buffs nach oben sind erlaubt).
    /// - COST/COST_SPC dürfen niemals über ORIGINAL_* steigen und niemals < 0 werden.
    /// </summary>
    private void EnforceHardBaselines()
    {
        // ATK/DEF: Mindestwert = Original
        if (FIGURE_ATK < ORIGINAL_ATK) FIGURE_ATK = ORIGINAL_ATK;
        if (FIGURE_DEF < ORIGINAL_DEF) FIGURE_DEF = ORIGINAL_DEF;

        // Kosten: 0..Original
        if (ORIGINAL_COST < 0) ORIGINAL_COST = 0;
        if (ORIGINAL_COST_SPC < 0) ORIGINAL_COST_SPC = 0;

        FIGURE_COST     = Mathf.Clamp(FIGURE_COST,     0, ORIGINAL_COST);
        FIGURE_COST_SPC = Mathf.Clamp(FIGURE_COST_SPC, 0, ORIGINAL_COST_SPC);
    }

    // ---------------- Target-Frame Logik ----------------

    private void UpdateTargetFrames()
    {
        UpdateIsTargetingFrames();
        //UpdateIsTargetedByFrames();
    }

    private void UpdateIsTargetingFrames()
    {
        if (!isTargetingParentFrame)
            return;

        if (FIGURE_TARGET == null)
        {
            isTargetingParentFrame.SetActive(false);
            SetSignFrame(isTargeting_Heart, isTargeting_Spade, isTargeting_Club, null);
            return;
        }

        string sign = null;
        var targetDf = FIGURE_TARGET.GetComponent<Display_Figure>();
        if (targetDf != null)
        {
            sign = targetDf.FIGURE_TYPE;
        }

        bool hasValidSign = !string.IsNullOrEmpty(sign);
        isTargetingParentFrame.SetActive(hasValidSign);
        SetSignFrame(isTargeting_Heart, isTargeting_Spade, isTargeting_Club, hasValidSign ? sign : null);
    }

    private void SetSignFrame(GameObject heart, GameObject spade, GameObject club, string sign)
    {
        bool hasSign = !string.IsNullOrEmpty(sign);

        if (heart) heart.SetActive(hasSign && sign == "Heart");
        if (spade) spade.SetActive(hasSign && sign == "Spade");
        if (club)  club.SetActive(hasSign && sign == "Club");
    }

    private void UpdateOwnSignFrame()
    {
        if (!HeartFrame && !SpadeFrame && !ClubFrame)
            return;

        string sign = (FIGURE_TYPE ?? string.Empty).Trim();

        bool isHeart = sign == "Heart";
        bool isSpade = sign == "Spade";
        bool isClub  = sign == "Club";

        if (HeartFrame) HeartFrame.SetActive(isHeart);
        if (SpadeFrame) SpadeFrame.SetActive(isSpade);
        if (ClubFrame)  ClubFrame.SetActive(isClub);

        if (!isHeart && !isSpade && !isClub && !string.IsNullOrEmpty(sign))
        {
            Debug.LogWarning($"[Display_Figure] Unbekanntes FIGURE_TYPE für SignFrames: '{sign}' auf {gameObject.name}");
        }
    }

    public void InsertFigureFieldEffect()
    {
        if (transform.parent == null)
        {
            Debug.LogWarning($"[FigureFieldEffect] Kein Parent gefunden bei {gameObject.name}");
            return;
        }

        string parentName = transform.parent.name;
        string figureName = FIGURE != null ? FIGURE.FIGURE_NAME : gameObject.name;

        if (parentName == "P1_Figures")
        {
            FieldCardController.instance.ActiveFigureFieldEffect.Add(FIGURE.Figure_FieldEffect);
            Debug.Log($"[P1] FigureFieldEffect gesetzt für Figur: {figureName}");
        }
        else if (parentName == "P2_Figures")
        {
            FieldCardController_P2.instance.ActiveFigureFieldEffect.Add(FIGURE.Figure_FieldEffect);
            Debug.Log($"[P2] FigureFieldEffect gesetzt für Figur: {figureName}");
        }
        else
        {
            Debug.LogWarning(
                $"[FigureFieldEffect] Unbekannter Parent '{parentName}' bei Figur: {figureName}"
            );
        }
    }
}
