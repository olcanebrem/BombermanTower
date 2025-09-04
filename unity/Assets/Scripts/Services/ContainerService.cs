using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Service implementation for container management
/// Handles creation and organization of level-specific container hierarchies
/// </summary>
public class ContainerService : MonoBehaviour, IContainerService
{
    public static IContainerService Instance { get; private set; }
    
    [Header("Container Configuration")]
    [SerializeField] private Transform levelContentParent;
    
    // Current level containers
    private string currentLevelId;
    private Transform currentLevelContainer;
    private Transform currentStaticContainer;
    private Transform currentDestructibleContainer;
    private Transform currentDynamicContainer;
    
    // Container cache for quick access
    private readonly Dictionary<string, Transform> containerCache = new();
    
    // Events
    public event System.Action<string> OnLevelContainerCreated;
    public event System.Action<string> OnLevelContainerDestroyed;
    
    // Properties
    public string CurrentLevelId => currentLevelId;
    public bool HasCurrentLevel => !string.IsNullOrEmpty(currentLevelId) && currentLevelContainer != null;
    
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
        // Find or create level content parent
        if (levelContentParent == null)
        {
            GameObject levelContentGO = GameObject.Find("[LEVEL CONTENT]");
            if (levelContentGO == null)
            {
                levelContentGO = new GameObject("[LEVEL CONTENT]");
            }
            levelContentParent = levelContentGO.transform;
        }
    }
    
    #region Container Creation
    
    public void CreateLevelContainers(string levelId, string levelName)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("[ContainerService] Level ID cannot be null or empty");
            return;
        }
        
        // Clear existing containers first
        ClearCurrentContainers();
        
        // Set current level info
        currentLevelId = levelId;
        
        // Create main level container
        string containerName = !string.IsNullOrEmpty(levelName) ? $"Level_{levelName}" : $"Level_{levelId}";
        GameObject levelContainerGO = new GameObject(containerName);
        currentLevelContainer = levelContainerGO.transform;
        currentLevelContainer.SetParent(levelContentParent);
        
        // Create category containers
        CreateCategoryContainers();
        
        // Cache containers for quick access
        UpdateContainerCache();
        
        // Publish event
        OnLevelContainerCreated?.Invoke(levelId);
        GameEventBus.Instance?.Publish(new ContainerCreated(levelContainerGO, "Level", levelId));
    }
    
    private void CreateCategoryContainers()
    {
        // Create Static container and sub-containers
        GameObject staticGO = new GameObject("Static");
        currentStaticContainer = staticGO.transform;
        currentStaticContainer.SetParent(currentLevelContainer);
        
        CreateSubContainer("Walls", currentStaticContainer);
        CreateSubContainer("Gates", currentStaticContainer);
        
        // Create Destructible container and sub-containers
        GameObject destructibleGO = new GameObject("Destructible");
        currentDestructibleContainer = destructibleGO.transform;
        currentDestructibleContainer.SetParent(currentLevelContainer);
        
        CreateSubContainer("Breakables", currentDestructibleContainer);
        
        // Create Dynamic container and sub-containers
        GameObject dynamicGO = new GameObject("Dynamic");
        currentDynamicContainer = dynamicGO.transform;
        currentDynamicContainer.SetParent(currentLevelContainer);
        
        CreateSubContainer("Enemies", currentDynamicContainer);
        CreateSubContainer("Collectibles", currentDynamicContainer);
        CreateSubContainer("Effects", currentDynamicContainer);
        CreateSubContainer("Projectiles", currentDynamicContainer);
    }
    
    private Transform CreateSubContainer(string name, Transform parent)
    {
        GameObject containerGO = new GameObject($"{name} Container");
        containerGO.transform.SetParent(parent);
        
        // Cache the container
        string cacheKey = $"{currentLevelId}_{name}";
        containerCache[cacheKey] = containerGO.transform;
        
        return containerGO.transform;
    }
    
    private void UpdateContainerCache()
    {
        if (currentLevelId == null) return;
        
        containerCache[$"{currentLevelId}_Static"] = currentStaticContainer;
        containerCache[$"{currentLevelId}_Destructible"] = currentDestructibleContainer;
        containerCache[$"{currentLevelId}_Dynamic"] = currentDynamicContainer;
        containerCache[$"{currentLevelId}_Level"] = currentLevelContainer;
    }
    
    #endregion
    
    #region Container Access
    
    public Transform GetContainerForTileType(TileType tileType)
    {
        if (!HasCurrentLevel) return levelContentParent;
        
        return tileType switch
        {
            TileType.Wall => GetCachedContainer("Walls") ?? currentStaticContainer ?? levelContentParent,
            TileType.Gate => GetCachedContainer("Gates") ?? currentStaticContainer ?? levelContentParent,
            TileType.Breakable => GetCachedContainer("Breakables") ?? currentDestructibleContainer ?? levelContentParent,
            TileType.Enemy or TileType.EnemyShooter => GetCachedContainer("Enemies") ?? currentDynamicContainer ?? levelContentParent,
            TileType.Coin or TileType.Health => GetCachedContainer("Collectibles") ?? currentDynamicContainer ?? levelContentParent,
            TileType.Player or TileType.PlayerSpawn => currentDynamicContainer ?? levelContentParent,
            TileType.Bomb => GetCachedContainer("Projectiles") ?? currentDynamicContainer ?? levelContentParent,
            TileType.Projectile => GetCachedContainer("Projectiles") ?? currentDynamicContainer ?? levelContentParent,
            TileType.Explosion => GetCachedContainer("Effects") ?? currentDynamicContainer ?? levelContentParent,
            _ => currentLevelContainer ?? levelContentParent
        };
    }
    
    public Transform GetProjectilesContainer()
    {
        return GetCachedContainer("Projectiles") ?? currentDynamicContainer ?? levelContentParent;
    }
    
    public Transform GetEffectsContainer()
    {
        return GetCachedContainer("Effects") ?? currentDynamicContainer ?? levelContentParent;
    }
    
    public Transform GetLevelContainer(string levelId = null)
    {
        string targetLevelId = levelId ?? currentLevelId;
        if (string.IsNullOrEmpty(targetLevelId)) return null;
        
        return GetCachedContainer("Level", targetLevelId) ?? currentLevelContainer;
    }
    
    public Transform GetStaticContainer()
    {
        return currentStaticContainer;
    }
    
    public Transform GetDestructibleContainer()
    {
        return currentDestructibleContainer;
    }
    
    public Transform GetDynamicContainer()
    {
        return currentDynamicContainer;
    }
    
    private Transform GetCachedContainer(string containerName, string levelId = null)
    {
        string targetLevelId = levelId ?? currentLevelId;
        if (string.IsNullOrEmpty(targetLevelId)) return null;
        
        string cacheKey = $"{targetLevelId}_{containerName}";
        return containerCache.TryGetValue(cacheKey, out Transform container) ? container : null;
    }
    
    #endregion
    
    #region Container Cleanup
    
    public void ClearCurrentContainers()
    {
        if (!string.IsNullOrEmpty(currentLevelId))
        {
            DestroyLevelContainer(currentLevelId);
        }
        
        currentLevelId = null;
        currentLevelContainer = null;
        currentStaticContainer = null;
        currentDestructibleContainer = null;
        currentDynamicContainer = null;
        
        // Clear cache entries for current level
        var keysToRemove = new List<string>();
        foreach (var key in containerCache.Keys)
        {
            if (key.StartsWith(currentLevelId ?? ""))
            {
                keysToRemove.Add(key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            containerCache.Remove(key);
        }
    }
    
    public void DestroyLevelContainer(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return;
        
        Transform container = GetCachedContainer("Level", levelId);
        if (container != null)
        {
            // Count objects for event
            int objectsDestroyed = CountChildrenRecursively(container);
            
            Destroy(container.gameObject);
            
            // Remove from cache
            var keysToRemove = new List<string>();
            foreach (var key in containerCache.Keys)
            {
                if (key.StartsWith(levelId))
                {
                    keysToRemove.Add(key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                containerCache.Remove(key);
            }
            
            // Publish events
            OnLevelContainerDestroyed?.Invoke(levelId);
            GameEventBus.Instance?.Publish(new ContainerCleared("Level", levelId, objectsDestroyed));
        }
    }
    
    public void CleanupNullReferences()
    {
        var keysToRemove = new List<string>();
        
        foreach (var kvp in containerCache)
        {
            if (kvp.Value == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            containerCache.Remove(key);
        }
    }
    
    private int CountChildrenRecursively(Transform parent)
    {
        int count = parent.childCount;
        for (int i = 0; i < parent.childCount; i++)
        {
            count += CountChildrenRecursively(parent.GetChild(i));
        }
        return count;
    }
    
    #endregion
}