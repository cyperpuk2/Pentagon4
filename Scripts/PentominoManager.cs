using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PentominoManager : MonoBehaviour
{
    public GameObject pentominoPrefab;

    private GridGenerator _gridGenerator;
    private List<DraggablePentomino> _allPentominoes = new();
    private GameStatsManager _statsManager;
    private PlacementHistoryManager _historyManager;
    private bool _isSolving;
    private bool _hasStarted; 

    public List<List<Vector2Int>> GetAllShapes()
    {
        return _classicPentominoShapes;
    }

    private List<List<Vector2Int>> _classicPentominoShapes = new()
    {
        new() { new(1,0), new(0,1), new(1,1), new(1,2), new(2,1) }, // F
        new() { new(0,0), new(1,0), new(2,0), new(3,0), new(4,0) }, // I
        new() { new(0,0), new(0,1), new(0,2), new(0,3), new(1,3) }, // L
        new() { new(0,0), new(0,1), new(1,1), new(1,2), new(1,3) }, // N
        new() { new(0,0), new(1,0), new(0,1), new(1,1), new(0,2) }, // P
        new() { new(0,0), new(1,0), new(2,0), new(1,1), new(1,2) }, // T
        new() { new(0,0), new(2,0), new(0,1), new(1,1), new(2,1) }, // U
        new() { new(0,0), new(0,1), new(0,2), new(1,0), new(2,0) }, // V
        new() { new(0,0), new(0,1), new(1,1), new(1,2), new(2,2) }, // W
        new() { new(0,0), new(1,0), new(1,1), new(1,2), new(2,2) }, // X
        new() { new(0,0), new(1,0), new(2,0), new(3,0), new(2,1) }, // Y
        new() { new(0,2), new(1,0), new(1,1), new(1,2), new(2,1) }  // Z
    };

    void Start()
    {
        _gridGenerator = FindFirstObjectByType<GridGenerator>();
        _statsManager = FindFirstObjectByType<GameStatsManager>();
        _historyManager = FindFirstObjectByType<PlacementHistoryManager>();

        if (pentominoPrefab == null || _statsManager == null || _historyManager == null)
            return;

        UpdateDisplay();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (!_hasStarted) 
            {
                SpawnPentominoes();
                _statsManager.StartGame();
                _hasStarted = true;
            }
            else if (!_statsManager.IsGameActive && _allPentominoes.Count > 0) 
            {
                _statsManager.StartGame(); 
            }
        }

        if (Input.GetKeyDown(KeyCode.A) && !_isSolving && _hasStarted)
            StartCoroutine(SolveBoard());
        if (Input.GetKeyDown(KeyCode.S) && _hasStarted)
            SaveGame();
        if (Input.GetKeyDown(KeyCode.L) && _hasStarted)
            LoadGame();
        if (Input.GetKeyDown(KeyCode.N) && _hasStarted)
            ResetGame();
        if (Input.GetKeyDown(KeyCode.E) && _hasStarted) 
            RestartSession();
        if (Input.GetKeyDown(KeyCode.Z) && _hasStarted)
            if (_historyManager != null)
                _historyManager.UndoLastPlacement();
        if (_hasStarted && IsGameComplete())
            _statsManager.CompleteGame();

        UpdateDisplay();
    }

    private void SpawnPentominoes()
    {
        if (_allPentominoes.Count > 0) return; 

        List<Vector2> pentominoPositions = new List<Vector2>
        {
            new (-7.06f, 4.52f),
            new (-8.07f, 2.97f),
            new (-4.61f, -1.68f),
            new (-9.11f, -1.85f),
            new (-4.91f, -6.82f),
            new (-9.72f, -6.76f),
            new (15.39f, 5.58f),
            new (15.47f, -2.41f),
            new (19.39f, -2.03f),
            new (14.66f, -6.86f),
            new (18.37f, -6.87f),
            new (19.78f, 1.88f)
        };

        for (int i = 0; i < _classicPentominoShapes.Count; i++)
        {
            GameObject newPentomino = Instantiate(pentominoPrefab, transform);
            Pentomino pentomino = newPentomino.GetComponent<Pentomino>();
            if (pentomino == null)
                continue;
            pentomino.Initialize(new List<Vector2Int>(_classicPentominoShapes[i]));
            newPentomino.transform.localPosition = new Vector3(pentominoPositions[i].x, pentominoPositions[i].y, 0f);

            DraggablePentomino draggable = newPentomino.GetComponent<DraggablePentomino>();
            if (draggable == null)
                continue;
            _allPentominoes.Add(draggable);
        }
    }

    private void UpdateDisplay()
    {
        if (_statsManager != null)
            _statsManager.UpdateDisplay();
    }

    private bool IsGameComplete()
    {
        bool allPlaced = true;
        foreach (var pentomino in _allPentominoes)
        {
            Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
            if (pentominoComponent == null)
                continue;
            if (pentominoComponent.currentOccupiedCells.Count == 0 || Vector3.SqrMagnitude(pentomino.transform.position - pentomino.startPosition) < 0.01f)
                allPlaced = false;
        }
        return allPlaced;
    }

    private IEnumerator SolveBoard()
    {
        _isSolving = true;
        _historyManager.ResetToInitialPositions();

        var fixedPositions = _gridGenerator.GetFixedPentominoes();
        if (fixedPositions.Count != _allPentominoes.Count)
            yield break;

        bool allPlaced = TryPlaceFixedPositions(fixedPositions);
        if (!allPlaced)
        {
            List<DraggablePentomino> remainingPentominoes = new List<DraggablePentomino>(_allPentominoes);
            bool solutionFound = false;
            yield return StartCoroutine(TryPlacePentominoes(remainingPentominoes, 0, result => solutionFound = result));
            if (!solutionFound)
                yield break;
        }

        _isSolving = false;
        _gridGenerator.UpdateNumbers();
    }

    private bool TryPlaceFixedPositions(List<(int index, List<Vector2Int> shape, Vector2Int position)> fixedPositions)
    {
        var sortedPentominoes = _allPentominoes.OrderBy(p => p.GetComponent<Pentomino>().index).ToList();
        fixedPositions = fixedPositions.OrderBy(p => p.index).ToList();

        for (int i = 0; i < sortedPentominoes.Count; i++)
        {
            var pentomino = sortedPentominoes[i];
            var (index, shape, position) = fixedPositions[i];
            Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
            float gridUnit = pentomino.cellSize + pentomino.cellSpacing;
            float startX = -((_gridGenerator.width - 1) * gridUnit) / 2f;
            float startY = -((_gridGenerator.height - 1) * gridUnit) / 2f;
            Vector3 worldPos = new Vector3(startX + position.x * gridUnit, startY + position.y * gridUnit, 0);

            pentominoComponent.shape = new List<Vector2Int>(shape);
            pentominoComponent.GenerateShape();

            if (CanPlacePentomino(pentomino, position.x, position.y))
                PlacePentomino(pentomino, worldPos);
            else
                return false;
        }
        return true;
    }

    private IEnumerator TryPlacePentominoes(List<DraggablePentomino> pentominoes, int index, System.Action<bool> onComplete)
    {
        if (index >= pentominoes.Count)
        {
            onComplete(true);
            yield break;
        }

        DraggablePentomino currentPentomino = pentominoes[index];
        Pentomino pentominoComponent = currentPentomino.GetComponent<Pentomino>();
        List<List<Vector2Int>> rotations = pentominoComponent.GetAllRotations();
        float gridUnit = currentPentomino.cellSize + currentPentomino.cellSpacing;
        float startX = -((_gridGenerator.width - 1) * gridUnit) / 2f;
        float startY = -((_gridGenerator.height - 1) * gridUnit) / 2f;

        for (int x = 0; x < _gridGenerator.width; x++)
        {
            for (int y = 0; y < _gridGenerator.height; y++)
            {
                foreach (List<Vector2Int> rotation in rotations)
                {
                    pentominoComponent.shape = new List<Vector2Int>(rotation);
                    pentominoComponent.GenerateShape();
                    Vector3 worldPos = new Vector3(startX + x * gridUnit, startY + y * gridUnit, 0);
                    currentPentomino.transform.position = worldPos;

                    if (CanPlacePentomino(currentPentomino, x, y))
                    {
                        PlacePentomino(currentPentomino, worldPos);
                        yield return new WaitForSeconds(0.05f);
                        bool subSolutionFound = false;
                        yield return StartCoroutine(TryPlacePentominoes(pentominoes, index + 1, result => subSolutionFound = result));
                        if (subSolutionFound)
                        {
                            onComplete(true);
                            yield break;
                        }
                        RemovePentomino(currentPentomino);
                    }
                }
            }
        }
        onComplete(false);
    }

    private bool CanPlacePentomino(DraggablePentomino draggable, int gridX, int gridY)
    {
        Pentomino pentomino = draggable.GetComponent<Pentomino>();

        List<Cell> futureCells = new List<Cell>();
        foreach (Vector2Int cellOffset in pentomino.Shape)
        {
            int cellX = gridX + cellOffset.x;
            int cellY = gridY + cellOffset.y;
            Cell targetCell = _gridGenerator.GetCellAt(cellX, cellY);
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
                    Cell neighbor = _gridGenerator.GetCellAt(cell.x + dx, cell.y + dy);
                    if (neighbor != null && neighbor.isOccupied && !futureCells.Contains(neighbor))
                        return false;
                }
            }
        }
        return true;
    }

    private void PlacePentomino(DraggablePentomino draggable, Vector3 worldPos)
    {
        draggable.transform.position = worldPos;
        Pentomino pentomino = draggable.GetComponent<Pentomino>();
        pentomino.UpdateOccupiedCells(worldPos, draggable.cellSize + draggable.cellSpacing,
            -((_gridGenerator.width - 1) * (draggable.cellSize + draggable.cellSpacing)) / 2f,
            -((_gridGenerator.height - 1) * (draggable.cellSize + draggable.cellSpacing)) / 2f,
            _gridGenerator);
    }

    private void RemovePentomino(DraggablePentomino draggable)
    {
        Pentomino pentomino = draggable.GetComponent<Pentomino>();
        foreach (Cell cell in pentomino.currentOccupiedCells)
            if (cell != null && !cell.isObstacle)
                cell.isOccupied = false;
        pentomino.currentOccupiedCells.Clear();
        draggable.transform.position = draggable.startPosition;
        pentomino.GenerateShape();
    }

    private void RestartSession()
    {
        _historyManager.ResetToInitialPositions();
        _gridGenerator.ClearAllOccupancy();
        foreach (var pentomino in _allPentominoes)
        {
            Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
            pentominoComponent.currentOccupiedCells.Clear();
            pentomino.transform.position = pentomino.startPosition;
            pentominoComponent.GenerateShape();
        }
        
        _gridGenerator.GenerateGrid();
        _gridGenerator.SetupForbiddenPositions();
        _gridGenerator.CacheRotations();
        _gridGenerator.PlaceSolvableObstacles();
        _gridGenerator.UpdateNumbers();
        
        _statsManager.ResetStats();
        
        UpdateDisplay();
    }

    [System.Serializable]
    private class GameState
    {
        public List<PentominoState> pentominoes;
        public GameStatsManager.StatsData stats;
        public List<CellState> cells;
    }

    [System.Serializable]
    private class PentominoState
    {
        public int index;
        public Vector3 position;
        public Quaternion rotation;
        public List<Vector2Int> shape;
    }

    [System.Serializable]
    private class CellState
    {
        public int x;
        public int y;
        public bool isObstacle;
        public bool isOccupied;
    }

    public void SaveGame()
    {
        _statsManager.PauseGame();
        SaveGameToFile();
    }

    private void SaveGameToFile()
    {
        GameState state = new GameState
        {
            pentominoes = new List<PentominoState>(),
            stats = _statsManager.GetStatsData(),
            cells = new List<CellState>()
        };

        foreach (var pentomino in _allPentominoes)
        {
            Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
            state.pentominoes.Add(new PentominoState
            {
                index = pentominoComponent.index,
                position = pentomino.transform.position,
                rotation = pentomino.transform.rotation,
                shape = new List<Vector2Int>(pentominoComponent.shape)
            });
        }

        foreach (var cell in _gridGenerator.GetAllCells())
        {
            state.cells.Add(new CellState
            {
                x = cell.x,
                y = cell.y,
                isObstacle = cell.isObstacle,
                isOccupied = cell.isOccupied
            });
        }

        string json = JsonUtility.ToJson(state, true);
        string path = Path.Combine(Application.persistentDataPath, "gameState.json");
        File.WriteAllText(path, json);
        
        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            string desktopPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "gameState.json");
            File.WriteAllText(desktopPath, json);
        }
    }

    public void LoadGame()
    {
        string path = Path.Combine(Application.persistentDataPath, "gameState.json");
        if (!File.Exists(path))
            return;

        string json = File.ReadAllText(path);
        GameState state = JsonUtility.FromJson<GameState>(json);

        _gridGenerator.ClearAllOccupancy();
        foreach (var pentomino in _allPentominoes)
            pentomino.GetComponent<Pentomino>().currentOccupiedCells.Clear();

        foreach (var pentominoState in state.pentominoes)
        {
            var pentomino = _allPentominoes.Find(p => p.GetComponent<Pentomino>().index == pentominoState.index);
            if (pentomino == null)
                continue;
            Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
            pentomino.transform.position = pentominoState.position;
            pentomino.transform.rotation = pentominoState.rotation;
            pentominoComponent.shape = new List<Vector2Int>(pentominoState.shape);
            pentominoComponent.GenerateShape();
            float gridUnit = pentomino.cellSize + pentomino.cellSpacing;
            float startX = -((_gridGenerator.width - 1) * gridUnit) / 2f;
            float startY = -((_gridGenerator.height - 1) * gridUnit) / 2f;
            pentominoComponent.UpdateOccupiedCells(pentominoState.position, gridUnit, startX, startY, _gridGenerator);
        }

        foreach (var cellState in state.cells)
        {
            Cell cell = _gridGenerator.GetCellAt(cellState.x, cellState.y);
            if (cell != null)
            {
                cell.isObstacle = cellState.isObstacle;
                cell.isOccupied = cellState.isOccupied;
                cell.SetObstacle(cellState.isObstacle);
            }
        }

        _statsManager.LoadStatsData(state.stats);
        _statsManager.ResumeGame();
        _gridGenerator.UpdateNumbers();
    }

    private void ResetGame()
    {
        _historyManager.ResetToInitialPositions();
        _gridGenerator.ClearAllOccupancy();
        foreach (var pentomino in _allPentominoes)
        {
            Pentomino pentominoComponent = pentomino.GetComponent<Pentomino>();
            pentominoComponent.currentOccupiedCells.Clear();
            pentomino.transform.position = pentomino.startPosition;
            pentominoComponent.GenerateShape();
        }
        _statsManager.ResetStats(); 
        _gridGenerator.UpdateNumbers();
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Save Game State")]
    private static void SaveGameViaMenu()
    {
        PentominoManager pentominoManager = FindFirstObjectByType<PentominoManager>();
        if (pentominoManager != null)
            pentominoManager.SaveGameToFile();
    }
#endif
}