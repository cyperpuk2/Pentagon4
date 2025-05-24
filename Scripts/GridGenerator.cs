using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class GridGenerator : MonoBehaviour
{
    public GameObject cellPrefab;
    public GameObject numberTextPrefab; 
    public int width = 12;
    public int height = 12;
    public float spacing = 0.1f;

    private List<Cell> _allCells = new();
    private Dictionary<Vector2Int, Cell> _cellMap = new();
    private PentominoManager _pentominoManager;
    private Dictionary<int, List<List<Vector2Int>>> _cachedRotations = new();
    private List<(int index, List<Vector2Int> shape, Vector2Int position)> _fixedPentominoes = new();
    private List<TextMeshProUGUI> _rowNumbersLeft = new(); 
    private List<TextMeshProUGUI> _columnNumbersBottom = new(); 
    private Canvas _canvas;
    private HashSet<Vector2> _forbiddenPositions = new(); 

    public List<Cell> GetAllCells() => _allCells;
    public Cell GetCellAt(int x, int y)
    {
        if (!IsInBounds(x, y) && x != -1 && y != -1) return null;
        _cellMap.TryGetValue(new Vector2Int(x, y), out Cell cell);
        return cell;
    }

    void Start()
    {
        Random.InitState((int)System.DateTime.Now.Ticks);
        _pentominoManager = FindFirstObjectByType<PentominoManager>();
        _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null)
        {
            return;
        }
        GenerateGrid();
        SetupForbiddenPositions();
        GenerateNumberLabels();
        CacheRotations();
        PlaceSolvableObstacles();
        UpdateNumbers();
        AdjustCamera();
    }

    public void SetupForbiddenPositions()
    {
        _forbiddenPositions = new HashSet<Vector2>
        {
            new (6.05f, -7.15f), new (4.95f, -7.15f), new (3.85f, -7.15f), new (2.75f, -7.15f),
            new (1.65f, -7.15f), new (0.5499998f, -7.15f), new (-0.5500002f, -7.15f), new (-1.65f, -7.15f),
            new (-2.75f, -7.15f), new (-3.85f, -7.15f), new (-4.95f, -7.15f), new (-6.05f, -7.15f),
            new (-7.15f, 6.05f), new (-7.15f, 4.95f), new (-7.15f, 3.85f), new (-7.15f, 2.75f),
            new (-7.15f, 1.65f), new (-7.15f, 0.5499998f), new (-7.15f, -0.5500002f), new (-7.15f, -1.65f),
            new (-7.15f, -2.75f), new (-7.15f, -3.85f), new (-7.15f, -4.95f), new (-7.15f, -6.05f)
        };
    }

    public void GenerateGrid()
    {
        float startX = -((width - 1) + (width - 1) * spacing) / 2f;
        float startY = -((height - 1) + (height - 1) * spacing) / 2f;

        for (int x = -1; x < width; x++)
        {
            for (int y = -1; y < height; y++)
            {
                if (x == -1 && y == -1) continue;

                Vector2 position = new Vector2(startX + x + x * spacing, startY + y + y * spacing);
                GameObject newCell = Instantiate(cellPrefab, position, Quaternion.identity, transform);

                Cell cell = newCell.GetComponent<Cell>();
                cell.SetCoordinates(x, y);
                cell.SetObstacle(false);
                cell.isOccupied = false;

                _allCells.Add(cell);
                _cellMap[new Vector2Int(x, y)] = cell;
            }
        }
    }

    void GenerateNumberLabels()
    {
        if (numberTextPrefab == null || _canvas == null)
            return;

        Vector2[] bottomPositions = new Vector2[]
        {
            new (451, -454), new (379, -454), new (297, -454),
            new (229, -454), new (152, -454), new (71, -454),
            new (-2, -454), new (-79, -454), new (-156, -454),
            new (-233, -454), new (-306, -454), new (-383, -454)
        };

        Vector2[] leftPositions = new Vector2[]
        {
            new (-455, 454), new (-455, 374), new (-455, 301),
            new (-455, 225), new (-455, 152), new (-455, 72),
            new (-455, -4), new (-455, -77), new (-455, -154),
            new (-455, -231), new (-455, -308), new (-455, -384)
        };

        for (int i = 0; i < bottomPositions.Length; i++)
        {
            Vector2 pixelPos = bottomPositions[i];
            GameObject textObj = Instantiate(numberTextPrefab, _canvas.transform);
            RectTransform rectTransform = textObj.GetComponent<RectTransform>();

            rectTransform.anchoredPosition = pixelPos;

            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            if (text == null)
                continue;

            text.fontSize = 60;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(50, 50);
            text.text = "0";
            _columnNumbersBottom.Add(text);
        }

        for (int i = 0; i < leftPositions.Length; i++)
        {
            Vector2 pixelPos = leftPositions[i];
            GameObject textObj = Instantiate(numberTextPrefab, _canvas.transform);
            RectTransform rectTransform = textObj.GetComponent<RectTransform>();

            rectTransform.anchoredPosition = pixelPos;

            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            if (text == null)
                continue;

            text.fontSize = 60;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(50, 50);
            text.text = "0";
            _rowNumbersLeft.Add(text);
        }
    }

    public void UpdateNumbers()
    {
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                Cell cell = GetCellAt(x, y);
                if (cell != null && cell.isOccupied && !cell.isObstacle)
                    count++;
            }
            int labelIndex = height - 1 - y;
            if (labelIndex < _rowNumbersLeft.Count)
                _rowNumbersLeft[labelIndex].text = count.ToString();
        }

        for (int x = 0; x < width; x++)
        {
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                Cell cell = GetCellAt(x, y);
                if (cell != null && cell.isOccupied && !cell.isObstacle)
                    count++;
            }
            int labelIndex = width - 1 - x;
            if (labelIndex < _columnNumbersBottom.Count)
                _columnNumbersBottom[labelIndex].text = count.ToString();
        }
    }

    public void CacheRotations()
    {
        List<List<Vector2Int>> pentominoShapes = _pentominoManager.GetAllShapes();
        for (int i = 0; i < pentominoShapes.Count; i++)
            _cachedRotations[i] = GetAllRotations(pentominoShapes[i]);
    }

    public void PlaceSolvableObstacles()
    {
        List<List<Vector2Int>> pentominoShapes = _pentominoManager.GetAllShapes();
        List<int> shapeIndices = Enumerable.Range(0, pentominoShapes.Count).OrderBy(_ => Random.value).ToList();
        List<List<Vector2Int>> placedShapes = new List<List<Vector2Int>>();
        List<Vector2Int> placedPositions = new List<Vector2Int>();

        foreach (Cell cell in _allCells)
        {
            cell.SetObstacle(false);
            cell.isOccupied = false;
        }

        if (TryPlacePentominoes(shapeIndices, 0, placedShapes, placedPositions))
        {
            _fixedPentominoes.Clear();
            for (int i = 0; i < placedShapes.Count; i++)
            {
                int shapeIndex = shapeIndices[i];
                Vector2Int pos = placedPositions[i];
                List<Vector2Int> shape = placedShapes[i];
                _fixedPentominoes.Add((shapeIndex, shape, pos));
            }

            ClearAllOccupancy();

            int obstacleCount = Random.Range(10, 21);
            List<Cell> freeCells = _allCells.Where(c => !IsPartOfSolution(c) && !IsCellInForbiddenPosition(c)).ToList();
            int obstaclesToPlace = Mathf.Min(obstacleCount, freeCells.Count);
            for (int i = 0; i < obstaclesToPlace; i++)
            {
                if (freeCells.Count == 0) break;
                int index = Random.Range(0, freeCells.Count);
                freeCells[index].SetObstacle(true);
                freeCells.RemoveAt(index);
            }
        }
    }

    private bool IsCellInForbiddenPosition(Cell cell)
    {
        Vector2 cellWorldPos = new Vector2(cell.transform.position.x, cell.transform.position.y);
        foreach (var forbidden in _forbiddenPositions)
            if (Vector2.Distance(cellWorldPos, forbidden) < 0.1f)
                return true;
        return false;
    }

    private bool TryPlacePentominoes(List<int> shapeIndices, int index, List<List<Vector2Int>> placedShapes, List<Vector2Int> placedPositions)
    {
        if (index >= shapeIndices.Count)
            return true;

        int shapeIndex = shapeIndices[index];
        List<List<Vector2Int>> rotations = _cachedRotations[shapeIndex];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                rotations = rotations.OrderBy(_ => Random.value).ToList();
                foreach (var rotation in rotations)
                {
                    if (CanPlaceShape(rotation, x, y))
                    {
                        PlaceShape(rotation, x, y);
                        placedShapes.Add(rotation);
                        placedPositions.Add(new Vector2Int(x, y));

                        if (TryPlacePentominoes(shapeIndices, index + 1, placedShapes, placedPositions))
                            return true;

                        RemoveShape(rotation, x, y);
                        placedShapes.RemoveAt(placedShapes.Count - 1);
                        placedPositions.RemoveAt(placedPositions.Count - 1);
                    }
                }
            }
        }

        return false;
    }

    private bool CanPlaceShape(List<Vector2Int> shape, int gridX, int gridY)
    {
        List<Cell> futureCells = new List<Cell>();
        foreach (Vector2Int cellOffset in shape)
        {
            int cellX = gridX + cellOffset.x;
            int cellY = gridY + cellOffset.y;
            Cell targetCell = GetCellAt(cellX, cellY);
            if (targetCell == null || targetCell.isObstacle || targetCell.isOccupied)
                return false;
            futureCells.Add(targetCell);
        }

        foreach (Cell cell in futureCells)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Cell neighbor = GetCellAt(cell.x + dx, cell.y + dy);
                    if (neighbor != null && neighbor.isOccupied && !futureCells.Contains(neighbor))
                        return false;
                }
            }
        }
        return true;
    }

    private void PlaceShape(List<Vector2Int> shape, int gridX, int gridY)
    {
        foreach (Vector2Int cellOffset in shape)
        {
            int cellX = gridX + cellOffset.x;
            int cellY = gridY + cellOffset.y;
            Cell targetCell = GetCellAt(cellX, cellY);
            if (targetCell != null)
                targetCell.isOccupied = true;
        }
    }

    private void RemoveShape(List<Vector2Int> shape, int gridX, int gridY)
    {
        foreach (Vector2Int cellOffset in shape)
        {
            int cellX = gridX + cellOffset.x;
            int cellY = gridY + cellOffset.y;
            Cell targetCell = GetCellAt(cellX, cellY);
            if (targetCell != null)
                targetCell.isOccupied = false;
        }
    }

    private List<List<Vector2Int>> GetAllRotations(List<Vector2Int> shape)
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

    private List<Vector2Int> Normalize(List<Vector2Int> shape)
    {
        int minX = shape.Min(p => p.x);
        int minY = shape.Min(p => p.y);
        return shape.Select(p => new Vector2Int(p.x - minX, p.y - minY)).ToList();
    }

    private string GetShapeHash(List<Vector2Int> shape)
    {
        var sorted = shape.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
        return string.Join(",", sorted);
    }

    private bool IsPartOfSolution(Cell cell)
    {
        foreach (var (_, shape, position) in _fixedPentominoes)
        {
            foreach (var offset in shape)
            {
                int cellX = position.x + offset.x;
                int cellY = position.y + offset.y;
                if (cell.x == cellX && cell.y == cellY)
                    return true;
            }
        }
        return false;
    }
    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    void AdjustCamera()
    {
        float aspectRatio = (float)Screen.width / Screen.height;
        float gridWidth = (width + 1) + (width) * spacing;
        float gridHeight = (height + 1) + (height) * spacing;
        float marginX = 5f;
        float marginY = 4f;
        float totalWidth = gridWidth + 2 * marginX;
        float totalHeight = gridHeight + 2 * marginY;
        float cameraSize = Mathf.Max(totalHeight / 2f, totalWidth / (2f * aspectRatio));
        cameraSize *= 1.2f;
        Camera.main.orthographicSize = cameraSize;
        Camera.main.transform.position = new Vector3(-0.5f, -0.5f, -10f);
    }
    public void ClearAllOccupancy()
    {
        foreach (Cell cell in _allCells)
            cell.isOccupied = false;
        UpdateNumbers();
    }
    public List<(int index, List<Vector2Int> shape, Vector2Int position)> GetFixedPentominoes()
    {
        return new List<(int, List<Vector2Int>, Vector2Int)>(_fixedPentominoes);
    }
}
