using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TodoItem
{
    [Tooltip("Beschreibung der Aufgabe")]
    [TextArea(2, 25)]
    public string description;

    [Tooltip("Ob die Aufgabe erledigt ist")]
    public bool isDone;
}

public class TO_DO_List : MonoBehaviour
{
    public List<TodoItem> todos = new List<TodoItem>();
}