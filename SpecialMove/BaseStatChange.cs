using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BaseStatChange : MonoBehaviour
{
    public static BaseStatChange current;

    [Header("Test")]
    public GameObject FigureToTest;

    private void Awake()
    {
        current = this;
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    public void TestStatChange()
    {
        if (FigureToTest == null)
        {
            Debug.LogWarning("[BaseStatChange] TestStatChange: FigureToTest == null.");
            return;
        }
        ChangeStatInteger(FigureToTest, 20, "Cost");
    }

    /// <summary>
    /// Ändert einen Integer-Stat der Figur (Attack/Defense/Cost) um change_amount.
    /// Werte werden bei 0 geklemmt. UI-Text (falls vorhanden) wird gefärbt und aktualisiert.
    /// </summary>
    public void ChangeStatInteger(GameObject targeted_figure, int change_amount, string StatType)
    {
        // Eingaben prüfen
        if (targeted_figure == null)
        {
            Debug.LogWarning("[BaseStatChange] ChangeStatInteger: targeted_figure == null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(StatType))
        {
            Debug.LogWarning("[BaseStatChange] ChangeStatInteger: StatType ist leer/null.");
            return;
        }

        var display = targeted_figure.GetComponent<Display_Figure>();
        if (display == null)
        {
            Debug.LogWarning($"[BaseStatChange] ChangeStatInteger: Display_Figure fehlt auf '{targeted_figure.name}'.");
            TryPlayParticles(targeted_figure); // optisches Feedback trotzdem versuchen
            return;
        }

        // Case-insensitive Lookup
        string key = StatType.Trim();
        string keyLower = key.ToLowerInvariant();

        // Mappe Stat → (applyChange, uiPath, getCurrentValue)
        var statMap = new Dictionary<string, (Action<int> apply, string path, Func<int> get)>
        {
            { "attack",  ( amount => display.FIGURE_ATK  = Mathf.Max(0, display.FIGURE_ATK  + amount), "Stats/ATKFrame/ATK-Text",  () => display.FIGURE_ATK  ) },
            { "defense", ( amount => display.FIGURE_DEF  = Mathf.Max(0, display.FIGURE_DEF  + amount), "Stats/DEFFrame/DEF-Text",  () => display.FIGURE_DEF  ) },
            { "cost",    ( amount => display.FIGURE_COST = Mathf.Max(0, display.FIGURE_COST + amount), "Stats/SignFrame/Cost-Text", () => display.FIGURE_COST ) }
        };

        if (!statMap.TryGetValue(keyLower, out var entry))
        {
            Debug.LogWarning($"[BaseStatChange] Unbekannter StatType: '{StatType}'. Erlaubt: Attack, Defense, Cost.");
            TryPlayParticles(targeted_figure); // du bekommst trotzdem ein leichtes Feedback
            return;
        }

        // Änderung anwenden (mit Clamp in der Lambda)
        try
        {
            entry.apply(change_amount);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BaseStatChange] applyChange Exception für '{StatType}': {e}");
            return;
        }

        // UI: Text-Komponente am Pfad finden (falls vorhanden), Farbe setzen & Zahl aktualisieren
        var textObj = FindTMPOnPath(targeted_figure.transform, entry.path);
        if (textObj != null)
        {
            // Farbe – an dein Original angelehnt (dunkleres Blau). Optional: bei negativen Werten z. B. rötlich färben.
            //textObj.color = new Color(0.4f, 0.5f, 0.8f, 1f);

            // Zahl direkt aktualisieren, falls es wirklich ein Zahlenfeld ist
            int currentVal = 0;
            bool gotVal = false;
            try { currentVal = entry.get(); gotVal = true; }
            catch { /* ignore */ }

            if (gotVal)
            {
                // Falls z. B. Cost-Text kein reiner Integer ist, passe hier ggf. Format an.
                textObj.text = currentVal.ToString();
            }
        }
        else
        {
            Debug.LogWarning($"[BaseStatChange] TMP Text am Pfad '{entry.path}' nicht gefunden auf '{targeted_figure.name}'.");
        }

        // Partikel-Feedback versuchen (wenn EffectPosition & ParticleController vorhanden)
        TryPlayParticles(targeted_figure);
    }

    // ----------------- Hilfsfunktionen -----------------

    private TextMeshProUGUI FindTMPOnPath(Transform root, string relativePath)
    {
        if (root == null || string.IsNullOrWhiteSpace(relativePath)) return null;
        var t = root.Find(relativePath);
        if (t == null) return null;
        return t.GetComponent<TextMeshProUGUI>();
    }

    private void TryPlayParticles(GameObject target)
    {
        if (target == null) return;

        var effectPos = target.transform.Find("EffectPosition");
        if (effectPos == null)
        {
            Debug.LogWarning($"[BaseStatChange] 'EffectPosition' nicht gefunden auf '{target.name}'. Keine Stat-Change-Partikel.");
            return;
        }

        if (ParticleController.Instance == null)
        {
            Debug.LogWarning("[BaseStatChange] ParticleController.Instance == null. Keine Stat-Change-Partikel.");
            return;
        }

        AudioManager.Instance?.PlaySFX2D("Healing");

        // Deine Originalparameter
        ParticleController.Instance.PlayParticleEffect(
            effectPos.position + new Vector3(0f, 0f, 00f),
            5,
            new Vector3(60f, 60f, 60f),
            Quaternion.Euler(-90f, 0f, 0f)
        );
    }
}

