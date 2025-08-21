using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

/// <summary>
/// ML-Agent implementation that controls PlayerController through composition pattern.
/// Uses minimal interface to inject actions into existing turn-based system.
/// </summary>
public class PlayerAgent : Agent
{
    [Header("ML-Agent Settings")]
    public bool useMLAgent = true;
    [Range(1, 20)]
    public float moveSpeed = 5f;
    
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
        
        if (rewardSystem == null)
        {
            Debug.LogWarning("[PlayerAgent] RewardSystem not found. Rewards will not be applied.");
        }
        
        if (envManager == null)
        {
            Debug.LogWarning("[PlayerAgent] EnvManager not found. Some observations may not work.");
        }
        
        // Setup cross-references
        if (useMLAgent)
        {
            playerController.mlAgent = this;
            playerController.useMLAgent = useMLAgent;
            Debug.Log("[PlayerAgent] ML-Agent mode activated!");
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
        
        if (debugActions) Debug.Log($"[PlayerAgent] Episode began at position ({lastPlayerPosition.x}, {lastPlayerPosition.y})");
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!useMLAgent || playerController == null) return;
        
        episodeSteps++;
        
        // Parse discrete actions
        int moveActionIndex = actions.DiscreteActions[0]; // 0-8: movement directions
        int bombActionIndex = actions.DiscreteActions[1]; // 0-1: bomb placement
        
        // Convert to game actions
        Vector2Int moveAction = ConvertMoveAction(moveActionIndex);
        bool bombAction = bombActionIndex == 1;
        
        // Inject actions into PlayerController
        playerController.SetMLMoveIntent(moveAction);
        playerController.SetMLBombIntent(bombAction);
        
        // Debug logging
        if (debugActions)
        {
            Debug.Log($"[PlayerAgent] Step {episodeSteps}: Move={moveAction}, Bomb={bombAction}");
        }
        
        // Apply step-based penalties and rewards
        ApplyStepRewards();
        
        // Check for episode termination conditions
        CheckEpisodeTermination();
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
            // Remaining enemies (normalized)
            float enemyRatio = envManager.enemyCount > 0 ? 
                (float)envManager.GetRemainingEnemyCount() / envManager.enemyCount : 0f;
            sensor.AddObservation(enemyRatio);
            
            // Remaining collectibles (normalized)  
            float collectibleRatio = envManager.collectibleCount > 0 ?
                (float)envManager.GetRemainingCollectibleCount() / envManager.collectibleCount : 0f;
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
        // Manual control for testing and debugging
        var discreteActions = actionsOut.DiscreteActions;
        
        // Movement (WASD keys)
        discreteActions[0] = 0; // Default: no movement
        
        if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.D)) 
            discreteActions[0] = 5; // Up-Right
        else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.D)) 
            discreteActions[0] = 6; // Down-Right
        else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.A)) 
            discreteActions[0] = 7; // Down-Left
        else if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.A)) 
            discreteActions[0] = 8; // Up-Left
        else if (Input.GetKey(KeyCode.W)) 
            discreteActions[0] = 1; // Up
        else if (Input.GetKey(KeyCode.D)) 
            discreteActions[0] = 2; // Right
        else if (Input.GetKey(KeyCode.S)) 
            discreteActions[0] = 3; // Down
        else if (Input.GetKey(KeyCode.A)) 
            discreteActions[0] = 4; // Left
        
        // Bomb placement (Space key)
        discreteActions[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;
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
                float enemyDistance = Vector2.Distance(playerPos, nearestEnemy) / envManager.MapWidth;
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
                float collectibleDistance = Vector2.Distance(playerPos, nearestCollectible) / envManager.MapWidth;
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
            float exitDistance = Vector2.Distance(playerPos, exitPos) / envManager.MapWidth;
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
        
        // Health change detection
        if (playerController.CurrentHealth != lastHealth)
        {
            if (playerController.CurrentHealth < lastHealth)
            {
                // Took damage
                int damage = lastHealth - playerController.CurrentHealth;
                rewardSystem.ApplyDamageReward(damage);
            }
            else
            {
                // Healed
                int healing = playerController.CurrentHealth - lastHealth;
                rewardSystem.ApplyCollectibleReward(CollectibleType.Health);
            }
            lastHealth = playerController.CurrentHealth;
        }
        
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
}