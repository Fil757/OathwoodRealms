using UnityEngine;

public class FigureButtonManager : MonoBehaviour
{
    public static FigureButtonManager Instance { get; private set; }

    private FigureButtons _current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Optional: DontDestroyOnLoad(gameObject);
    }

    public void ActivateButtons(FigureButtons target)
    {
        if (target == null)
            return;

        if (_current == target)
        {
            // Schon aktiv → ggf. nichts tun oder toggeln
            return;
        }

        // Altes Feld schließen
        if (_current != null)
        {
            _current.DeactivateButtons();
        }

        _current = target;
        _current.ActivateButtons();

        TutorialHintManager.current.Show_EachFigureButton(1f);
    }

    public void DeactivateCurrent()
    {
        if (_current != null)
        {
            _current.DeactivateButtons();
            _current = null;
        }
    }
}

