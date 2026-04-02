using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartNewGame : MonoBehaviour
{
    public GameObject BlackScreen;
    public GameObject LoadingIcon;

    public float fadeInSeconds = 0.15f;
    public float fadeOutSeconds = 0.25f;
    public float pseudoLoadingSeconds = 4f;

    private bool running;
    private CanvasGroup cg;

    public void StartNewGameButton()
    {
        if (running) return;
        StartCoroutine(Co_ReloadScene());
    }

    private IEnumerator Co_ReloadScene()
    {
        running = true;

        // Overlay
        if (BlackScreen != null)
        {
            if (!BlackScreen.activeSelf) BlackScreen.SetActive(true);

            cg = BlackScreen.GetComponent<CanvasGroup>();
            if (cg == null) cg = BlackScreen.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            cg.blocksRaycasts = true;
        }

        if (LoadingIcon != null) LoadingIcon.SetActive(true);

        // Fade in
        yield return Fade(0f, 1f, Mathf.Max(0.01f, fadeInSeconds));

        // optional pseudo loading
        float end = Time.unscaledTime + pseudoLoadingSeconds;
        while (Time.unscaledTime < end) yield return null;

        // Scene reload
        int idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx);

        // (nach LoadScene ist dieses Objekt weg, außer es ist DontDestroyOnLoad)
        // Wenn es DOoL ist: dann noch Fade out machen (siehe Hinweis unten)
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        if (cg == null) yield break;

        cg.alpha = from;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        cg.alpha = to;
    }
}

