using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneSwitcher : MonoBehaviour
{
    [Tooltip("Wartezeit vor dem Szenenwechsel (Sekunden)")]
    public float delaySeconds = 2f;

    public Toggle tutorialToggle;

    /// <summary>
    /// Wechselt in die angegebene Szene (per Szenenname).
    /// Szene muss in den Build Settings eingetragen sein.
    /// </summary>
    public void SwitchToScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("SceneSwitcher: sceneName ist leer.");
            return;
        }

        StartCoroutine(LoadSceneDelayed(sceneName));
    }

    /// <summary>
    /// Optional: Wechsel per Build-Index
    /// </summary>
    public void SwitchToScene(int buildIndex)
    {
        if (buildIndex < 0)
        {
            Debug.LogWarning("SceneSwitcher: Ungültiger BuildIndex.");
            return;
        }

        GameSessionSettings.tutorialEnabled = tutorialToggle.isOn;

        StartCoroutine(LoadSceneDelayed(buildIndex));
    }

    // ------------------------------------------------------

    private IEnumerator LoadSceneDelayed(string sceneName)
    {
        yield return new WaitForSeconds(delaySeconds);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator LoadSceneDelayed(int buildIndex)
    {
        yield return new WaitForSeconds(delaySeconds);
        SceneManager.LoadScene(buildIndex);
    }
}
