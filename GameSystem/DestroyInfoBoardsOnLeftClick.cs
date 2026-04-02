using UnityEngine;

public class DestroyInfoBoardsOnLeftClick : MonoBehaviour
{
    private const string FIGURE_BOARD = "FigureInformationBoard(Clone)";
    private const string FIELD_BOARD  = "FieldSpellTrapInformationBoard(Clone)";

    private void Update()
    {
        // Nur Linksklick
        if (Input.GetMouseButtonDown(0))
        {
            DestroyIfExists(FIGURE_BOARD);
            DestroyIfExists(FIELD_BOARD);
        }
    }

    private void DestroyIfExists(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null)
            Destroy(obj);
    }
}
