using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LoadToFigure : MonoBehaviour
{
    public static LoadToFigure current;

    private Coroutine flashRoutine;

    // throttle je Image, damit nicht gespammt wird
    private Dictionary<Image, float> flashTimestamps = new Dictionary<Image, float>();

    private void Awake()
    {
        current = this;
    }

    /// <summary>
    /// Lädt/entlädt die Figur (0..100%) um 'load_amount' (kann negativ sein).
    /// Bricht sauber ab, wenn Referenzen fehlen.
    /// </summary>
    public void LoadingToFigure(GameObject loaded_Figure, int load_amount)
    {
        // API - Call an den FieldCardController
        FieldCardController.instance.Figure_TakenLoad(loaded_Figure, load_amount);
        FieldCardController_P2.instance.Figure_TakenLoad(loaded_Figure, load_amount);

        // --- Eingaben prüfen ---
        if (!loaded_Figure)
        {
            Debug.LogWarning("[LoadToFigure] LoadingToFigure abgebrochen: loaded_Figure == null.");
            return;
        }

        if (load_amount == 0)
        {
            Debug.Log("[LoadToFigure] load_amount == 0, keine Änderung.");
            return;
        }

        // --- Komponenten/Referenzen besorgen ---
        var figure_script = loaded_Figure.GetComponent<Display_Figure>();
        if (figure_script == null)
        {
            Debug.LogWarning($"[LoadToFigure] Display_Figure fehlt auf '{loaded_Figure.name}'. Nur visuelles Feedback (falls möglich).");
            // Wir zeigen trotzdem PopUp/Partikel, sofern die Anker existieren:
            //TryCreateLoadPopup(loaded_Figure, load_amount);
            TryPlayLoadParticles(loaded_Figure, load_amount);
            return;
        }

        // LoadBar-Image (UI)
        Image loadBarImage = figure_script.LoadBar;
        //FindImageOnChildPath(figure_script.ConnectedStatField.transform, "LoadBar");

        // --- Werteänderung + UI-Controller (wenn vorhanden) ---
        int currentLoad = figure_script.FIGURE_LOAD;
        int newLoad = Mathf.Clamp(currentLoad + load_amount, 0, 100);


        figure_script.FIGURE_LOAD = newLoad;

        // --- Popup & Partikel (sicher) ---
        TryCreateLoadPopup(loaded_Figure, load_amount);
        TryPlayLoadParticles(loaded_Figure, load_amount);
    }

    // --------- VISUELLES FLASHEN ---------

    public void FlashLightBlue(float totalDuration, Image LoadImage)
    {
        if (!LoadImage)
        {
            Debug.Log("[LoadToFigure] FlashLightBlue: LoadImage == null, Abbruch.");
            return;
        }
        if (!isActiveAndEnabled || !LoadImage.gameObject.activeInHierarchy)
        {
            // Komponente/Objekt nicht aktiv → nicht flashen
            return;
        }

        flashRoutine = StartCoroutine(FlashRoutine(totalDuration, LoadImage));
    }

    private IEnumerator FlashRoutine(float totalDuration, Image LoadImage)
    {
        if (!LoadImage) yield break;

        // throttle (min. 1s Abstand pro Image)
        float lastTime;
        if (flashTimestamps.TryGetValue(LoadImage, out lastTime))
        {
            if (Time.time - lastTime < 1f)
                yield break;
        }
        flashTimestamps[LoadImage] = Time.time;

        float alpha = LoadImage.color.a;
        Color target = new Color(1f, 1f, 1f, alpha);
        Color baseColor = new Color(0.8f, 0.8f, 0.8f, alpha);

        float half = Mathf.Max(0.001f, totalDuration / 2f);

        // Phase 1: hochblenden
        float t = 0f;
        Color startColor = LoadImage.color;
        while (t < half)
        {
            if (!LoadImage) yield break;
            LoadImage.color = Color.Lerp(startColor, target, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        if (!LoadImage) yield break;
        LoadImage.color = target;

        // Phase 2: zurückblenden
        t = 0f;
        while (t < half)
        {
            if (!LoadImage) yield break;
            LoadImage.color = Color.Lerp(target, baseColor, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        if (!LoadImage) yield break;
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
    /// Erstellt das Load/Unload-Popup sicher (benötigt EffectPosition & LoadPopUp.current).
    /// </summary>
    private void TryCreateLoadPopup(GameObject target, int load_amount)
    {
        PopUp.current.FigurePopUp("Loading", load_amount, target);
    }

    /// <summary>
    /// Spielt Load/Unload-Partikel ab, wenn EffectPosition & ParticleController existieren.
    /// </summary>
    private void TryPlayLoadParticles(GameObject target, int load_amount)
    {
        if (!target) return;

        Transform effectPos = target.transform.Find("EffectPosition");
        if (effectPos == null)
        {
            Debug.LogWarning($"[LoadToFigure] 'EffectPosition' nicht gefunden auf '{target.name}'. Keine Load-Partikel.");
            return;
        }

        if (ParticleController.Instance == null)
        {
            Debug.LogWarning("[LoadToFigure] ParticleController.Instance == null. Keine Load-Partikel.");
            return;
        }

        // 0: Load, 4: Unload (deine Konvention)
        int index = (load_amount < 0) ? 4 : 0;

        AudioManager.Instance?.PlaySFX2D("Loading");

        ParticleController.Instance.PlayParticleEffect(
            effectPos.position + new Vector3(0f, 0f, 0f),
            index,
            new Vector3(90f, 90f, 90f),
            Quaternion.Euler(-90f, 0f, 0f)
        );
    }
}

