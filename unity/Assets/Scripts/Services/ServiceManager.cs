using UnityEngine;

/// <summary>
/// Central service manager for initializing and coordinating all game services
/// Ensures proper initialization order and dependency resolution
/// </summary>
public class ServiceManager : MonoBehaviour
{
    public static ServiceManager Instance { get; private set; }
    
    [Header("Service Configuration")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private SpriteDatabase spriteDatabase;
    
    // Service instances
    public ILevelDataService LevelDataService { get; private set; }
    public IContainerService ContainerService { get; private set; }
    public IPlayerService PlayerService { get; private set; }
    public GameEventBus EventBus { get; private set; }
    public LayeredGridService GridService { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAllServices();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Initialize all services in the correct order
    /// </summary>
    private void InitializeAllServices()
    {
        Debug.Log("[ServiceManager] Initializing all game services...");
        
        // 1. Event Bus (must be first)
        InitializeEventBus();
        
        // 2. Core Services
        InitializeGridService();
        InitializeLevelDataService();
        InitializeContainerService();
        InitializePlayerService();
        
        Debug.Log("[ServiceManager] All services initialized successfully");
        
        // Subscribe to service events for monitoring
        SubscribeToServiceEvents();
    }
    
    private void InitializeEventBus()
    {
        if (GameEventBus.Instance == null)
        {
            var eventBusGO = new GameObject("GameEventBus");
            eventBusGO.transform.SetParent(transform);
            EventBus = eventBusGO.AddComponent<GameEventBus>();
        }
        else
        {
            EventBus = GameEventBus.Instance;
        }
        
        Debug.Log("[ServiceManager] ✅ GameEventBus initialized");
    }
    
    private void InitializeGridService()
    {
        if (LayeredGridService.Instance == null)
        {
            var gridServiceGO = new GameObject("LayeredGridService");
            gridServiceGO.transform.SetParent(transform);
            GridService = gridServiceGO.AddComponent<LayeredGridService>();
        }
        else
        {
            GridService = LayeredGridService.Instance;
        }
        
        Debug.Log("[ServiceManager] ✅ LayeredGridService initialized");
    }
    
    private void InitializeLevelDataService()
    {
        if (LevelDataService.Instance == null)
        {
            var levelDataServiceGO = new GameObject("LevelDataService");
            levelDataServiceGO.transform.SetParent(transform);
            LevelDataService = levelDataServiceGO.AddComponent<LevelDataService>();
        }
        else
        {
            LevelDataService = LevelDataService.Instance;
        }
        
        Debug.Log("[ServiceManager] ✅ LevelDataService initialized");
    }
    
    private void InitializeContainerService()
    {
        if (ContainerService.Instance == null)
        {
            var containerServiceGO = new GameObject("ContainerService");
            containerServiceGO.transform.SetParent(transform);
            ContainerService = containerServiceGO.AddComponent<ContainerService>();
        }
        else
        {
            ContainerService = ContainerService.Instance;
        }
        
        Debug.Log("[ServiceManager] ✅ ContainerService initialized");
    }
    
    private void InitializePlayerService()
    {
        if (PlayerService.Instance == null)
        {
            var playerServiceGO = new GameObject("PlayerService");
            playerServiceGO.transform.SetParent(transform);
            var playerService = playerServiceGO.AddComponent<PlayerService>();
            
            // Configure player service
            if (playerPrefab != null)
            {
                playerService.PlayerPrefab = playerPrefab;
            }
            
            PlayerService = playerService;
        }
        else
        {
            PlayerService = PlayerService.Instance;
            
            // Ensure player prefab is set
            if (playerPrefab != null && PlayerService.PlayerPrefab == null)
            {
                PlayerService.PlayerPrefab = playerPrefab;
            }
        }
        
        Debug.Log("[ServiceManager] ✅ PlayerService initialized");
    }
    
    private void SubscribeToServiceEvents()
    {
        // Monitor service events for debugging/logging
        if (EventBus != null)
        {
            EventBus.Subscribe<LevelLoadStarted>(OnLevelLoadStarted);
            EventBus.Subscribe<PlayerSpawned>(OnPlayerSpawned);
            EventBus.Subscribe<ContainerCreated>(OnContainerCreated);
        }
        
        Debug.Log("[ServiceManager] ✅ Subscribed to service events");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (EventBus != null)
        {
            EventBus.Unsubscribe<LevelLoadStarted>(OnLevelLoadStarted);
            EventBus.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
            EventBus.Unsubscribe<ContainerCreated>(OnContainerCreated);
        }
    }
    
    #region Event Handlers
    
    private void OnLevelLoadStarted(LevelLoadStarted eventData)
    {
        Debug.Log($"[ServiceManager] 🎯 Level load started: {eventData.LevelName}");
    }
    
    private void OnPlayerSpawned(PlayerSpawned eventData)
    {
        Debug.Log($"[ServiceManager] 🎮 Player spawned: {eventData.Player?.name} at {eventData.GridPosition}");
    }
    
    private void OnContainerCreated(ContainerCreated eventData)
    {
        Debug.Log($"[ServiceManager] 📁 Container created: {eventData.ContainerType} for {eventData.LevelId}");
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Get service instance by type - for external access
    /// </summary>
    public T GetService<T>() where T : class
    {
        if (typeof(T) == typeof(ILevelDataService)) return LevelDataService as T;
        if (typeof(T) == typeof(IContainerService)) return ContainerService as T;
        if (typeof(T) == typeof(IPlayerService)) return PlayerService as T;
        if (typeof(T) == typeof(GameEventBus)) return EventBus as T;
        if (typeof(T) == typeof(LayeredGridService)) return GridService as T;
        
        Debug.LogWarning($"[ServiceManager] Service of type {typeof(T).Name} not found");
        return null;
    }
    
    /// <summary>
    /// Check if all services are properly initialized
    /// </summary>
    public bool AreAllServicesReady()
    {
        return LevelDataService != null && 
               ContainerService != null && 
               PlayerService != null && 
               EventBus != null && 
               GridService != null;
    }
    
    #endregion
}