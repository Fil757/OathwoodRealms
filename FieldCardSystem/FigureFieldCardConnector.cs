using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FigureFieldCardConnector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject P1_Figures;
    [SerializeField] private GameObject P2_Figures;

    [Header("Controllers")]
    [Tooltip("Controller, der die ActiveFigureFieldEffect-Liste für P1 hält.")]
    [SerializeField] private FieldCardController FieldCardController_P1;

    [Tooltip("Controller, der die ActiveFigureFieldEffect-Liste für P2 hält.")]
    [SerializeField] private FieldCardController_P2 FieldCardController_P2;

    [Header("Runtime Settings")]
    [Tooltip("Wie oft (in Sekunden) die Verbindungen geprüft werden. 0 = kein Loop.")]
    [SerializeField] private float checkInterval = 0.25f;

    private Coroutine _checkRoutine;

    private void OnEnable()
    {
        StartCheckLoop();
    }

    private void OnDisable()
    {
        StopCheckLoop();
    }

    public void StartCheckLoop()
    {
        if (checkInterval <= 0f) return;
        if (_checkRoutine != null) return;

        _checkRoutine = StartCoroutine(CheckLoop());
    }

    public void StopCheckLoop()
    {
        if (_checkRoutine == null) return;

        StopCoroutine(_checkRoutine);
        _checkRoutine = null;
    }

    private IEnumerator CheckLoop()
    {
        // Beim Start einmal sofort prüfen
        CheckConnections();

        var wait = new WaitForSeconds(checkInterval);

        while (true)
        {
            yield return wait;
            CheckConnections();
        }
    }

    /// <summary>
    /// Prüft sowohl P1 als auch P2 und entfernt verwaiste Figure-FieldEffects.
    /// </summary>
    public void CheckConnections()
    {
        // P1
        var p1Controller = ResolveP1Controller();
        if (p1Controller != null)
            CleanupOrphanedEffects_P1(P1_Figures, p1Controller);

        // P2
        var p2Controller = ResolveP2Controller();
        if (p2Controller != null)
            CleanupOrphanedEffects_P2(P2_Figures, p2Controller);
    }

    private FieldCardController ResolveP1Controller()
    {
        if (FieldCardController_P1 != null) return FieldCardController_P1;

        // Optional: wenn du bei P1 weiterhin den Singleton nutzt
        if (FieldCardController.instance != null) return FieldCardController.instance;

        // Fallback (einmalig okay, aber nicht jede Frame suchen)
        return FindObjectOfType<FieldCardController>();
    }

    private FieldCardController_P2 ResolveP2Controller()
    {
        if (FieldCardController_P2 != null) return FieldCardController_P2;

        // Falls du auch bei P2 einen Singleton hast (nur aktivieren, wenn es den wirklich gibt!)
        // if (FieldCardController_P2.instance != null) return FieldCardController_P2.instance;

        // Fallback
        return FindObjectOfType<FieldCardController_P2>();
    }

    // ---------------------------
    // Cleanup P1 (FieldCardController)
    // ---------------------------
    private void CleanupOrphanedEffects_P1(GameObject figuresRoot, FieldCardController controller)
    {
        if (figuresRoot == null) return;
        if (controller == null) return;

        List<FieldCard> activeEffects = controller.ActiveFigureFieldEffect;
        if (activeEffects == null || activeEffects.Count == 0) return;

        HashSet<string> existingFigureIDs = BuildFigureIdSet(figuresRoot);

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            FieldCard effect = activeEffects[i];

            if (effect == null)
            {
                activeEffects.RemoveAt(i);
                continue;
            }

            string id = effect.connection_to_figure_ID;
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (!existingFigureIDs.Contains(id))
            {
                Debug.Log($"[FigureFieldCardConnector] P1 Removed FieldEffect (Figure missing): {id}");
                activeEffects.RemoveAt(i);
            }
        }
    }

    // ---------------------------
    // Cleanup P2 (FieldCardController_P2)
    // ---------------------------
    private void CleanupOrphanedEffects_P2(GameObject figuresRoot, FieldCardController_P2 controller)
    {
        if (figuresRoot == null) return;
        if (controller == null) return;

        List<FieldCard> activeEffects = controller.ActiveFigureFieldEffect;
        if (activeEffects == null || activeEffects.Count == 0) return;

        HashSet<string> existingFigureIDs = BuildFigureIdSet(figuresRoot);

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            FieldCard effect = activeEffects[i];

            if (effect == null)
            {
                activeEffects.RemoveAt(i);
                continue;
            }

            string id = effect.connection_to_figure_ID;
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (!existingFigureIDs.Contains(id))
            {
                Debug.Log($"[FigureFieldCardConnector] P2 Removed FieldEffect (Figure missing): {id}");
                activeEffects.RemoveAt(i);
            }
        }
    }

    // Gemeinsamer Helper: Figure-IDs sammeln
    private HashSet<string> BuildFigureIdSet(GameObject figuresRoot)
    {
        HashSet<string> ids = new HashSet<string>();
        Transform root = figuresRoot.transform;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;

            // Achtung: Name als ID ist anfällig (Clone/Umbenennung).
            ids.Add(child.name);
        }

        return ids;
    }

    [ContextMenu("Check Connections Now")]
    private void CheckConnectionsNow()
    {
        CheckConnections();
    }
}
