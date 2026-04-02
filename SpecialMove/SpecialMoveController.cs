using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// SpecialMoveController
//  - Zentraler Manager in der Szene (Singleton-ähnlich)
//  - Spawnt pro SpecialMove eine Runner-Instanz
//  - NEW: Sperrt NUR die Caster-Figur (FigureActionLock), nicht das ganze Spiel
// ============================================================================
public class SpecialMoveController : MonoBehaviour
{
    public static SpecialMoveController current;
    

    public enum ActiveSide { P1, P2 }

    #region Inspector: Globale Referenzen / Kontext

    [Header("Aktive Perspektive")]
    [Tooltip("P1 = Self ist Player1/Figures_P1; P2 = Self ist Player2/Figures_P2")]
    public ActiveSide activeSide = ActiveSide.P1;

    [Header("Figure Arrays (Szene)")]
    public GameObject[] Figures_P1;
    public GameObject[] Figures_P2;

    [Header("Effect Anchors (Fallback)")]
    public GameObject P1_Effect_Position;
    public GameObject P2_Effect_Position;
    public GameObject Central_Effect_Position;

    [Header("Players (für Player-Stats)")]
    public GameObject Player1;
    public GameObject Player2;

    [Header("Integrationen")]
    [SerializeField] private CameraShake cameraShake; // Instanz-Referenz (kein static)

    [Header("Globaler Bezugs-Punkt")]
    [Tooltip("Alle Zielpositionen (Figur & Partikel) werden als Offset relativ zu diesem Transform interpretiert.")]
    public Transform PointZero;

    [Header("Euler-Mirroring Offsets (Particles)")]
    public float euleroffset_multiplikator_x;
    public float euleroffset_multiplikator_y;
    public float euleroffset_multiplikator_z;

    public float euleroffset_extraoffset_x;
    public float euleroffset_extraoffset_y;
    public float euleroffset_extraoffset_z;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        current = this;

        RefreshFiguresFromScene_SM();

        if (cameraShake == null)
            cameraShake = FindObjectOfType<CameraShake>(true);

        // Auto-Find für PointZero, falls nicht gesetzt
        if (PointZero == null)
        {
            var go = GameObject.Find("Game-Canvas/PointZero");
            if (!go) go = GameObject.Find("PointZero");
            PointZero = go ? go.transform : null;
        }
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    private void Update()
    {
        // Stabilisiert die Side-Zuordnung (dein bestehender Workaround)
        if (TurnManager.current != null)
        {
            if (TurnManager.current.activePlayer == "P1") activeSide = ActiveSide.P1;
            if (TurnManager.current.activePlayer == "P2") activeSide = ActiveSide.P2;
        }
    }

    #endregion

    #region Öffentliche API (Spawning)

    /// <summary>
    /// Spiele einen SpecialMove ohne spezifischen Caster (keine Figuren-Sperre).
    /// </summary>
    public void PlaySpecialMove(SpecialMove move)
    {
        if (!move) return;
        SpawnAndRun(move, null, null);
    }

    /// <summary>
    /// Figur spielt ihre Special A (Caster bekannt → Figur wird gesperrt).
    /// </summary>
    public void PlaySpecialMoveFromFigure(GameObject Figure)
    {
        if (!Figure) return;

        var df = Figure.GetComponent<Display_Figure>();
        if (!df || df.FIGURE == null) return;

        SpecialMove move = df.FIGURE.SPECIAL_A;
        if (!move) return;

        // Wenn Figur gelockt ist -> blocken
        if (IsFigureLocked(Figure))
            return;

        // Kostenprüfung
        int cost = move.SPECIALMOVE_LOAD_COST;
        int load = df.FIGURE_LOAD;
        if (load < cost)
        {
            if (Messagebox.current) Messagebox.current.ShowMessageBox();
            return;
        }

        // Kosten zahlen
        df.FIGURE_LOAD -= cost;

        SpawnAndRun(move, Figure, null);
    }

    /// <summary>
    /// Spell von einer DeckCard wird gespielt.
    /// </summary>
    public void Play_SpellFromDeckCard(SpecialMove move)
    {
        if (!move) return;

        // API-CALL an den FieldCardController (FIX: 2. if war bei dir falsch kopiert)
        if (activeSide == ActiveSide.P1)
        {
            FieldCardController.instance?.Player_PlaysDeckSpell("Self", true);
            FieldCardController_P2.instance?.Player_PlaysDeckSpell("Opponent", true);
        }
        else // P2
        {
            FieldCardController.instance?.Player_PlaysDeckSpell("Opponent", true);
            FieldCardController_P2.instance?.Player_PlaysDeckSpell("Self", true);
        }

        SpawnAndRun(move, null, null);
    }

    /// <summary>
    /// Figur spielt Special A/B der aktuellen Figur (Caster = current figure der aktiven Side).
    /// </summary>
    public void Play_SpecialMove_currentFigure_A() => Play_SpecialMove_currentFigure("A");

    public void Play_SpecialMove_currentFigure(string AorB)
    {
        StartCoroutine(Play_SpecialMove_currentFigure_IEnumerator(AorB));
    }

    private IEnumerator Play_SpecialMove_currentFigure_IEnumerator(string AorB)
    {
        var tm = TurnManager.current;
        if (tm == null) yield break;

        var curFig = (activeSide == ActiveSide.P1 ? tm.current_figure_P1 : tm.current_figure_P2);
        if (!curFig) yield break;

        // Wenn Figur gelockt ist -> blocken
        if (IsFigureLocked(curFig))
            yield break;

        var df = curFig.GetComponent<Display_Figure>();
        if (!df) yield break;

        SpecialMove move = null;
        if (AorB == "A") move = df.SPECIAL_A;
        else if (AorB == "B") move = df.SPECIAL_B;

        if (!move) yield break;

        int cost = move.SPECIALMOVE_LOAD_COST;
        int load = df.FIGURE_LOAD;
        if (load < cost)
        {
            if (Messagebox.current) Messagebox.current.ShowMessageBox();
            yield break;
        }

        // Kosten zahlen
        df.FIGURE_LOAD -= cost;
        if (df.LoadBar && LoadBarController.current)
            LoadBarController.current.PayLoad(cost, df.LoadBar);

        // ERST JETZT: Defensive deaktivieren
        //if (AttackController.current != null)
            //AttackController.current.Deactivate_Defense(curFig);

        // Runner erzeugen & laufen lassen (Caster = curFig)
        SpawnAndRun(move, curFig, null);
    }

    /// <summary>
    /// Hülle für zukünftige Figur-Beschwörung aus Deckkarte.
    /// </summary>
    public void Play_FigureFromDeckCard(TCG.DeckCard card)
    {
        if (card == null) return;

        // TODO: Spawn Beschwörungs-Move als Runner, wenn du willst.
    }

    #endregion

    #region Runner-Erzeugung + Per-Figure Lock

    private bool IsFigureLocked(GameObject fig)
    {
        if (!fig) return false;
        var l = fig.GetComponent<FigureActionLock>();
        return l != null && l.IsLocked;
    }

    private FigureActionLock EnsureLock(GameObject fig)
    {
        if (!fig) return null;
        var l = fig.GetComponent<FigureActionLock>();
        if (l == null) l = fig.AddComponent<FigureActionLock>();
        return l;
    }

    /// <summary>
    /// Spawn Runner und starte ihn.
    /// NEW: Sperrt nur die Caster-Figur (falls gesetzt) bis Runner fertig ist.
    /// </summary>
    private void SpawnAndRun(SpecialMove move, GameObject caster, System.Action onFinished)
    {
        if (!move) return;

        // --- Per-Figure Lock setzen ---
        FigureActionLock casterLock = null;
        if (caster != null)
        {
            casterLock = EnsureLock(caster);
            casterLock.Lock();
        }

        // Runner GO erstellen
        var runnerGO = new GameObject("SpecialMoveRunner_" + move.name);
        var runner = runnerGO.AddComponent<SpecialMoveRunner>();

        // Kontext kopieren
        runner.activeSide = this.activeSide;
        runner.MoveToPlay = move;

        runner.Figures_P1 = this.Figures_P1;
        runner.Figures_P2 = this.Figures_P2;

        runner.P1_Effect_Position = this.P1_Effect_Position;
        runner.P2_Effect_Position = this.P2_Effect_Position;
        runner.Central_Effect_Position = this.Central_Effect_Position;

        runner.Player1 = this.Player1;
        runner.Player2 = this.Player2;

        runner.PointZero = this.PointZero;
        runner.cameraShake = this.cameraShake;

        runner.euleroffset_multiplikator_x = this.euleroffset_multiplikator_x;
        runner.euleroffset_multiplikator_y = this.euleroffset_multiplikator_y;
        runner.euleroffset_multiplikator_z = this.euleroffset_multiplikator_z;

        runner.euleroffset_extraoffset_x = this.euleroffset_extraoffset_x;
        runner.euleroffset_extraoffset_y = this.euleroffset_extraoffset_y;
        runner.euleroffset_extraoffset_z = this.euleroffset_extraoffset_z;

        // Optional: Sound des SpecialMoves abspielen
        if (move.SPECIALMOVE_SOUND != null && SoundProfileController.current != null)
            SoundProfileController.current.PlaySound(move.SPECIALMOVE_SOUND);

        // OnFinished: Unlock caster + optional callback
        runner.OnFinished = () =>
        {
            if (casterLock != null)
                casterLock.Unlock();

            onFinished?.Invoke();
        };

        // Start
        runner.Begin();
    }

    #endregion

    #region Utility: Figures aus Szene ziehen

    public void RefreshFiguresFromScene_SM()
    {
        // P1
        {
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
            }
        }

        // P2
        {
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
            }
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (PointZero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(PointZero.position, 0.05f);
        }
    }
#endif
}