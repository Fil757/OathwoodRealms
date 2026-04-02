using UnityEngine;
using UnityEngine.UI;

public class InformationBoardButton : MonoBehaviour
{
    private Button button;

    public bool isFigureInfo;

    private void Awake()
    {
        // Versuche automatisch den Button auf demselben GameObject zu holen
        button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("InformationBoardButton: Kein Button-Component gefunden!", this);
            return;
        }

        // Listener über eigene Methode registrieren
        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if(isFigureInfo) {InformationBoardHandler.current.Spawn_FigureInfoBoard();}
        else{InformationBoardHandler.current.Spawn_NoNFigureInfoBoard();}
    }
}

