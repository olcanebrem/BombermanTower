using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Service interface for level data management
/// Separates level loading logic from Unity components
/// </summary>
public interface ILevelDataService
{
    // Level file management
    void ScanForLevelFiles();
    List<LevelFileEntry> GetAvailableLevels();
    LevelFileEntry GetSelectedLevel();
    bool SelectLevelByNumber(int levelNumber);
    bool SelectLevelByNumberAndVersion(int levelNumber, string version);
    
    // Level progression
    bool HasNextLevel();
    bool HasPreviousLevel();
    bool SelectNextLevel();
    bool SelectPreviousLevel();
    void ResetToFirstLevel();
    void SelectLastLevel();
    
    // Level data access
    HoudiniLevelData GetCurrentLevelData();
    HoudiniLevelData LoadLevelData(TextAsset levelAsset);
    int GetCurrentLevelNumber();
    int GetTotalLevelCount();
    
    // Level validation
    bool ValidateLevelData(HoudiniLevelData levelData);
    Dictionary<TileType, int> GetExpectedTileCounts(HoudiniLevelData levelData);
    
    // Events
    event System.Action<HoudiniLevelData> OnLevelDataLoaded;
    event System.Action<string> OnLevelLoadError;
}