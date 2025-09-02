using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Service implementation for level data management
/// Handles all level file operations and validation
/// </summary>
public class LevelDataService : MonoBehaviour, ILevelDataService
{
    public static ILevelDataService Instance { get; private set; }
    
    [Header("Level Configuration")]
    [SerializeField] private List<LevelFileEntry> availableLevels = new List<LevelFileEntry>();
    [SerializeField] private int selectedLevelIndex = 0;
    [SerializeField] private string levelsDirectoryPath = "Assets/Levels";
    
    private HoudiniLevelData currentLevelData;
    private HoudiniLevelParser levelParser;
    
    // Events
    public event System.Action<HoudiniLevelData> OnLevelDataLoaded;
    public event System.Action<string> OnLevelLoadError;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeService();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeService()
    {
        // Find or create level parser
        levelParser = HoudiniLevelParser.Instance;
        if (levelParser == null)
        {
            levelParser = FindObjectOfType<HoudiniLevelParser>();
            if (levelParser == null)
            {
                var parserGO = new GameObject("HoudiniLevelParser");
                levelParser = parserGO.AddComponent<HoudiniLevelParser>();
            }
        }
    }
    
    #region Level File Management
    
    public void ScanForLevelFiles()
    {
        availableLevels.Clear();
        
        #if UNITY_EDITOR
        ScanLevelsWithAssetDatabase();
        #else
        ScanLevelsFromResources();
        #endif
        
        // Sort by level number, then by version
        availableLevels.Sort((a, b) => {
            int levelCompare = a.levelNumber.CompareTo(b.levelNumber);
            return levelCompare != 0 ? levelCompare : string.Compare(a.version, b.version, System.StringComparison.Ordinal);
        });
    }
    
    #if UNITY_EDITOR
    private void ScanLevelsWithAssetDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("LEVEL_ t:TextAsset", new[] { levelsDirectoryPath });
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            
            if (IsValidLevelFileName(fileName))
            {
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (textAsset != null)
                {
                    var levelEntry = ParseLevelFileName(fileName, assetPath, textAsset);
                    availableLevels.Add(levelEntry);
                }
            }
        }
    }
    #endif
    
    private void ScanLevelsFromResources()
    {
        TextAsset[] levelAssets = Resources.LoadAll<TextAsset>("Levels");
        
        foreach (TextAsset asset in levelAssets)
        {
            if (IsValidLevelFileName(asset.name))
            {
                string assetPath = "Resources/Levels/" + asset.name;
                var levelEntry = ParseLevelFileName(asset.name, assetPath, asset);
                availableLevels.Add(levelEntry);
            }
        }
    }
    
    private bool IsValidLevelFileName(string fileName)
    {
        return fileName.StartsWith("LEVEL_") && fileName.Contains("_v");
    }
    
    private LevelFileEntry ParseLevelFileName(string fileName, string fullPath, TextAsset textAsset)
    {
        var entry = new LevelFileEntry
        {
            fileName = fileName,
            fullPath = fullPath,
            textAsset = textAsset,
            levelNumber = 1,
            version = "1.0.0"
        };
        
        try
        {
            string[] parts = fileName.Split('_');
            
            if (parts.Length >= 2 && int.TryParse(parts[1], out int levelNumber))
            {
                entry.levelNumber = levelNumber;
            }
            
            if (parts.Length >= 3 && parts[2].StartsWith("v"))
            {
                entry.version = parts[2].Substring(1);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LevelDataService] Failed to parse level file name '{fileName}': {e.Message}");
        }
        
        return entry;
    }
    
    public List<LevelFileEntry> GetAvailableLevels()
    {
        return new List<LevelFileEntry>(availableLevels);
    }
    
    public LevelFileEntry GetSelectedLevel()
    {
        if (selectedLevelIndex >= 0 && selectedLevelIndex < availableLevels.Count)
        {
            return availableLevels[selectedLevelIndex];
        }
        return new LevelFileEntry();
    }
    
    #endregion
    
    #region Level Selection and Progression
    
    public bool SelectLevelByNumber(int levelNumber)
    {
        int index = availableLevels.FindIndex(level => level.levelNumber == levelNumber);
        if (index >= 0)
        {
            selectedLevelIndex = index;
            return true;
        }
        return false;
    }
    
    public bool SelectLevelByNumberAndVersion(int levelNumber, string version)
    {
        int index = availableLevels.FindIndex(level => 
            level.levelNumber == levelNumber && level.version == version);
        if (index >= 0)
        {
            selectedLevelIndex = index;
            return true;
        }
        return false;
    }
    
    public bool HasNextLevel()
    {
        return selectedLevelIndex < availableLevels.Count - 1;
    }
    
    public bool HasPreviousLevel()
    {
        return selectedLevelIndex > 0;
    }
    
    public bool SelectNextLevel()
    {
        if (HasNextLevel())
        {
            selectedLevelIndex++;
            return true;
        }
        return false;
    }
    
    public bool SelectPreviousLevel()
    {
        if (HasPreviousLevel())
        {
            selectedLevelIndex--;
            return true;
        }
        return false;
    }
    
    public void ResetToFirstLevel()
    {
        if (availableLevels.Count > 0)
        {
            selectedLevelIndex = 0;
        }
    }
    
    public void SelectLastLevel()
    {
        if (availableLevels.Count > 0)
        {
            selectedLevelIndex = availableLevels.Count - 1;
        }
    }
    
    #endregion
    
    #region Level Data Operations
    
    public HoudiniLevelData GetCurrentLevelData()
    {
        return currentLevelData;
    }
    
    public HoudiniLevelData LoadLevelData(TextAsset levelAsset)
    {
        if (levelAsset == null)
        {
            OnLevelLoadError?.Invoke("Level asset is null");
            return null;
        }
        
        try
        {
            // Use LevelImporter if available, otherwise fallback to direct parsing
            if (LevelImporter.Instance != null)
            {
                currentLevelData = LevelImporter.Instance.ImportLevel(levelAsset);
            }
            else if (levelParser != null)
            {
                currentLevelData = levelParser.ParseLevelData(levelAsset);
            }
            else
            {
                OnLevelLoadError?.Invoke("No level parser available");
                return null;
            }
            
            if (currentLevelData != null)
            {
                OnLevelDataLoaded?.Invoke(currentLevelData);
            }
            else
            {
                OnLevelLoadError?.Invoke($"Failed to parse level data from {levelAsset.name}");
            }
            
            return currentLevelData;
        }
        catch (System.Exception e)
        {
            OnLevelLoadError?.Invoke($"Exception loading level data: {e.Message}");
            return null;
        }
    }
    
    public int GetCurrentLevelNumber()
    {
        var selectedLevel = GetSelectedLevel();
        return selectedLevel.levelNumber;
    }
    
    public int GetTotalLevelCount()
    {
        return availableLevels.Count;
    }
    
    #endregion
    
    #region Level Validation
    
    public bool ValidateLevelData(HoudiniLevelData levelData)
    {
        if (levelData == null) return false;
        if (levelData.grid == null) return false;
        if (levelData.gridWidth <= 0 || levelData.gridHeight <= 0) return false;
        
        // Validate player spawn position
        var spawn = levelData.playerSpawn;
        if (spawn.x < 0 || spawn.x >= levelData.gridWidth ||
            spawn.y < 0 || spawn.y >= levelData.gridHeight)
        {
            return false;
        }
        
        return true;
    }
    
    public Dictionary<TileType, int> GetExpectedTileCounts(HoudiniLevelData levelData)
    {
        if (levelData?.GetExpectedTileCounts != null)
        {
            return levelData.GetExpectedTileCounts();
        }
        
        // Fallback: calculate from grid data
        var counts = new Dictionary<TileType, int>();
        
        if (levelData?.grid != null)
        {
            for (int x = 0; x < levelData.gridWidth; x++)
            {
                for (int y = 0; y < levelData.gridHeight; y++)
                {
                    if (x < levelData.grid.GetLength(0) && y < levelData.grid.GetLength(1))
                    {
                        char symbol = levelData.grid[x, y];
                        TileType type = TileSymbols.DataSymbolToType(symbol);
                        
                        if (!counts.ContainsKey(type))
                            counts[type] = 0;
                        counts[type]++;
                    }
                }
            }
        }
        
        return counts;
    }
    
    #endregion
}