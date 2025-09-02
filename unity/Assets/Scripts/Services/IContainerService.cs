using UnityEngine;

/// <summary>
/// Service interface for container management
/// Handles creation and organization of level-specific containers
/// </summary>
public interface IContainerService
{
    // Container creation
    void CreateLevelContainers(string levelId, string levelName);
    void ClearCurrentContainers();
    
    // Container access
    Transform GetContainerForTileType(TileType tileType);
    Transform GetProjectilesContainer();
    Transform GetEffectsContainer();
    Transform GetLevelContainer(string levelId = null);
    
    // Container organization
    Transform GetStaticContainer();
    Transform GetDestructibleContainer();
    Transform GetDynamicContainer();
    
    // Cleanup
    void CleanupNullReferences();
    void DestroyLevelContainer(string levelId);
    
    // Properties
    string CurrentLevelId { get; }
    bool HasCurrentLevel { get; }
    
    // Events
    event System.Action<string> OnLevelContainerCreated;
    event System.Action<string> OnLevelContainerDestroyed;
}