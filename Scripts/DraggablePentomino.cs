using UnityEngine;
using System.Collections.Generic;

public class DraggablePentomino : MonoBehaviour
{
    public GridGenerator gridGenerator;
    public PlacementHistoryManager historyManager;
    private GameStatsManager _statsManager;

    private Vector3 _offset;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private bool _isDragging;

    public float cellSize = 1f;
    public float cellSpacing = 0.1f;

    [HideInInspector]
    public Vector3 startPosition;
    [HideInInspector]
    public Quaternion startRotation;

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        if (gridGenerator == null)
            gridGenerator = FindFirstObjectByType<GridGenerator>();
        if (historyManager == null)
            historyManager = FindFirstObjectByType<PlacementHistoryManager>();
        _statsManager = FindFirstObjectByType<GameStatsManager>();
    }

    void OnMouseDown()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _offset = transform.position - new Vector3(mouseWorldPos.x, mouseWorldPos.y, 0);
        _isDragging = true;
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    void OnMouseDrag()
    {
        if (!_isDragging) return;
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(mouseWorldPos.x, mouseWorldPos.y, 0) + _offset;
    }

    void OnMouseUp()
    {
        _isDragging = false;
        SnapToGrid();
        if (transform.position != _initialPosition || transform.rotation != _initialRotation)
        {
            if (historyManager != null)
                historyManager.RecordPlacement(this);
            if (_statsManager != null && _statsManager.IsGameActive)
                _statsManager.IncrementMove();
            if (gridGenerator != null)
                gridGenerator.UpdateNumbers();
        }
    }

    public void SnapToGrid()
    {
        float gridUnit = cellSize + cellSpacing;
        float startX = -((gridGenerator.width - 1) * gridUnit) / 2f;
        float startY = -((gridGenerator.height - 1) * gridUnit) / 2f;
        Vector2 gridOrigin = new Vector2(startX, startY);

        Pentomino pentomino = GetComponent<Pentomino>();
        if (pentomino == null)
            return;

        List<Cell> previouslyOccupied = new List<Cell>(pentomino.currentOccupiedCells);
        foreach (Cell cell in previouslyOccupied)
            if (cell != null && !cell.isObstacle)
                cell.isOccupied = false;
        pentomino.currentOccupiedCells.Clear();

        float localX = (transform.position.x - gridOrigin.x) / gridUnit;
        float localY = (transform.position.y - gridOrigin.y) / gridUnit;
        float snappedX = Mathf.Round(localX) * gridUnit + gridOrigin.x;
        float snappedY = Mathf.Round(localY) * gridUnit + gridOrigin.y;
        Vector3 snappedOrigin = new Vector3(snappedX, snappedY, transform.position.z);

        List<Cell> futureCells = new List<Cell>();
        HashSet<Vector2> forbiddenPositions = new HashSet<Vector2>
        {
            new (6.05f, -7.15f), new (4.95f, -7.15f), new (3.85f, -7.15f), new (2.75f, -7.15f),
            new (1.65f, -7.15f), new (0.5499998f, -7.15f), new (-0.5500002f, -7.15f), new (-1.65f, -7.15f),
            new (-2.75f, -7.15f), new (-3.85f, -7.15f), new (-4.95f, -7.15f), new (-6.05f, -7.15f),
            new (-7.15f, 6.05f), new (-7.15f, 4.95f), new (-7.15f, 3.85f), new (-7.15f, 2.75f),
            new (-7.15f, 1.65f), new (-7.15f, 0.5499998f), new (-7.15f, -0.5500002f), new (-7.15f, -1.65f),
            new (-7.15f, -2.75f), new (-7.15f, -3.85f), new (-7.15f, -4.95f), new (-7.15f, -6.05f)
        };

        foreach (Vector2Int cellOffset in pentomino.Shape)
        {
            Vector3 cellWorldPos = snappedOrigin + new Vector3(cellOffset.x * gridUnit, cellOffset.y * gridUnit, 0);
            Vector2 checkPos = new Vector2(cellWorldPos.x, cellWorldPos.y);
            foreach (var forbidden in forbiddenPositions)
                if (Vector2.Distance(checkPos, forbidden) < 0.1f)
                {
                    RollbackPosition(previouslyOccupied);
                    return;
                }

            int gridX = Mathf.RoundToInt((cellWorldPos.x - startX) / gridUnit);
            int gridY = Mathf.RoundToInt((cellWorldPos.y - startY) / gridUnit);
            Cell targetCell = gridGenerator.GetCellAt(gridX, gridY);
            if (targetCell == null || targetCell.isObstacle || targetCell.isOccupied)
            {
                RollbackPosition(previouslyOccupied);
                return;
            }
            futureCells.Add(targetCell);
        }

        foreach (Cell cell in futureCells)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Cell neighbor = gridGenerator.GetCellAt(cell.x + dx, cell.y + dy);
                    if (neighbor != null && neighbor.isOccupied && !futureCells.Contains(neighbor))
                    {
                        RollbackPosition(previouslyOccupied);
                        return;
                    }
                }
            }
        }

        transform.position = snappedOrigin;
        pentomino.UpdateOccupiedCells(snappedOrigin, gridUnit, startX, startY, gridGenerator);
    }

    void Update()
    {
        if (_isDragging && Input.GetKeyDown(KeyCode.R))
        {
            Pentomino pentomino = GetComponent<Pentomino>();
            if (pentomino != null)
            {
                if (historyManager != null)
                    historyManager.RecordPlacement(this);
                if (_statsManager != null && _statsManager.IsGameActive)
                    _statsManager.IncrementMove();
                pentomino.Rotate();
                SnapToGrid();
                if (gridGenerator != null)
                    gridGenerator.UpdateNumbers();
            }
        }
    }

    private void RollbackPosition(List<Cell> previouslyOccupied)
    {
        foreach (Cell cell in previouslyOccupied)
            if (cell != null && !cell.isObstacle)
                cell.isOccupied = true;
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;
        Pentomino pentomino = GetComponent<Pentomino>();
        pentomino.currentOccupiedCells = previouslyOccupied;
    }
}