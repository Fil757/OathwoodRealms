using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TargetingSystem : MonoBehaviour
{
    public static TargetingSystem current;

    void Awake()
    {
        current = this;
    }
    // --------------- Neu: Standard-Delay & Optionen ---------------
    [Header("Target-Verknüpfung")]
    [Tooltip("Zeitversatz (Sekunden) bis die Verknüpfung gesetzt wird.")]
    public float connectDelaySeconds = 2.59f;

    [Tooltip("Unscaled Time für das Delay nutzen (z. B. bei Pausen).")]
    public bool useUnscaledTime = false;

    // Pro targeting-Figur die gerade geplante (verzögerte) Verknüpfung merken
    private readonly Dictionary<GameObject, Coroutine> _pendingConnections = new();

    // ---------------- Dein bestehender Code ----------------
    public GameObject Segments;

    public string RandomSignChoice()
    {
        string[] Signs = { "Heart", "Spade", "Club" };
        int rand_num = UnityEngine.Random.Range(0, Signs.Length);
        return Signs[rand_num];
    }

    public void AnimateRandomSignChoice(string chosenSign)
    {
        var sq_anim_script = Segments.GetComponent<SegmentSequenceAnimator>();
        int chosen_index = 0;

        switch (chosenSign)
        {
            case "Heart":  chosen_index = 0; break;
            case "Spade":  chosen_index = 1; break;
            case "Club":   chosen_index = 2; break;
            default:       chosen_index = -1; break;
        }

        sq_anim_script.forcedSelectIndex = chosen_index;
        Segments.SetActive(true);
    }

    // ------------------- NEU: Sign-Target für Caster-Seite -------------------
    /// <summary>
    /// Ermittelt aus Sicht des Casters (P1 oder P2) eine Gegner-Figur mit dem gewünschten Sign.
    /// </summary>
    public GameObject TargetFigureChoiceFor(GameObject caster, string chosenSign)
    {
        if (!TurnManager.current) return null;
        if (!caster) return null;

        bool casterIsP1 = caster == TurnManager.current.current_figure_P1;
        var enemyArray = casterIsP1 ? TurnManager.current.Figures_P2
                                    : TurnManager.current.Figures_P1;

        if (enemyArray == null || enemyArray.Length == 0) return null;

        for (int i = 0; i < enemyArray.Length; i++)
        {
            var enemy = enemyArray[i];
            if (!enemy) continue;

            var df = enemy.GetComponent<Display_Figure>();
            if (!df) continue;

            if (df.FIGURE_TYPE == chosenSign)
                return enemy;
        }
        return null;
    }

    // ---------------- Neu: verzögertes Verbinden ----------------
    /// <summary>
    /// Startet eine verzögerte Verknüpfung. Bricht vorherige geplante Verknüpfung
    /// für dieselbe targeting-Figur ab (falls vorhanden).
    /// </summary>
    public void ConnectFigureWithTargetDelayed(GameObject targetingFig, GameObject targetedFig, float? delayOverride = null)
    {
        if (targetingFig == null) return;

        // Falls für diese Figur schon ein Delay läuft: abbrechen
        if (_pendingConnections.TryGetValue(targetingFig, out var running) && running != null)
        {
            StopCoroutine(running);
            _pendingConnections[targetingFig] = null;
        }

        float delay = delayOverride.HasValue ? Mathf.Max(0f, delayOverride.Value) : Mathf.Max(0f, connectDelaySeconds);
        var co = StartCoroutine(CoConnectAfterDelay(targetingFig, targetedFig, delay));
        _pendingConnections[targetingFig] = co;
    }

    private IEnumerator CoConnectAfterDelay(GameObject targetingFig, GameObject targetedFig, float delay)
    {
        // Delay (scaled oder unscaled)
        if (delay > 0f)
        {
            if (useUnscaledTime)
            {
                float t = 0f;
                while (t < delay)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(delay);
            }
        }

        // Safety-Rechecks nach dem Delay
        if (targetingFig == null) yield break;           // Figur zerstört/abgeworfen?
        if (this == null || !gameObject.activeInHierarchy) yield break;

        // Nur verbinden, wenn die Figur noch die aktuelle Figur ihrer Seite ist
        if (TurnManager.current != null)
        {
            bool stillCurrentP1 = TurnManager.current.current_figure_P1 == targetingFig;
            bool stillCurrentP2 = TurnManager.current.current_figure_P2 == targetingFig;

            if (!stillCurrentP1 && !stillCurrentP2)
                yield break; // Caster hat sich geändert
        }

        // Jetzt wirklich verbinden
        ConnectFigureWithTarget(targetingFig, targetedFig);

        // Cleanup
        if (_pendingConnections.ContainsKey(targetingFig))
            _pendingConnections[targetingFig] = null;
    }

    public void ConnectFigureWithTarget(GameObject targetingFig, GameObject targetedFig)
    {
        if (targetingFig == null) return;

        // Caster
        var casterDf = targetingFig.GetComponent<Display_Figure>();
        if (casterDf == null) return;

        // 1) Caster weiß, wen er targetet
        casterDf.FIGURE_TARGET = targetedFig;

        // 2) Ziel weiß, von wem es getargetet wird
        if (targetedFig != null)
        {
            var targetDf = targetedFig.GetComponent<Display_Figure>();
            if (targetDf != null)
            {
                targetDf.FIGURE_BEING_TARGETED_BY = targetingFig;
            }
        }

        AnimateLineConnection(targetingFig, targetedFig);
    }

    // NEU: Zielwahl über explizite Side (robust für Multi-Caster Spells)
    public GameObject TargetFigureChoiceForSide(SpecialMoveController.ActiveSide casterSide, string chosenSign)
    {
        if (!TurnManager.current) return null;

        // Gegner-Array eindeutig über Side bestimmen
        var enemyArray = (casterSide == SpecialMoveController.ActiveSide.P1)
            ? TurnManager.current.Figures_P2
            : TurnManager.current.Figures_P1;

        if (enemyArray == null || enemyArray.Length == 0) return null;

        for (int i = 0; i < enemyArray.Length; i++)
        {
            var enemy = enemyArray[i];
            if (!enemy) continue;

            var df = enemy.GetComponent<Display_Figure>();
            if (!df) continue;

            if (df.FIGURE_TYPE == chosenSign)
                return enemy;
        }

        return null;
    }

    public void AnimateLineConnection(GameObject targetingFig, GameObject targetedFig)
    {
        if (targetedFig == null)
        {
            Debug.Log("Zielfigur nicht auf dem Spielfeld - Targeting abgebrochen");
            return;
        }

        // Start FX beim Caster
        ParticleController.Instance.PlayParticleEffect(
            targetingFig.transform.Find("Model/3D-Model").transform.position + new Vector3(0f, 0f, 50f),
            1,
            new Vector3(60f, 60f, 60f),
            Quaternion.Euler(-90f, 0f, 0f));

        // Zentraler Impuls
        ParticleController.Instance.PlayParticleEffect(
            GameObject.Find("Game-Canvas/CentralEffectPosition").transform.position + new Vector3(0f, 0f, 0f),
            6,
            new Vector3(50f, 50f, 50f),
            Quaternion.Euler(-90f, 0f, 0f));

        // Ziel FX
        ParticleController.Instance.PlayParticleEffect(
            targetedFig.transform.Find("Model/3D-Model").transform.position + new Vector3(0f, 0f, 50f),
            1,
            new Vector3(60f, 60f, 60f),
            Quaternion.Euler(-90f, 0f, 0f));


        AudioManager.Instance?.PlaySFX2D("Cast_Figure");
        AudioManager.Instance?.PlaySFX2D("Cast_Figure_Punch");
        CameraShake.current.Shake(0.5f, 1f);
    }

    // ---------------- Komfort: Ablauf für aktuelle Figur (P1/P2) ----------------
    public void RunAutoTargetingForCurrentP1()
    {
        var currentP1 = TurnManager.current?.current_figure_P1;
        if (currentP1 == null) return;

        var df = currentP1.GetComponent<Display_Figure>();

        //kostenprüfung
        int cost = df.FIGURE_COST;
        int load = df.FIGURE_LOAD; // z. B. Player-Base.Display_Figure.FIGURE_LOAD

        // Richtig: zu wenig Load wenn load < cost
        if (load < cost)
        {
            Debug.Log($"Not enough Load. Cost={cost}, FigureLoad={load}");
            Messagebox.current.ShowMessageBox();
            return;
        }

        Image loadBar = df.LoadBar;
        LoadBarController.current.PayLoad(cost, loadBar);
        df.FIGURE_LOAD -= cost;

        string sign = RandomSignChoice();
        AnimateRandomSignChoice(sign);

        GameObject target = TargetFigureChoiceFor(currentP1, sign); // P1 → Gegner P2

        if (target == null)
        {
            TutorialHintManager.current
                ?.Show_TargetingFailsSign(3.2f);
            return;
        }

        ConnectFigureWithTargetDelayed(currentP1, target);
    }

    public void RunAutoTargetingForCurrentP2()
    {
        var currentP2 = TurnManager.current?.current_figure_P2;
        if (currentP2 == null) return;

        var df = currentP2.GetComponent<Display_Figure>();

        //kostenprüfung
        int cost = df.FIGURE_COST;
        int load = df.FIGURE_LOAD; // z. B. Player-Base.Display_Figure.FIGURE_LOAD

        // Richtig: zu wenig Load wenn load < cost
        if (load < cost)
        {
            Debug.Log($"Not enough Load. Cost={cost}, FigureLoad={load}");
            Messagebox.current.ShowMessageBox();
            return;
        }

        Image loadBar = df.LoadBar;
        LoadBarController.current.PayLoad(cost, loadBar);
        df.FIGURE_LOAD -= cost;

        string sign = RandomSignChoice();
        AnimateRandomSignChoice(sign);

        GameObject target = TargetFigureChoiceFor(currentP2, sign); // P2 → Gegner P1
        ConnectFigureWithTargetDelayed(currentP2, target);
    }

    public void RunAutoTargeting(GameObject caster)
    {
        if (caster == null) return;
        if (TurnManager.current == null) return;

        var df = caster.GetComponent<Display_Figure>();
        if (df == null) return;

        bool isP1 = TurnManager.current.current_figure_P1 == caster;
        bool isP2 = TurnManager.current.current_figure_P2 == caster;

        if (!isP1 && !isP2)
        {
            Debug.LogWarning("Caster ist nicht aktuelle Figur von P1 oder P2 – AutoTargeting abgebrochen.");
            return;
        }

        // Kostenprüfung
        int cost = df.FIGURE_COST;
        int load = df.FIGURE_LOAD;

        if (load < cost)
        {
            Debug.Log($"Not enough Load. Cost={cost}, FigureLoad={load}");
            Messagebox.current.ShowMessageBox();
            return;
        }

        // Load zahlen
        Image loadBar = df.LoadBar;
        LoadBarController.current.PayLoad(cost, loadBar);
        df.FIGURE_LOAD -= cost;

        // Sign wählen & animieren
        string sign = RandomSignChoice();
        AnimateRandomSignChoice(sign);

        // Ziel bestimmen
        GameObject target = TargetFigureChoiceFor(caster, sign);

        // Verzögert verbinden
        ConnectFigureWithTargetDelayed(caster, target);
    }


}
