using UnityEngine;
using TMPro;
using System.IO;

public class GameStatsManager : MonoBehaviour
{
    public TextMeshProUGUI statsText;
    public bool IsGameActive { get; private set; }
    public int MoveCount { get; private set; }
    public float CurrentGameTime { get; private set; }
    private float[] _completionTimes = new float[10];
    private int _completionIndex;
    private float _fastestTime = float.MaxValue;
    private float _averageTime;

    private const string StatsFilePath = "gameStats.json";

    void Awake()
    {
        LoadPersistentStats();
    }

    void Start()
    {
        if (statsText == null)
            return;
        UpdateDisplay();
    }

    void Update()
    {
        if (IsGameActive)
        {
            CurrentGameTime += Time.deltaTime;
            UpdateDisplay();
        }
    }

    public void StartGame()
    {
        if (!IsGameActive)
        {
            IsGameActive = true;
            MoveCount = 0;
            CurrentGameTime = 0f;
            UpdateDisplay();
        }
    }

    public void ResumeGame()
    {
        IsGameActive = true;
        UpdateDisplay();
    }

    public void PauseGame()
    {
        IsGameActive = false;
        UpdateDisplay();
    }

    public void IncrementMove()
    {
        if (IsGameActive)
        {
            MoveCount++;
            UpdateDisplay();
        }
    }

    public void CompleteGame()
    {
        if (IsGameActive)
        {
            IsGameActive = false;
            _completionTimes[_completionIndex] = CurrentGameTime;
            _completionIndex = (_completionIndex + 1) % _completionTimes.Length;
            _fastestTime = Mathf.Min(_fastestTime, CurrentGameTime);
            _averageTime = CalculateAverage(_completionTimes);
            SavePersistentStats();
            UpdateDisplay();
        }
    }

    public void ResetStats()
    {
        MoveCount = 0;
        CurrentGameTime = 0f;
        IsGameActive = false;
        UpdateDisplay();
    }

    private float CalculateAverage(float[] times)
    {
        float sum = 0f;
        int count = 0;
        foreach (float time in times)
            if (time > 0f)
            {
                sum += time;
                count++;
            }
        return count > 0 ? sum / count : 0f;
    }
    
    public void UpdateDisplay()
    {
        if (statsText != null)
            statsText.text = $"Ходи: {MoveCount}\nЧас гри: {CurrentGameTime:F2} сек\nНайшвидший час: {(_fastestTime == float.MaxValue ? 0f : _fastestTime):F2} сек\nСередній час: {_averageTime:F2} сек";
    }

    [System.Serializable]
    public class StatsData
    {
        public int moveCount;
        public float currentGameTime;
        public float[] completionTimes;
        public int completionIndex;
        public float fastestTime;
        public float averageTime;
        public bool isGameActive;
    }

    public StatsData GetStatsData()
    {
        return new StatsData
        {
            moveCount = MoveCount,
            currentGameTime = CurrentGameTime,
            completionTimes = _completionTimes,
            completionIndex = _completionIndex,
            fastestTime = _fastestTime,
            averageTime = _averageTime,
            isGameActive = IsGameActive
        };
    }

    public void LoadStatsData(StatsData data)
    {
        MoveCount = data.moveCount;
        CurrentGameTime = data.currentGameTime;
        _completionTimes = data.completionTimes;
        _completionIndex = data.completionIndex;
        _fastestTime = data.fastestTime;
        _averageTime = data.averageTime;
        IsGameActive = data.isGameActive;
        UpdateDisplay();
    }

    private void SavePersistentStats()
    {
        PersistentStatsData persistentData = new PersistentStatsData
        {
            completionTimes = _completionTimes,
            completionIndex = _completionIndex,
            fastestTime = _fastestTime,
            averageTime = _averageTime
        };
        string json = JsonUtility.ToJson(persistentData, true);
        string path = Path.Combine(Application.persistentDataPath, StatsFilePath);
        File.WriteAllText(path, json);
        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            string desktopPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), StatsFilePath);
            File.WriteAllText(desktopPath, json);
        }
    }

    private void LoadPersistentStats()
    {
        string path = Path.Combine(Application.persistentDataPath, StatsFilePath);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            PersistentStatsData persistentData = JsonUtility.FromJson<PersistentStatsData>(json);
            _completionTimes = persistentData.completionTimes;
            _completionIndex = persistentData.completionIndex;
            _fastestTime = persistentData.fastestTime;
            _averageTime = persistentData.averageTime;
        }
        else
        {
            _completionTimes = new float[10];
            _completionIndex = 0;
            _fastestTime = float.MaxValue;
            _averageTime = 0f;
        }
    }

    [System.Serializable]
    private class PersistentStatsData
    {
        public float[] completionTimes;
        public int completionIndex;
        public float fastestTime;
        public float averageTime;
    }
}