using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HealToFigure : MonoBehaviour
{
    public static HealToFigure current;

    private Coroutine flashRoutine;

    // throttle je Image, damit nicht gespammt wird
    private readonly Dictionary<Image, float> flashTimestamps = new();

    private void Awake()
    {
        current = this;
    }

    /// <summary>
    /// Heilt eine Figur um 'healing_amount'.
    /// Bricht sauber ab, wenn Referenzen fehlen.
    /// </summary>
    public void HealingToFigure(GameObject healedfigure, int healing_amount)
    {
        // API-Call an den Fieldcardcontroller
        FieldCardController.instance.Figure_TakenHealing(healedfigure, healing_amount);
        FieldCardController_P2.instance.Figure_TakenHealing(healedfigure, healing_amount);

        // --- Eingaben prüfen ---
        if (healedfigure == null)
        {
            Debug.LogWarning("[HealToFigure] HealingToFigure abgebrochen: healedfigure == null.");
            return;
        }

        if (healing_amount <= 0)
        {
            Debug.LogWarning($"[HealToFigure] HealingToFigure abgebrochen: healing_amount ({healing_amount}) <= 0.");
            return;
        }

        // --- PopUp sicher triggern ---
        Vector3 worldPos = healedfigure.transform.position;

        PopUp.current.FigurePopUp("Healing", healing_amount, healedfigure);

        // --- Display_Figure holen ---
        var figure_script = healedfigure.GetComponent<Display_Figure>();
        if (figure_script == null)
        {
            Debug.LogWarning($"[HealToFigure] Display_Figure fehlt auf '{healedfigure.name}'. Heilung nur visuell (PopUp), keine Werteänderung.");
            // wir machen trotzdem noch Partikel-Versuch:
            TryPlayHealParticles(healedfigure);
            return;
        }

        // --- HealthBar-Image holen (UI) ---
        Image healed_healthbar = figure_script.HealthBar;
        //FindImageOnChildPath(healedfigure.transform, "Health_Load/HealthBar");
        if (healed_healthbar == null)
        {
            Debug.LogWarning($"[HealToFigure] Health_Load/HealthBar Image nicht gefunden auf '{healedfigure.name}'. Nur Werte werden angepasst.");
        }
        else
        {
            // sanftes Flashen; Fehler abfangen in der Methode selbst
            //FlashLightGreen(2.5f, healed_healthbar);
        }

        // --- Zahlenwerte prüfen und anwenden ---
        int healed_maxhealth = figure_script.FIGURE_MAX_HEALTH;
        int healed_currenthealth = figure_script.FIGURE_HEALTH;

        if (healed_maxhealth <= 0)
        {
            Debug.LogWarning($"[HealToFigure] FIGURE_MAX_HEALTH <= 0 auf '{healedfigure.name}'. Abbruch.");
            TryPlayHealParticles(healedfigure);
            return;
        }

        if (healed_currenthealth >= healed_maxhealth)
        {
            // schon voll – nur Partikel/Popup als Feedback
            Debug.Log($"[HealToFigure] '{healedfigure.name}' ist bereits voll geheilt ({healed_currenthealth}/{healed_maxhealth}).");
            TryPlayHealParticles(healedfigure);
            return;
        }

        // Healthbar Controller (UI Balken) – optional
        if (HealthBarController.current != null && healed_healthbar != null)
        {
            // Dein Controller erwartet offenbar ein Delta (negativ = Heilung in deinem Projektsetup)
            HealthBarController.current.ChangeHealthBar(-healing_amount, healed_healthbar, healed_currenthealth, healed_maxhealth);
        }
        else
        {
            if (HealthBarController.current == null)
                Debug.LogWarning("[HealToFigure] HealthBarController.current == null (UI-Balken wird nicht animiert).");
            if (healed_healthbar == null)
                Debug.LogWarning("[HealToFigure] Kein Healthbar-Image vorhanden (UI-Balken wird nicht animiert).");
        }

        // Logische Werte setzen + clamp
        figure_script.FIGURE_HEALTH = Mathf.Min(healed_currenthealth + healing_amount, healed_maxhealth);

        // --- Partikel versuchen ---
        TryPlayHealParticles(healedfigure);
    }

    // --------- VISUELLES FLASHEN ---------

    public void FlashLightGreen(float totalDuration, Image LoadImage)
    {
        if (LoadImage == null)
        {
            Debug.Log("[HealToFigure] FlashLightGreen: LoadImage == null, Abbruch.");
            return;
        }
        if (!isActiveAndEnabled || LoadImage == null || !LoadImage.gameObject.activeInHierarchy)
        {
            // Kein aktives Objekt → nicht flashen
            return;
        }

        // falls bereits eine Routine läuft, nicht zwanghaft abbrechen – jedes Image hat sein eigenes Throttle
        flashRoutine = StartCoroutine(FlashRoutine(totalDuration, LoadImage));
    }

    private IEnumerator FlashRoutine(float totalDuration, Image LoadImage)
    {
        if (LoadImage == null) yield break;

        // throttle (min. 1s Abstand pro Image)
        if (flashTimestamps.TryGetValue(LoadImage, out float lastTime))
        {
            if (Time.time - lastTime < 1f)
                yield break;
        }
        flashTimestamps[LoadImage] = Time.time;

        // Ziel- und Basisfarben behutsam bestimmen (Alpha beibehalten)
        float alpha = LoadImage.color.a;
        Color target = new Color(1f, 1f, 1f, alpha);
        Color baseColor = new Color(0.8f, 0.8f, 0.8f, alpha);

        float half = Mathf.Max(0.001f, totalDuration / 2f);

        // Phase 1: hochblenden
        float t = 0f;
        Color startColor = LoadImage.color;
        while (t < half)
        {
            if (LoadImage == null) yield break; // wurde zerstört
            LoadImage.color = Color.Lerp(startColor, target, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        if (LoadImage == null) yield break;
        LoadImage.color = target;

        // Phase 2: zurückblenden
        t = 0f;
        while (t < half)
        {
            if (LoadImage == null) yield break;
            LoadImage.color = Color.Lerp(target, baseColor, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        if (LoadImage == null) yield break;
        LoadImage.color = baseColor;

        flashRoutine = null;
    }

    // --------- HILFSFUNKTIONEN ---------

    /// <summary>
    /// Sucht ein Image auf einem Kindpfad. Gibt null zurück, wenn Pfad/Komponente fehlt.
    /// </summary>
    public Image FindImageOnChildPath(Transform root, string relativePath)
    {
        if (root == null) return null;
        Transform t = root.Find(relativePath);
        if (t == null) return null;
        return t.GetComponent<Image>();
    }

    /// <summary>
    /// Spielt Heal-Partikel ab, wenn EffectPosition und ParticleController existieren.
    /// </summary>
    private void TryPlayHealParticles(GameObject target)
    {
        if (target == null) return;

        Transform effectPos = target.transform.Find("EffectPosition");
        if (effectPos == null)
        {
            Debug.LogWarning($"[HealToFigure] 'EffectPosition' nicht gefunden auf '{target.name}'. Keine Heal-Partikel.");
            return;
        }

        if (ParticleController.Instance == null)
        {
            Debug.LogWarning("[HealToFigure] ParticleController.Instance == null. Keine Heal-Partikel.");
            return;
        }
        
        AudioManager.Instance?.PlaySFX2D("Healing");

        // Sichere Standardwerte (deine ursprünglichen Parameter)
        Vector3 scale = new Vector3(90f, 90f, 90f);
        Quaternion rot = Quaternion.Euler(-90f, 0f, 0f);

        ParticleController.Instance.PlayParticleEffect(
            effectPos.position,
            2,
            scale,
            rot
        );
    }
}


