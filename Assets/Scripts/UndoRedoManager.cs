// Assets/Scripts/UndoRedoManager.cs
using System.Collections.Generic;
using UnityEngine;

public class UndoRedoManager : MonoBehaviour
{
    private Stack<Transform> undoStack = new Stack<Transform>();
    private Stack<Transform> redoStack = new Stack<Transform>();

    public void RecordStroke(Transform stroke)
    {
        undoStack.Push(stroke);
        redoStack.Clear(); // Clear redo when new action is performed
    }

    public void Undo()
    {
        if (undoStack.Count > 0)
        {
            var stroke = undoStack.Pop();
            stroke.gameObject.SetActive(false);
            redoStack.Push(stroke);
        }
    }

    public void Redo()
    {
        if (redoStack.Count > 0)
        {
            var stroke = redoStack.Pop();
            stroke.gameObject.SetActive(true);
            undoStack.Push(stroke);
        }
    }

    public void ClearHistory()
    {
        undoStack.Clear();
        redoStack.Clear();
    }
}