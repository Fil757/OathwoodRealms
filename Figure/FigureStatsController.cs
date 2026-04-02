using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FigureStatsController : MonoBehaviour
{
    public static FigureStatsController instance;

    [Header("Prefab")]
    public GameObject figureStatPrefab;

    [Header("P1 Stat Fields")]
    public GameObject P1_Figure_Stat_Field;
    public GameObject P1_Figure_Stat_Field_mirror;

    [Header("P2 Stat Fields")]
    public GameObject P2_Figure_Stat_Field;
    public GameObject P2_Figure_Stat_Field_mirror;

    [Header("Resources")]
    [Tooltip("Folder prefix under Resources for portraits, e.g. 'Portraits/'.")]
    public string portraitResourcesFolder = "Portraits/";
    [Tooltip("Sign resource names without extension: LightBG_Heart, LightBG_Spade, LightBG_Club, LightBG_Diamond")]
    public string signPrefix = "LightBG_";

    // small caches to avoid repeated Resources.Load calls
    private readonly Dictionary<string, Sprite> _signSpriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> _portraitSpriteCache = new Dictionary<string, Sprite>();

    void Awake()
    {
        instance = this;
    }

    // Creates both the main (data-bound) and the mirror (visual copy) for the active player.
    public void CreateFigureStat(string casted_sign)
    {
        var side = ResolveActiveSide();
        ResolveStatParents(side, out Transform mainParent, out Transform mirrorParent);

        // resolve the figure on the correct side by FIGURE_TYPE (Heart/Spade/Club/Diamond)
        GameObject casted_figure = FindCastedFigure(casted_sign, side);

        bool canBuildMain = figureStatPrefab != null && mainParent != null && casted_figure != null;
        bool canBuildMirror = figureStatPrefab != null && mirrorParent != null && casted_figure != null;

        GameObject mainStat = null;
        GameObject mirrorStat = null;

        if (!canBuildMain)
        {
            if (figureStatPrefab == null) Debug.LogWarning("[FigureStatsController] figureStatPrefab is not assigned.");
            if (mainParent == null) Debug.LogWarning("[FigureStatsController] mainParent missing for side: " + side);
            if (casted_figure == null) Debug.LogWarning("[FigureStatsController] No figure with sign '" + casted_sign + "' found for side: " + side);
        }
        else
        {
            mainStat = Instantiate(figureStatPrefab, mainParent);
            ConnectStatsToFigure_DataBound(casted_figure, mainStat);
            GenerateSignPicture(casted_sign, mainStat);
            GeneratePortraitPicture(casted_figure, mainStat);
            mainStat.transform.SetAsLastSibling();
        }

        if (!canBuildMirror)
        {
            if (mirrorParent == null) Debug.Log("[FigureStatsController] mirrorParent missing for side: " + side + " (mirror left empty).");
        }
        else
        {
            mirrorStat = Instantiate(figureStatPrefab, mirrorParent);
            // initial fill from DF values for immediate visual parity
            ConnectStatsToFigure_VisualOnly(casted_figure, mirrorStat);
            GenerateSignPicture(casted_sign, mirrorStat);
            GeneratePortraitPicture(casted_figure, mirrorStat);
            mirrorStat.name += " (Mirror)";
            mirrorStat.transform.SetAsLastSibling();
        }

        // keep mirror in perfect sync with main every frame (texts, bars, images)
        if (mainStat && mirrorStat)
            StartCoroutine(SyncMirrorContinuous(mainStat, mirrorStat));
    }

    /// <summary>
    /// Erzeugt Stat-Felder gezielt für eine übergebene Figuren-Instanz. Nutzt die richtige Seite (P1/P2)
    /// und bindet das Main-Stat-Feld data-bound an die Display_Figure. Das Mirror-Feld wird visuell synchronisiert.
    /// </summary>
    public void CreateFigureStatForFigure(GameObject figureGO, bool forP2)
    {
        if (figureGO == null)
        {
            Debug.LogWarning("[FigureStatsController] CreateFigureStatForFigure: figureGO is null");
            return;
        }

        var df = figureGO.GetComponent<Display_Figure>();
        if (df == null)
        {
            Debug.LogWarning("[FigureStatsController] CreateFigureStatForFigure: Display_Figure missing on figureGO");
            return;
        }

        var side = forP2 ? Side.P2 : Side.P1;
        ResolveStatParents(side, out Transform mainParent, out Transform mirrorParent);

        if (figureStatPrefab == null)
        {
            Debug.LogWarning("[FigureStatsController] figureStatPrefab not set");
            return;
        }
        if (mainParent == null)
        {
            Debug.LogWarning("[FigureStatsController] mainParent is null for side " + side);
            return;
        }

        // MAIN (data-bound)
        var mainStat = Instantiate(figureStatPrefab, mainParent);
        ConnectStatsToFigure_DataBound(figureGO, mainStat);
        GenerateSignPicture(df.FIGURE_TYPE, mainStat);
        GeneratePortraitPicture(figureGO, mainStat);
        mainStat.transform.SetAsLastSibling();

        // MIRROR (visual only)
        if (mirrorParent != null)
        {
            var mirrorStat = Instantiate(figureStatPrefab, mirrorParent);
            ConnectStatsToFigure_VisualOnly(figureGO, mirrorStat);
            GenerateSignPicture(df.FIGURE_TYPE, mirrorStat);
            GeneratePortraitPicture(figureGO, mirrorStat);
            mirrorStat.name += " (Mirror)";
            mirrorStat.transform.SetAsLastSibling();
            StartCoroutine(SyncMirrorContinuous(mainStat, mirrorStat));
        }
    }

    // Removes all P1 stat fields for the given sign across all four containers.
    public void DestroyFigureStat_P1(string killed_sign)
    {
        bool hasSign = !string.IsNullOrEmpty(killed_sign);

        if (hasSign)
        {
            Transform[] parents =
            {
                P1_Figure_Stat_Field           ? P1_Figure_Stat_Field.transform           : null,
                P1_Figure_Stat_Field_mirror    ? P1_Figure_Stat_Field_mirror.transform    : null,
                //P2_Figure_Stat_Field           ? P2_Figure_Stat_Field.transform           : null,
                //P2_Figure_Stat_Field_mirror    ? P2_Figure_Stat_Field_mirror.transform    : null
            };

            for (int p = 0; p < parents.Length; p++)
            {
                Transform parent = parents[p];
                if (parent == null) continue;

                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var child = parent.GetChild(i);
                    var stat = child.GetComponent<FigureStat>();
                    if (stat != null && stat.typeOfFigureHolder == killed_sign)
                    {
                        Debug.Log($"[FigureStatsController] Removing Stat-Field '{child.name}' for sign '{killed_sign}' on '{parent.name}'.");
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[FigureStatsController] DestroyFigureStat: killed_sign is null/empty.");
        }
    }
    // Removes all P2 stat fields for the given sign across all four containers.
    public void DestroyFigureStat_P2(string killed_sign)
    {
        bool hasSign = !string.IsNullOrEmpty(killed_sign);

        if (hasSign)
        {
            Transform[] parents =
            {
                //P1_Figure_Stat_Field           ? P1_Figure_Stat_Field.transform           : null,
                //P1_Figure_Stat_Field_mirror    ? P1_Figure_Stat_Field_mirror.transform    : null,
                P2_Figure_Stat_Field           ? P2_Figure_Stat_Field.transform           : null,
                P2_Figure_Stat_Field_mirror    ? P2_Figure_Stat_Field_mirror.transform    : null
            };

            for (int p = 0; p < parents.Length; p++)
            {
                Transform parent = parents[p];
                if (parent == null) continue;

                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var child = parent.GetChild(i);
                    var stat = child.GetComponent<FigureStat>();
                    if (stat != null && stat.typeOfFigureHolder == killed_sign)
                    {
                        Debug.Log($"[FigureStatsController] Removing Stat-Field '{child.name}' for sign '{killed_sign}' on '{parent.name}'.");
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[FigureStatsController] DestroyFigureStat: killed_sign is null/empty.");
        }
    }

    // =========================
    // INTERNAL HELPERS
    // =========================

    private enum Side { P1, P2 }

    // Turns "P1"/"P2" from TurnManager into a local enum.
    private Side ResolveActiveSide()
    {
        var tm = TurnManager.current;
        if (tm == null)
        {
            Debug.LogWarning("[FigureStatsController] TurnManager.current is null. Defaulting to P1.");
            return Side.P1;
        }

        string ap = tm.activePlayer;
        if (ap == "P2") return Side.P2;

        if (ap != "P1")
            Debug.LogWarning("[FigureStatsController] Unexpected activePlayer value: '" + ap + "'. Defaulting to P1.");

        return Side.P1;
    }

    // Resolves main/mirror parents for the current side.
    private void ResolveStatParents(Side side, out Transform mainParent, out Transform mirrorParent)
    {
        mainParent = null;
        mirrorParent = null;

        if (side == Side.P1)
        {
            if (P1_Figure_Stat_Field) mainParent = P1_Figure_Stat_Field.transform;
            if (P1_Figure_Stat_Field_mirror) mirrorParent = P1_Figure_Stat_Field_mirror.transform;
        }
        else
        {
            if (P2_Figure_Stat_Field) mainParent = P2_Figure_Stat_Field.transform;
            if (P2_Figure_Stat_Field_mirror) mirrorParent = P2_Figure_Stat_Field_mirror.transform;
        }
    }

    // Finds the figure on the requested side with matching FIGURE_TYPE (Heart/Spade/Club/Diamond).
    private GameObject FindCastedFigure(string casted_sign, Side side)
    {
        var tm = TurnManager.current;
        GameObject[] currentFigures = null;

        if (tm != null)
            currentFigures = (side == Side.P1) ? tm.Figures_P1 : tm.Figures_P2;

        if (currentFigures != null)
        {
            for (int i = 0; i < currentFigures.Length; i++)
            {
                var go = currentFigures[i];
                if (!go) continue;

                var df = go.GetComponent<Display_Figure>();
                if (df != null && df.FIGURE_TYPE == casted_sign)
                    return go;
            }
        }
        else
        {
            Debug.LogWarning("[FigureStatsController] Figure array for side '" + side + "' is null.");
        }

        return null;
    }

    // MAIN: binds UI (bars & texts) to Display_Figure for live updates by gameplay systems.
    private void ConnectStatsToFigure_DataBound(GameObject casted_figure, GameObject instantiated_stat)
    {
        var df = casted_figure ? casted_figure.GetComponent<Display_Figure>() : null;
        var ins = instantiated_stat ? instantiated_stat.GetComponent<FigureStat>() : null;

        if (df != null && ins != null)
        {
            // Live bindings for bars & text (Display_Figure drives these continuously)
            df.HealthBar = ins.HealthBar;
            df.LoadBar = ins.LoadBar;

            df.nameText = ins.nameText;
            df.healthText = ins.healthText;
            df.loadText = ins.loadText;
            //df.defText = ins.defText;
            //df.atkText = ins.atkText;
            df.costText = ins.costText;
            df.levelText = ins.tierText;

            // bookkeeping
            ins.typeOfFigureHolder = df.FIGURE_TYPE;
            instantiated_stat.name = df.FIGURE_NAME + " Stat-Field";

            // immediate baseline UI
            if (ins.nameText) ins.nameText.text = df.FIGURE_NAME;
            if (ins.healthText) ins.healthText.text = df.FIGURE_HEALTH + "HP";
            if (ins.loadText) ins.loadText.text = df.FIGURE_LOAD + "%";
            if (ins.defText) ins.defText.text = df.FIGURE_DEF.ToString();
            if (ins.atkText) ins.atkText.text = df.FIGURE_ATK.ToString(); //HIER
            if (ins.costText) ins.costText.text = df.FIGURE_COST.ToString();
            if (ins.specialcostText) ins.specialcostText.text = df.FIGURE_COST_SPC.ToString();
            if (ins.tierText) ins.tierText.text = df.FIGURE_LEVEL.ToString();

            // bars baseline
            if (ins.HealthBar && df.FIGURE_MAX_HEALTH > 0)
                ins.HealthBar.fillAmount = Mathf.Clamp01(df.FIGURE_HEALTH / (float)df.FIGURE_MAX_HEALTH);
            if (ins.LoadBar)
                ins.LoadBar.fillAmount = Mathf.Clamp01(df.FIGURE_LOAD / 100f);
        }
        else
        {
            if (df == null) Debug.LogWarning("[FigureStatsController] Display_Figure missing on casted_figure.");
            if (ins == null) Debug.LogWarning("[FigureStatsController] FigureStat missing on instantiated_stat.");
        }
    }

    // MIRROR: initial snapshot (we do not bind DF to mirror to avoid double ownership).
    // Continuous syncing is done by a coroutine started after instantiation.
    private void ConnectStatsToFigure_VisualOnly(GameObject casted_figure, GameObject instantiated_stat)
    {
        var df = casted_figure ? casted_figure.GetComponent<Display_Figure>() : null;
        var ins = instantiated_stat ? instantiated_stat.GetComponent<FigureStat>() : null;

        if (df != null && ins != null)
        {
            if (ins.nameText) ins.nameText.text = df.FIGURE_NAME;
            if (ins.healthText) ins.healthText.text = df.FIGURE_HEALTH + "HP";
            if (ins.loadText) ins.loadText.text = df.FIGURE_LOAD + "%";
            if (ins.defText) ins.defText.text = df.FIGURE_DEF.ToString();
            if (ins.atkText) ins.atkText.text = df.FIGURE_ATK.ToString();
            if (ins.costText) ins.costText.text = df.FIGURE_COST.ToString();
            if (ins.specialcostText) ins.specialcostText.text = df.FIGURE_COST_SPC.ToString();
            if (ins.tierText) ins.tierText.text = df.FIGURE_LEVEL.ToString();

            if (ins.HealthBar && df.FIGURE_MAX_HEALTH > 0)
                ins.HealthBar.fillAmount = Mathf.Clamp01(df.FIGURE_HEALTH / (float)df.FIGURE_MAX_HEALTH);
            if (ins.LoadBar)
                ins.LoadBar.fillAmount = Mathf.Clamp01(df.FIGURE_LOAD / 100f);

            ins.typeOfFigureHolder = df.FIGURE_TYPE;
            instantiated_stat.name = df.FIGURE_NAME + " Stat-Field (Mirror)";
        }
        else
        {
            if (df == null) Debug.LogWarning("[FigureStatsController] Display_Figure missing on casted_figure (mirror).");
            if (ins == null) Debug.LogWarning("[FigureStatsController] FigureStat missing on instantiated_stat (mirror).");
        }
    }

    // Loads sign sprite via Resources: "LightBG_Heart", "LightBG_Spade", ...
    private void GenerateSignPicture(string casted_sign, GameObject instantiated_stat)
    {
        var ins = instantiated_stat ? instantiated_stat.GetComponent<FigureStat>() : null;
        if (ins == null || ins.SignImage == null) return;

        string key = casted_sign ?? "";

        if (!_signSpriteCache.TryGetValue(key, out var spr))
        {
            string resPath = null;
            if (casted_sign == "Heart") resPath = signPrefix + "Heart";
            else if (casted_sign == "Spade") resPath = signPrefix + "Spade";
            else if (casted_sign == "Club") resPath = signPrefix + "Club";
            else if (casted_sign == "Diamond") resPath = signPrefix + "Diamond";
            else Debug.LogWarning("[FigureStatsController] Unknown sign: " + casted_sign);

            if (!string.IsNullOrEmpty(resPath))
                spr = Resources.Load<Sprite>(resPath);

            _signSpriteCache[key] = spr; // may be null if missing
        }

        ins.SignImage.sprite = spr;
    }

    private void GeneratePortraitPicture(GameObject casted_figure, GameObject instantiated_stat)
    {
        var df = casted_figure.GetComponent<Display_Figure>();
        var ins = instantiated_stat.GetComponent<FigureStat>();

        ins.FigurePortraitImage.sprite = df.FigurePortraitSprite;
    }


    private string BuildPortraitResourcePath(string key)
    {
        if (string.IsNullOrEmpty(portraitResourcesFolder))
            return key; // allow absolute path if someone wants
        return portraitResourcesFolder.TrimEnd('/') + "/" + key;
    }

    private Sprite LoadPortraitCached(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;
        if (_portraitSpriteCache.TryGetValue(fullPath, out var s)) return s;
        var loaded = Resources.Load<Sprite>(fullPath);
        _portraitSpriteCache[fullPath] = loaded;
        return loaded;
    }

    private string SafeKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Trim().Replace(" ", "_"); // simple normalization; adjust if needed
    }

    // Keeps mirror visuals in lockstep with main by copying values every frame.
    private IEnumerator SyncMirrorContinuous(GameObject mainStat, GameObject mirrorStat)
    {
        var main = mainStat.GetComponent<FigureStat>();
        var mir = mirrorStat.GetComponent<FigureStat>();

        if (main == null || mir == null) yield break;

        var mainHB = main.HealthBar; var mirHB = mir.HealthBar;
        var mainLB = main.LoadBar; var mirLB = mir.LoadBar;

        var mainHT = main.healthText; var mirHT = mir.healthText;
        var mainLT = main.loadText; var mirLT = mir.loadText;
        var mainDT = main.defText; var mirDT = mir.defText;
        var mainAT = main.atkText; var mirAT = mir.atkText;
        var mainCT = main.costText; var mirCT = mir.costText;

        var mainPortrait = main.FigurePortraitImage; var mirPortrait = mir.FigurePortraitImage;
        var mainSign = main.SignImage; var mirSign = mir.SignImage;

        // copy loop � late visual sync
        while (mainStat && mirrorStat)
        {
            if (mainHB && mirHB) mirHB.fillAmount = mainHB.fillAmount;
            if (mainLB && mirLB) mirLB.fillAmount = mainLB.fillAmount;

            if (mainHT && mirHT) mirHT.text = mainHT.text;
            if (mainLT && mirLT) mirLT.text = mainLT.text;
            if (mainDT && mirDT) mirDT.text = mainDT.text;
            if (mainAT && mirAT) mirAT.text = mainAT.text;
            if (mainCT && mirCT) mirCT.text = mainCT.text;

            if (mainPortrait && mirPortrait && mirPortrait.sprite != mainPortrait.sprite)
                mirPortrait.sprite = mainPortrait.sprite;

            if (mainSign && mirSign && mirSign.sprite != mainSign.sprite)
                mirSign.sprite = mainSign.sprite;

            yield return null; // next frame
        }
    }
}