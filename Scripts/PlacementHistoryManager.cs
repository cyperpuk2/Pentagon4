using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PlacementHistoryManager : MonoBehaviour
{
    public struct PentominoAction
    {
        public DraggablePentomino Pentomino;
        public List<Cell> OccupiedCells;
        public Vector3 Position;
        public Quaternion Rotation;
        public List<Vector2Int> Shape;
    }

    private Stack<PentominoAction> _history = new();
    private GridGenerator _gridGenerator;

    void Awake()
    {
        _gridGenerator = FindFirstObjectByType<GridGenerator>();
    }

    public void RecordPlacement(DraggablePentomino pentomino)
    {
        if (pentomino == null) return;
        Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
        if (pentominoComponent == null) return;
        PentominoAction action = new PentominoAction
        {
            Pentomino = pentomino,
            OccupiedCells = new List<Cell>(pentominoComponent.currentOccupiedCells),
            Position = pentomino.transform.position,
            Rotation = pentomino.transform.rotation,
            Shape = new List<Vector2Int>(pentominoComponent.shape)
        };
        _history.Push(action);
    }

    public void UndoLastPlacement()
    {
        if (_history.Count == 0)
            return;
        PentominoAction lastAction = _history.Pop();
        if (lastAction.Pentomino == null)
            return;
        Pentomino pentominoComponent = lastAction.Pentomino.GetComponent<Pentomino>();
        if (pentominoComponent == null)
            return;
        foreach (Cell cell in pentominoComponent.currentOccupiedCells)
            if (cell != null && !cell.isObstacle)
                cell.isOccupied = false;
        pentominoComponent.currentOccupiedCells.Clear();
        bool isLastAction = _history.Count == 0 || !_history.Any(action => action.Pentomino == lastAction.Pentomino);
        if (isLastAction)
        {
            lastAction.Pentomino.transform.position = lastAction.Pentomino.startPosition;
            lastAction.Pentomino.transform.rotation = lastAction.Pentomino.startRotation;
            pentominoComponent.shape = new List<Vector2Int>(pentominoComponent.Shape);
            pentominoComponent.GenerateShape();
        }
        else
        {
            lastAction.Pentomino.transform.position = lastAction.Position;
            lastAction.Pentomino.transform.rotation = lastAction.Rotation;
            pentominoComponent.shape = new List<Vector2Int>(lastAction.Shape);
            pentominoComponent.GenerateShape();
            float gridUnit = lastAction.Pentomino.cellSize + lastAction.Pentomino.cellSpacing;
            float startX = -((_gridGenerator.width - 1) * gridUnit) / 2f;
            float endX = ((_gridGenerator.width - 1) * gridUnit) / 2f;
            float startY = -((_gridGenerator.height - 1) * gridUnit) / 2f;
            float endY = ((_gridGenerator.height - 1) * gridUnit) / 2f;
            if (lastAction.Position.x >= startX && lastAction.Position.x <= endX && lastAction.Position.y >= startY && lastAction.Position.y <= endY)
                lastAction.Pentomino.GetComponent<DraggablePentomino>().SnapToGrid();
            if (lastAction.OccupiedCells != null)
            {
                foreach (Cell cell in lastAction.OccupiedCells)
                    if (cell != null && !cell.isObstacle)
                        cell.isOccupied = true;
                pentominoComponent.currentOccupiedCells = lastAction.OccupiedCells;
            }
        }
        if (_gridGenerator != null)
            _gridGenerator.UpdateNumbers();
    }

    public void ResetToInitialPositions()
    {
        DraggablePentomino[] allPentominoes = FindObjectsOfType<DraggablePentomino>();
        foreach (var pentomino in allPentominoes)
        {
            Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
            if (pentominoComponent != null)
            {
                foreach (Cell cell in pentominoComponent.currentOccupiedCells)
                    if (cell != null && !cell.isObstacle)
                        cell.isOccupied = false;
                pentominoComponent.currentOccupiedCells.Clear();
            }
            pentomino.transform.position = pentomino.startPosition;
            pentomino.transform.rotation = pentomino.startRotation;
            pentominoComponent.shape = new List<Vector2Int>(pentominoComponent.Shape);
            pentominoComponent.GenerateShape();
        }
        _history.Clear();
        if (_gridGenerator != null)
            _gridGenerator.UpdateNumbers();
    }
}