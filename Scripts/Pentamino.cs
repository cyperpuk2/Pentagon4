using UnityEngine;
using System.Collections.Generic;

public class Pentomino : MonoBehaviour
{
    public GameObject pentominoCellPrefab;
    public List<Vector2Int> shape = new();
    public List<Vector2Int> Shape => shape;

    [SerializeField] private float cellSpacing = 0.1f;
    private float CellSize => 1f + cellSpacing;

    public List<Cell> currentOccupiedCells = new();
    public GridGenerator gridGenerator;
    public int index;

    void Start()
    {
        if (shape != null && shape.Count > 0)
            GenerateShape();

        if (gridGenerator == null)
            gridGenerator = FindFirstObjectByType<GridGenerator>();
    }

    public void GenerateShape()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (pentominoCellPrefab == null)
            return;

        foreach (Vector2Int pos in shape)
        {
            GameObject cell = Instantiate(pentominoCellPrefab, transform);
            cell.transform.localPosition = new Vector3(pos.x * CellSize, pos.y * CellSize, 0);
            cell.transform.localScale = Vector3.one;
        }
    }

    public void Rotate()
    {
        for (int i = 0; i < shape.Count; i++)
        {
            int x = shape[i].x;
            int y = shape[i].y;
            shape[i] = new Vector2Int(-y, x);
        }
        GenerateShape();
    }

    public void Initialize(List<Vector2Int> newShape)
    {
        shape = newShape;
        index = transform.GetSiblingIndex();
        GenerateShape();
    }

    public void UpdateOccupiedCells(Vector3 snappedOrigin, float gridUnit, float startX, float startY, GridGenerator newGridGenerator)
    {
        currentOccupiedCells.Clear();
        foreach (Vector2Int cellOffset in shape)
        {
            Vector3 cellWorldPos = snappedOrigin + new Vector3(cellOffset.x * gridUnit, cellOffset.y * gridUnit, 0);
            int gridX = Mathf.RoundToInt((cellWorldPos.x - startX) / gridUnit);
            int gridY = Mathf.RoundToInt((cellWorldPos.y - startY) / gridUnit);
            Cell gridCell = newGridGenerator.GetCellAt(gridX, gridY);
            if (gridCell != null)
            {
                gridCell.isOccupied = true;
                currentOccupiedCells.Add(gridCell);
            }
        }
    }

    public List<List<Vector2Int>> GetAllRotations()
    {
        HashSet<string> seen = new HashSet<string>(); 
        List<List<Vector2Int>> result = new List<List<Vector2Int>>();
        for (int rot = 0; rot < 4; rot++) 
        {
            List<Vector2Int> transformed = TransformShape(shape, rot);
            List<Vector2Int> normalized = Normalize(transformed);
            string hash = GetShapeHash(normalized);
            if (!seen.Contains(hash))
            {
                seen.Add(hash);
                result.Add(normalized);
            }
        }
        return result;
    }

    private List<Vector2Int> TransformShape(List<Vector2Int> original, int rotation)
    {
        List<Vector2Int> transformed = new List<Vector2Int>();
        foreach (var point in original)
        {
            Vector2Int p = point;
            for (int i = 0; i < rotation; i++)
                p = new Vector2Int(-p.y, p.x); 
            transformed.Add(p);
        }
        return transformed;
    }

    private List<Vector2Int> Normalize(List<Vector2Int> newShape)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        foreach (var p in newShape)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
        }
        List<Vector2Int> norm = new List<Vector2Int>();
        foreach (var p in newShape)
            norm.Add(new Vector2Int(p.x - minX, p.y - minY));
        return norm;
    }

    private string GetShapeHash(List<Vector2Int> newShape)
    {
        newShape.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return string.Join(",", newShape);
    }
}