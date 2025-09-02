using UnityEngine;

/// <summary>
/// Service implementation for player management
/// Handles all player lifecycle operations and system registrations
/// </summary>
public class PlayerService : MonoBehaviour, IPlayerService
{
    public static IPlayerService Instance { get; private set; }
    
    [Header("Player Configuration")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private SpriteDatabase spriteDatabase;
    
    private PlayerController currentPlayer;
    private Vector2Int playerSpawnPosition;
    private LayeredGridService layeredGrid;
    
    // Events
    public event System.Action<PlayerController> OnPlayerCreated;
    public event System.Action<PlayerController> OnPlayerDestroyed;
    public event System.Action<PlayerController> OnPlayerRegistered;
    public event System.Action<PlayerController> OnPlayerMoved;
    
    // Properties
    public GameObject PlayerPrefab 
    { 
        get => playerPrefab; 
        set => playerPrefab = value; 
    }
    
    public PlayerController CurrentPlayer => currentPlayer;
    
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
    
    private void Start()
    {
        // Subscribe to relevant events
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Subscribe<LevelLoadStarted>(OnLevelLoadStarted);
            GameEventBus.Instance.Subscribe<LevelCleanupStarted>(OnLevelCleanupStarted);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<LevelLoadStarted>(OnLevelLoadStarted);
            GameEventBus.Instance.Unsubscribe<LevelCleanupStarted>(OnLevelCleanupStarted);
        }
    }
    
    private void InitializeService()
    {
        // Get LayeredGridService reference
        layeredGrid = LayeredGridService.Instance;
        if (layeredGrid == null)
        {
            var layeredGridGO = new GameObject("LayeredGridService");
            layeredGrid = layeredGridGO.AddComponent<LayeredGridService>();
        }
    }
    
    #region Player Creation and Destruction
    
    public PlayerController CreatePlayerAtPosition(Vector2Int gridPosition, Vector3 worldPosition)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerService] PlayerPrefab is null! Cannot create player.");
            return null;
        }
        
        // Clean up existing player first
        if (currentPlayer != null)
        {
            DestroyCurrentPlayer();
        }
        
        // Clean any orphaned players from scene
        ClearAllPlayers();
        
        // Create new player instance
        Transform playerParent = GetPlayerContainer();
        GameObject playerGO = Instantiate(playerPrefab, worldPosition, Quaternion.identity, playerParent);
        currentPlayer = playerGO.GetComponent<PlayerController>();
        
        if (currentPlayer == null)
        {
            Debug.LogError("[PlayerService] Created player object doesn't have PlayerController component!");
            Destroy(playerGO);
            return null;
        }
        
        // Setup player visual
        var playerTileBase = playerGO.GetComponent<TileBase>();
        if (playerTileBase != null && spriteDatabase != null)
        {
            playerTileBase.SetVisual(spriteDatabase.GetSprite(TileType.Player));
        }
        
        // Initialize player with position
        currentPlayer.Init(gridPosition.x, gridPosition.y);
        playerSpawnPosition = gridPosition;
        
        // Place in grid system
        if (!PlacePlayerInGrid(currentPlayer, gridPosition))
        {
            Debug.LogError($"[PlayerService] Failed to place player in grid at {gridPosition}");
            Destroy(playerGO);
            currentPlayer = null;
            return null;
        }
        
        // Register with game systems
        RegisterPlayerWithSystems(currentPlayer);
        
        // Publish events
        OnPlayerCreated?.Invoke(currentPlayer);
        GameEventBus.Instance?.Publish(new PlayerSpawned(currentPlayer, gridPosition, worldPosition));
        
        return currentPlayer;
    }
    
    public void DestroyCurrentPlayer()
    {
        if (currentPlayer != null)
        {
            // Unregister from systems first
            UnregisterPlayerFromSystems(currentPlayer);
            
            // Remove from grid
            if (layeredGrid != null)
            {
                layeredGrid.RemoveActor(currentPlayer.gameObject, currentPlayer.X, currentPlayer.Y);
            }
            
            // Publish events
            OnPlayerDestroyed?.Invoke(currentPlayer);
            GameEventBus.Instance?.Publish(new PlayerDestroyed(currentPlayer, "Destroyed"));
            
            // Destroy GameObject
            Destroy(currentPlayer.gameObject);
            currentPlayer = null;
        }
    }
    
    public void ClearAllPlayers()
    {
        var allPlayers = FindObjectsOfType<PlayerController>();
        foreach (var player in allPlayers)
        {
            if (player != currentPlayer)
            {
                Destroy(player.gameObject);
            }
        }
    }
    
    #endregion
    
    #region Player Registration
    
    public void RegisterPlayerWithSystems(PlayerController player)
    {
        if (player == null) return;
        
        // Register with GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(player);
        }
        
        // PlayerAgentManager registration is handled via events in the new system
        
        // Publish registration event
        OnPlayerRegistered?.Invoke(player);
        GameEventBus.Instance?.Publish(new PlayerRegistered(player, true));
    }
    
    public void UnregisterPlayerFromSystems(PlayerController player)
    {
        if (player == null) return;
        
        // Unregister from PlayerAgentManager
        if (PlayerAgentManager.Instance != null)
        {
            PlayerAgentManager.Instance.UnregisterPlayer();
        }
        
        // Additional system unregistrations can be added here
    }
    
    #endregion
    
    #region Player State and Access
    
    public PlayerController GetCurrentPlayer()
    {
        return currentPlayer;
    }
    
    public bool HasCurrentPlayer()
    {
        return currentPlayer != null;
    }
    
    public Vector2Int GetPlayerSpawnPosition()
    {
        return playerSpawnPosition;
    }
    
    #endregion
    
    #region Player Validation
    
    public bool ValidatePlayerSpawnPosition(Vector2Int position)
    {
        if (layeredGrid == null) return false;
        
        // Check if position is within grid bounds
        if (!layeredGrid.IsValidPosition(position.x, position.y))
        {
            return false;
        }
        
        // Check if position is walkable
        if (!layeredGrid.IsWalkable(position.x, position.y))
        {
            return false;
        }
        
        return true;
    }
    
    public bool IsPlayerPositionSafe(Vector2Int position)
    {
        if (layeredGrid == null) return false;
        
        // Check basic walkability
        if (!ValidatePlayerSpawnPosition(position)) return false;
        
        // Check for immediate dangers (bombs, explosions, etc.)
        GameObject bomb = layeredGrid.GetBombAt(position.x, position.y);
        GameObject effect = layeredGrid.GetEffectAt(position.x, position.y);
        
        if (bomb != null || effect != null)
        {
            return false;
        }
        
        return true;
    }
    
    #endregion
    
    #region Player Movement and Placement
    
    public bool TryMovePlayer(Vector2Int fromPos, Vector2Int toPos)
    {
        if (currentPlayer == null || layeredGrid == null) return false;
        
        // Validate destination
        if (!IsPlayerPositionSafe(toPos))
        {
            return false;
        }
        
        // Attempt movement in grid system
        bool moveSuccess = layeredGrid.MoveActor(currentPlayer.gameObject, fromPos.x, fromPos.y, toPos.x, toPos.y);
        
        if (moveSuccess)
        {
            // Update player transform
            Vector3 worldPos = layeredGrid.GridToWorld(toPos.x, toPos.y);
            currentPlayer.transform.position = worldPos;
            
            // Update player internal coordinates
            currentPlayer.Init(toPos.x, toPos.y);
            
            // Publish movement event
            OnPlayerMoved?.Invoke(currentPlayer);
        }
        
        return moveSuccess;
    }
    
    public bool PlacePlayerInGrid(PlayerController player, Vector2Int position)
    {
        if (player == null || layeredGrid == null) return false;
        
        return layeredGrid.PlaceActor(player.gameObject, position.x, position.y);
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnLevelLoadStarted(LevelLoadStarted eventData)
    {
        // Level is starting to load - prepare for cleanup
    }
    
    private void OnLevelCleanupStarted(LevelCleanupStarted eventData)
    {
        // Clean up current player when level cleanup starts
        if (currentPlayer != null)
        {
            UnregisterPlayerFromSystems(currentPlayer);
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private Transform GetPlayerContainer()
    {
        // Use container service if available
        if (ContainerService.Instance != null)
        {
            return ContainerService.Instance.GetContainerForTileType(TileType.Player);
        }
        
        // Fallback to finding a suitable container
        GameObject levelContent = GameObject.Find("[LEVEL CONTENT]");
        if (levelContent != null)
        {
            return levelContent.transform;
        }
        
        // Last resort - create a temporary container
        GameObject tempContainer = new GameObject("[PLAYER CONTAINER]");
        return tempContainer.transform;
    }
    
    #endregion
}