using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSessionEnder : MonoBehaviour
{
    [Header("Optional")]
    [Tooltip("Szene, die nach dem Beenden der Session geladen wird (z. B. MainMenu). Leer = kein Szenenwechsel.")]
    public string returnSceneName;

    /// <summary>
    /// Beendet die aktuelle Game-Session.
    /// Optional: Lädt eine definierte Rückkehr-Szene.
    /// </summary>
    public void EndGameSession()
    {
        // 1) Session-spezifische Daten zurücksetzen
        ResetRuntimeState();

        // 2) Falls Rückkehrszene definiert → laden
        if (!string.IsNullOrEmpty(returnSceneName))
        {
            SceneManager.LoadScene(returnSceneName);
            return;
        }

        // 3) Sonst: Anwendung beenden
        QuitApplication();
    }

    private void ResetRuntimeState()
    {
        // HIER alles zurücksetzen, was eine "Session" ausmacht
        // Beispiele:
        // - Static Singletons
        // - PlayerStats
        // - TurnManager
        // - Match-Flags

        // Beispiel (nur Platzhalter):
        // GameState.Reset();
        // TurnManager.current = null;
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
