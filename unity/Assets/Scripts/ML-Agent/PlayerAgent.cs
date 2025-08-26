using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;

/// <summary>
/// ML-Agent implementation that integrates with turn-based system through ITurnBased interface.
/// Converts ML-Agent decisions into IGameAction objects for the TurnManager.
/// </summary>
public class PlayerAgent : Agent, ITurnBased
{
    [Header("ML-Agent Settings")]
    public bool useMLAgent = true;
    [Range(1, 20)]
    public float moveSpeed = 5f;
    
    [Header("Training Settings")]
    [Tooltip("Behavior name must match YAML config - Default: PlayerAgent")]
    public string behaviorName = "PlayerAgent";
    
    [Header("Observation Settings")]
    [Range(1, 5)]
    public int observationRadius = 2; // Grid radius around player for observations
    public bool useDistanceObservations = true;
    public bool useGridObservations = true;
    
    [Header("Debug")]
    public bool debugActions = false;
    public bool debugObservations = false;
    
    // Component references
    private PlayerController playerController;
    private RewardSystem rewardSystem;
    private EnvManager envManager;
    
    // ML-Agent state tracking
    private int episodeSteps = 0;
    private Vector2Int lastPlayerPosition;
    private int lastHealth;
    private int lastEnemyCount;
    private float episodeStartTime;
    
    // ITurnBased implementation
    public bool HasActedThisTurn { get; set; }
    private IGameAction pendingAction;
    private bool needsDecision = false;
    
    // Cached values for observations
    private float cachedMapSize = 15f;
    private bool mapSizeCached = false;
    private int cachedInitialEnemyCount = 1;
    private int cachedInitialCollectibleCount = 1;
    private bool levelDataCached = false;
    
    // Action mapping arrays
    private readonly Vector2Int[] moveDirections = new Vector2Int[]
    {
        Vector2Int.zero,        // 0: No movement
        new Vector2Int(0, -1),  // 1: Up (W) - Y negative because Unity grid
        new Vector2Int(1, 0),   // 2: Right (D)
        new Vector2Int(0, 1),   // 3: Down (S) - Y positive because Unity grid  
        new Vector2Int(-1, 0),  // 4: Left (A)
        // Optional diagonal movements:
        new Vector2Int(1, -1),  // 5: Up-Right
        new Vector2Int(1, 1),   // 6: Down-Right
        new Vector2Int(-1, 1),  // 7: Down-Left
        new Vector2Int(-1, -1)  // 8: Up-Left
    };

    //=========================================================================
    // UNITY ML-AGENTS LIFECYCLE
    //=========================================================================
    
    public override void Initialize()
    {
        // Set behavior name for ML-Agents training
        if (!string.IsNullOrEmpty(behaviorName))
        {
            GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorName = behaviorName;
            Debug.Log($"[PlayerAgent] Behavior name set to: {behaviorName}");
        }
        
        // Get required components
        playerController = GetComponent<PlayerController>();
        rewardSystem = GetComponent<RewardSystem>();
        envManager = FindObjectOfType<EnvManager>();
        
        // Validate setup
        if (playerController == null)
        {
            Debug.LogError("[PlayerAgent] PlayerController component required!");
            useMLAgent = false;
            return;
        }
        
        rewardSystem = FindObjectOfType<RewardSystem>();
        if (rewardSystem == null)
        {
            Debug.LogWarning("[PlayerAgent] RewardSystem not found immediately. Starting delayed search...");
            StartCoroutine(DelayedRewardSystemSearch());
        }
        else
        {
            Debug.Log("[PlayerAgent] RewardSystem found immediately!");
        }
        
        if (envManager == null)
        {
            Debug.LogWarning("[PlayerAgent] EnvManager not found. Some observations may not work.");
        }
        
        // Setup ML-Agent integration
        if (useMLAgent)
        {
            // Keep reference for backward compatibility
            playerController.mlAgent = this;
            playerController.useMLAgent = useMLAgent;
            
            // Register with TurnManager for turn-based control
            // Note: PlayerController will unregister itself when ML-Agent takes over
            TurnManager.Instance?.Unregister(playerController);
            TurnManager.Instance?.Register(this);
            Debug.Log("[PlayerAgent] ML-Agent mode activated and registered with TurnManager!");
        }
    }
    IEnumerator DelayedRewardSystemSearch()
    {
        for (int i = 0; i < 5; i++) // 5 deneme
        {
            yield return new WaitForSeconds(0.2f * (i + 1)); // Artan bekleme
            
            rewardSystem = FindObjectOfType<RewardSystem>();
            if (rewardSystem != null)
            {
                Debug.Log($"[PlayerAgent] RewardSystem found after {i + 1} attempts!");
                break;
            }
            
            // RL TRAINING objesinde spesifik arama
            GameObject rlTraining = GameObject.Find("RL TRAINING");
            if (rlTraining != null)
            {
                rewardSystem = rlTraining.GetComponent<RewardSystem>();
                if (rewardSystem != null)
                {
                    Debug.Log("[PlayerAgent] RewardSystem found in RL TRAINING object!");
                    break;
                }
            }
        }
        
        if (rewardSystem == null)
        {
            Debug.LogError("[PlayerAgent] RewardSystem still not found after multiple attempts!");
        }
    }
    public override void OnEpisodeBegin()
    {
        if (debugActions) Debug.Log("[PlayerAgent] Episode starting...");
        
        // Reset episode tracking
        episodeSteps = 0;
        episodeStartTime = Time.time;
        
        // Initialize tracking variables
        if (playerController != null)
        {
            lastPlayerPosition = new Vector2Int(playerController.X, playerController.Y);
            lastHealth = playerController.CurrentHealth;
        }
        
        if (envManager != null)
        {
            lastEnemyCount = envManager.GetRemainingEnemyCount();
        }
        
        // Reset reward system
        if (rewardSystem != null)
        {
            rewardSystem.OnEpisodeBegin();
        }
        
        // Cache level data for observations
        CacheLevelData();
        
        // Subscribe to level change events
        if (LevelLoader.instance != null)
        {
            LevelLoader.instance.OnEnemyListChanged += InvalidateEnemyCache;
            LevelLoader.instance.OnCollectibleListChanged += InvalidateCollectibleCache;
        }
        
        if (debugActions) Debug.Log($"[PlayerAgent] Episode began at position ({lastPlayerPosition.x}, {lastPlayerPosition.y})");
    }
    
    private void CacheLevelData()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.GetCurrentLevelData() != null)
        {
            var levelData = LevelManager.Instance.GetCurrentLevelData();
            cachedMapSize = Mathf.Max(levelData.width, levelData.height);
            cachedInitialEnemyCount = Mathf.Max(1, levelData.enemyPositions.Count);
            cachedInitialCollectibleCount = Mathf.Max(1, levelData.collectiblePositions.Count);
            levelDataCached = true;
            mapSizeCached = true;
        }
        else
        {
            cachedMapSize = 15f; // Default fallback
            cachedInitialEnemyCount = 1;
            cachedInitialCollectibleCount = 1;
            levelDataCached = false;
            mapSizeCached = false;
        }
    }
    
    private void InvalidateEnemyCache()
    {
        // Enemy count changed, but we don't need to invalidate initial count cache
        if (debugObservations) Debug.Log("[PlayerAgent] Enemy list changed");
    }
    
    private void InvalidateCollectibleCache()
    {
        // Collectible count changed, but we don't need to invalidate initial count cache
        if (debugObservations) Debug.Log("[PlayerAgent] Collectible list changed");
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        var discreteActions = actions.DiscreteActions;
        
        if (discreteActions.Length == 0)
        {
            Debug.LogError("OnActionReceived: DiscreteActions array boş!");
            return;
        }
        
        if (!useMLAgent || playerController == null || !needsDecision) return;
        
        episodeSteps++;
        needsDecision = false;
        
        int moveAction = discreteActions[0];
        Debug.Log($"Received action: {moveAction}");
        
        // Parse discrete actions
        int moveActionIndex = discreteActions[0]; // 0-8: movement directions
        int bombActionIndex = discreteActions.Length > 1 ? discreteActions[1] : 0; // 0-1: bomb placement
        
        Debug.Log($"[PlayerAgent] MoveIndex: {moveActionIndex}, BombIndex: {bombActionIndex}");
        
        // Convert to IGameAction
        pendingAction = CreateGameAction(moveActionIndex, bombActionIndex);
        
        Debug.Log($"[PlayerAgent] PendingAction: {(pendingAction != null ? pendingAction.GetType().Name : "NULL")}");
        
        // Debug logging
        if (debugActions)
        {
            Debug.Log($"[PlayerAgent] Step {episodeSteps}: Action created - {pendingAction?.GetType().Name}");
        }
        
        // Apply step-based penalties and rewards
        ApplyStepRewards();
        
        // Check for episode termination conditions
        CheckEpisodeTermination();
    }
    
    private IGameAction CreateGameAction(int moveActionIndex, int bombActionIndex)
    {
        // Priority: Bomb action > Move action > No action
        if (bombActionIndex == 1)
        {
            // Place bomb at current position
            Vector2Int bombDirection = Vector2Int.zero;
            return new PlaceBombAction(playerController, bombDirection);
        }
        
        Vector2Int moveDirection = ConvertMoveAction(moveActionIndex);
        if (moveDirection != Vector2Int.zero)
        {
            return new MoveAction(playerController, moveDirection);
        }
        
        // "No movement" is also a valid action - create MoveAction with zero vector
        return new MoveAction(playerController, Vector2Int.zero);
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerController == null) return;
        
        int observationCount = 0;
        
        // === PLAYER STATE OBSERVATIONS ===
        // Player position (normalized)
        var ll = LevelLoader.instance;
        if (ll != null)
        {
            sensor.AddObservation((float)playerController.X / ll.Width);
            sensor.AddObservation((float)playerController.Y / ll.Height);
            observationCount += 2;
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            observationCount += 2;
        }
        
        // Player health (normalized)
        float healthRatio = playerController.MaxHealth > 0 ? 
            (float)playerController.CurrentHealth / playerController.MaxHealth : 0f;
        sensor.AddObservation(healthRatio);
        observationCount += 1;
        
        // Player velocity/last move direction
        Vector2Int lastMove = GetLastMoveDirection();
        sensor.AddObservation((float)lastMove.x);
        sensor.AddObservation((float)lastMove.y);
        observationCount += 2;
        
        // === GAME STATE OBSERVATIONS ===
        if (envManager != null)
        {
            // Get current counts
            int currentEnemyCount = envManager.GetRemainingEnemyCount();
            int currentCollectibleCount = envManager.GetRemainingCollectibleCount();
            
            // Use cached initial counts for better performance
            float enemyRatio = (float)currentEnemyCount / cachedInitialEnemyCount;
            sensor.AddObservation(enemyRatio);
            
            // Remaining collectibles (normalized)  
            float collectibleRatio = (float)currentCollectibleCount / cachedInitialCollectibleCount;
            sensor.AddObservation(collectibleRatio);
            observationCount += 2;
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            observationCount += 2;
        }
        
        // === GRID-BASED OBSERVATIONS ===
        if (useGridObservations)
        {
            observationCount += CollectGridObservations(sensor);
        }
        
        // === DISTANCE-BASED OBSERVATIONS ===
        if (useDistanceObservations)
        {
            observationCount += CollectDistanceObservations(sensor);
        }
        
        if (debugObservations)
        {
            Debug.Log($"[PlayerAgent] Collected {observationCount} observations");
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        
        if (discreteActionsOut.Length == 0) return;
        
        int action = 0;
        if (Input.GetKey(KeyCode.W)) action = 1;
        else if (Input.GetKey(KeyCode.S)) action = 2;
        else if (Input.GetKey(KeyCode.A)) action = 3;
        else if (Input.GetKey(KeyCode.D)) action = 4;
        
        discreteActionsOut[0] = action;
    }
        
    //=========================================================================
    // OBSERVATION COLLECTION HELPERS
    //=========================================================================
    
    private int CollectGridObservations(VectorSensor sensor)
    {
        var ll = LevelLoader.instance;
        if (ll == null) return 0;
        
        int observationCount = 0;
        Vector2Int playerPos = new Vector2Int(playerController.X, playerController.Y);
        
        // Collect observations in a grid around the player
        for (int y = -observationRadius; y <= observationRadius; y++)
        {
            for (int x = -observationRadius; x <= observationRadius; x++)
            {
                Vector2Int checkPos = playerPos + new Vector2Int(x, y);
                
                // Bounds check
                if (checkPos.x < 0 || checkPos.x >= ll.Width || 
                    checkPos.y < 0 || checkPos.y >= ll.Height)
                {
                    // Out of bounds = solid wall
                    sensor.AddObservation(1f); // Wall
                    sensor.AddObservation(0f); // No enemy
                    sensor.AddObservation(0f); // No collectible
                    sensor.AddObservation(0f); // No bomb
                    observationCount += 4;
                    continue;
                }
                
                // Tile type observation
                var tileType = TileSymbols.DataSymbolToType(ll.levelMap[checkPos.x, checkPos.y]);
                sensor.AddObservation(GetTileTypeValue(tileType));
                observationCount += 1;
                
                // Object observations
                var obj = ll.tileObjects[checkPos.x, checkPos.y];
                
                // Enemy detection
                bool hasEnemy = obj != null && 
                    (obj.GetComponent<EnemyTile>() != null || obj.GetComponent<EnemyShooterTile>() != null);
                sensor.AddObservation(hasEnemy ? 1f : 0f);
                observationCount += 1;
                
                // Collectible detection  
                bool hasCollectible = obj != null && obj.GetComponent<ICollectible>() != null;
                sensor.AddObservation(hasCollectible ? 1f : 0f);
                observationCount += 1;
                
                // Bomb detection
                bool hasBomb = obj != null && obj.GetComponent<BombTile>() != null;
                sensor.AddObservation(hasBomb ? 1f : 0f);
                observationCount += 1;
            }
        }
        
        return observationCount;
    }
    
    private int CollectDistanceObservations(VectorSensor sensor)
    {
        int observationCount = 0;
        
        if (envManager != null)
        {
            Vector3 playerPos = transform.position;
            
            // Nearest enemy distance and direction
            Vector2 nearestEnemy = envManager.GetNearestEnemyPosition(playerPos);
            if (nearestEnemy != Vector2.zero)
            {
                Vector2 enemyDirection = (nearestEnemy - (Vector2)playerPos).normalized;
                float enemyDistance = Vector2.Distance(playerPos, nearestEnemy) / cachedMapSize;
                sensor.AddObservation(enemyDirection.x);
                sensor.AddObservation(enemyDirection.y);
                sensor.AddObservation(Mathf.Clamp01(enemyDistance));
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
            }
            observationCount += 3;
            
            // Nearest collectible distance and direction
            Vector2 nearestCollectible = envManager.GetNearestCollectiblePosition(playerPos);
            if (nearestCollectible != Vector2.zero)
            {
                Vector2 collectibleDirection = (nearestCollectible - (Vector2)playerPos).normalized;
                float collectibleDistance = Vector2.Distance(playerPos, nearestCollectible) / cachedMapSize;
                sensor.AddObservation(collectibleDirection.x);
                sensor.AddObservation(collectibleDirection.y);
                sensor.AddObservation(Mathf.Clamp01(collectibleDistance));
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
            }
            observationCount += 3;
            
            // Exit distance and direction
            Vector2 exitPos = envManager.GetExitPosition();
            Vector2 exitDirection = (exitPos - (Vector2)playerPos).normalized;
            float exitDistance = Vector2.Distance(playerPos, exitPos) / cachedMapSize;
            sensor.AddObservation(exitDirection.x);
            sensor.AddObservation(exitDirection.y);
            sensor.AddObservation(Mathf.Clamp01(exitDistance));
            observationCount += 3;
        }
        else
        {
            // Add zeros if no EnvManager
            for (int i = 0; i < 9; i++)
            {
                sensor.AddObservation(0f);
            }
            observationCount += 9;
        }
        
        return observationCount;
    }
    
    //=========================================================================
    // REWARD SYSTEM INTEGRATION
    //=========================================================================
    
    private void ApplyStepRewards()
    {
        if (rewardSystem == null) return;
        
        // Update distance-based rewards
        rewardSystem.UpdateRewards();
        
        // Check for state changes that warrant rewards
        Vector2Int currentPos = new Vector2Int(playerController.X, playerController.Y);
        
        // Movement reward (exploration)
        if (currentPos != lastPlayerPosition)
        {
            rewardSystem.ApplyExplorationReward();
            lastPlayerPosition = currentPos;
        }
        
        // Health change detection (now handled by RewardSystem event subscription)
        lastHealth = playerController.CurrentHealth;
        
        // Enemy count change detection
        if (envManager != null)
        {
            int currentEnemyCount = envManager.GetRemainingEnemyCount();
            if (currentEnemyCount < lastEnemyCount)
            {
                // Enemy killed
                int enemiesKilled = lastEnemyCount - currentEnemyCount;
                for (int i = 0; i < enemiesKilled; i++)
                {
                    rewardSystem.ApplyEnemyKillReward();
                }
                lastEnemyCount = currentEnemyCount;
            }
        }
    }
    
    private void CheckEpisodeTermination()
    {
        // Death condition
        if (playerController.CurrentHealth <= 0)
        {
            if (rewardSystem != null)
                rewardSystem.ApplyDeathPenalty();
            
            if (debugActions) Debug.Log("[PlayerAgent] Episode ended: Player died");
            EndEpisode();
            return;
        }
        
        // Victory condition (all enemies defeated and reached exit)
        if (envManager != null)
        {
            if (envManager.GetRemainingEnemyCount() == 0)
            {
                Vector2Int playerPos = new Vector2Int(playerController.X, playerController.Y);
                Vector2Int exitPos = new Vector2Int(
                    Mathf.RoundToInt(envManager.GetExitPosition().x),
                    Mathf.RoundToInt(envManager.GetExitPosition().y)
                );
                
                if (playerPos == exitPos)
                {
                    if (rewardSystem != null)
                        rewardSystem.ApplyLevelCompleteReward();
                    
                    if (debugActions) Debug.Log("[PlayerAgent] Episode ended: Level completed!");
                    EndEpisode();
                    return;
                }
            }
        }
        
        // Timeout condition
        if (episodeSteps >= 3000) // Max steps from config
        {
            if (rewardSystem != null)
                rewardSystem.ApplyTimeoutPenalty();
            
            if (debugActions) Debug.Log("[PlayerAgent] Episode ended: Timeout");
            EndEpisode();
            return;
        }
    }
    
    //=========================================================================
    // HELPER METHODS
    //=========================================================================
    
    private Vector2Int ConvertMoveAction(int actionIndex)
    {
        if (actionIndex >= 0 && actionIndex < moveDirections.Length)
        {
            return moveDirections[actionIndex];
        }
        return Vector2Int.zero;
    }
    
    private float GetTileTypeValue(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Empty: return 0f;
            case TileType.Breakable: return 0.5f;
            case TileType.Wall: return 1f;
            case TileType.Stairs: return 0.2f;
            default: return 0f;
        }
    }
    
    private Vector2Int GetLastMoveDirection()
    {
        // Access last move direction from PlayerController
        // This uses reflection as lastMoveDirection is private
        var field = typeof(PlayerController).GetField("lastMoveDirection", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return (Vector2Int)field.GetValue(playerController);
        }
        
        return Vector2Int.zero;
    }
    
    //=========================================================================
    // PUBLIC INTERFACE FOR DEBUGGING
    //=========================================================================
    
    /// <summary>
    /// Get current episode statistics for debugging
    /// </summary>
    public string GetEpisodeStats()
    {
        float episodeTime = Time.time - episodeStartTime;
        return $"Episode: {episodeSteps} steps, {episodeTime:F1}s, " +
               $"Health: {playerController.CurrentHealth}/{playerController.MaxHealth}, " +
               $"Enemies: {(envManager != null ? envManager.GetRemainingEnemyCount() : 0)}";
    }
    
    /// <summary>
    /// Force episode end (for debugging)
    /// </summary>
    public void ForceEndEpisode()
    {
        Debug.Log("[PlayerAgent] Episode manually ended");
        EndEpisode();
    }
    
    //=========================================================================
    // ITURNBASED INTERFACE IMPLEMENTATION
    //=========================================================================
    
    public void ResetTurn()
    {
        HasActedThisTurn = false;
        pendingAction = null;
        needsDecision = false;
        Debug.Log("[PlayerAgent] Turn reset - HasActedThisTurn = false");
    }
    
    public IGameAction GetAction()
    {
        if (!useMLAgent || playerController == null)
        {
            // Return null action if ML-Agent is disabled
            return null;
        }
        
        Debug.Log($"[PlayerAgent] GetAction called - HasActedThisTurn: {HasActedThisTurn}, PendingAction: {(pendingAction != null ? pendingAction.GetType().Name : "NULL")}");
        
        // Check if we already have a pending action from ML decision
        if (pendingAction != null)
        {
            IGameAction action = pendingAction;
            pendingAction = null;
            HasActedThisTurn = true;
            Debug.Log($"[PlayerAgent] Returning action: {action.GetType().Name}");
            return action;
        }
        
        // Always request new decision - simple approach
        Debug.Log("[PlayerAgent] RequestDecision called - requesting fresh decision");
        RequestDecision();
        
        // TEMPORARY SOLUTION: Manual simulation until OnActionReceived works
        // TODO: Remove this when ML-Agents Python connection is fixed
        
        Debug.Log("[PlayerAgent] [TEMP] Using manual simulation - ML-Agents not connected");
        
        // Apply step rewards (same as real ML-Agents would do) 
        if (rewardSystem != null)
        {
            ApplyStepRewards();
        }
        
        // Simulate discrete actions 
        int simulatedMoveAction = UnityEngine.Random.Range(0, 9); 
        int simulatedBombAction = UnityEngine.Random.Range(0, 2);
        
        Debug.Log($"[PlayerAgent] [TEMP] Simulated MoveIndex: {simulatedMoveAction}, BombIndex: {simulatedBombAction}");
        
        // Use real CreateGameAction logic
        IGameAction simulatedAction = CreateGameAction(simulatedMoveAction, simulatedBombAction);
        
        if (simulatedAction != null)
        {
            HasActedThisTurn = true;
            Debug.Log($"[PlayerAgent] [TEMP] Returning simulated action: {simulatedAction.GetType().Name}");
            return simulatedAction;
        }
        
        return null;
    }
    
    private void OnDestroy()
    {
        // Unregister from TurnManager when destroyed
        if (useMLAgent && TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
        
        // Unsubscribe from events
        if (LevelLoader.instance != null)
        {
            LevelLoader.instance.OnEnemyListChanged -= InvalidateEnemyCache;
            LevelLoader.instance.OnCollectibleListChanged -= InvalidateCollectibleCache;
        }
    }
}