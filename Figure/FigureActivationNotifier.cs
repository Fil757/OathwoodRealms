using UnityEngine;

public class FigureActivationNotifier : MonoBehaviour
{
    public enum Side { P1, P2 }
    public Side side = Side.P1;

    private void OnEnable()
    {
        // Aktivierungen sind unkritisch, hier dürfen wir direkt gehen (für Auto-Select).
        if (TurnManager.current == null) return;

        if (side == Side.P1)
            TurnManager.current.OnP1FigureActivated(gameObject);
        else
            TurnManager.current.OnP2FigureActivated(gameObject);
    }

    private void OnDisable()
    {
        // WICHTIG: während OnDisable wird die Hierarchie verändert -> nur deferred refresh!
        var tm = TurnManager.current;
        if (tm == null) return;

        // Keine unmittelbare Deaktivierungs-Logik hier – nur refresh anfordern.
        tm.RequestRefreshFiguresDeferred();
    }
}

