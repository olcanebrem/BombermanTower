using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using Debug = UnityEngine.Debug;

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
    public bool debugActions = true;
    public bool debugObservations = false;
    
    [Header("Heuristic Control")]
    public bool useRandomHeuristic = true;
    public bool enableManualInput = false;
    
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
    
    // Debug Panel Properties
    public int CurrentMoveIndex { get; private set; } = 0;
    public int CurrentBombIndex { get; private set; } = 0;
    public Vector2Int LastActionDirection { get; private set; } = Vector2Int.zero;
    public string CurrentActionType { get; private set; } = "None";
    
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
        // Configure Academy for turn-based gameplay
        var academy = Unity.MLAgents.Academy.Instance;
        if (academy != null)
        {
            // Disable automatic stepping - we'll control it manually via TurnManager
            academy.AutomaticSteppingEnabled = false;
            Debug.Log("[PlayerAgent] Academy automatic stepping disabled - turn-based control active");
        }
        
        // Set behavior name for ML-Agents training
        if (!string.IsNullOrEmpty(behaviorName))
        {
            GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorName = behaviorName;
            Debug.Log($"[PlayerAgent] Behavior name set to: {behaviorName}");
        }
        
        // Validate action space setup
        var behaviorParameters = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (behaviorParameters != null)
        {
            var actionSpec = behaviorParameters.BrainParameters.ActionSpec;
            Debug.Log($"[PlayerAgent] Action Space - Discrete Actions: {actionSpec.NumDiscreteActions}, Branch sizes: [{string.Join(", ", actionSpec.BranchSizes)}]");
            
            if (actionSpec.NumDiscreteActions != 2)
            {
                Debug.LogWarning($"[PlayerAgent] Expected 2 discrete actions (move + bomb), but found {actionSpec.NumDiscreteActions}. Please check BehaviorParameters in Inspector.");
            }
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
        
        // Setup ML-Agent integration - now centralized through TurnManager
        
        if (UseMLAgent)
        {
            // Unregister PlayerController and register ML-Agent centrally
            TurnManager.Instance?.Unregister(playerController);
            TurnManager.Instance?.RegisterMLAgent(this);
            Debug.Log("[PlayerAgent] ML-Agent registered centrally through TurnManager");
            
            // Debug: Check if registration was successful
            if (TurnManager.Instance != null)
            {
                var turnObjects = TurnManager.Instance.GetTurnBasedObjects();
                bool isRegistered = false;
                foreach (var obj in turnObjects)
                {
                    if (obj == this)
                    {
                        isRegistered = true;
                        break;
                    }
                }
                Debug.Log($"[PlayerAgent] Registration check - Is registered: {isRegistered}, Total turn objects: {turnObjects.Count}");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerAgent] UseMLAgent is FALSE - ML-Agent will NOT be registered for turn-based control!");
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
        
        // Reset environment
        if (envManager != null)
        {
            envManager.ResetEnvironment();
        }
        
        // Re-find RewardSystem if lost during scene lifecycle
        if (rewardSystem == null)
        {
            rewardSystem = FindObjectOfType<RewardSystem>();
            Debug.Log($"[PlayerAgent] RewardSystem re-found in OnEpisodeBegin: {(rewardSystem != null ? "SUCCESS" : "FAILED")}");
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
        Debug.Log($"[PlayerAgent] 🐍 PYTHON → OnActionReceived CALLED! Episode Step: {episodeSteps + 1} - Starting action processing");
        
        var discreteActions = actions.DiscreteActions;
        
        if (discreteActions.Length == 0)
        {
            Debug.LogError("OnActionReceived: DiscreteActions array boş!");
            return;
        }
        
        Debug.Log($"[PlayerAgent] 🐍 Python Raw Actions: [{string.Join(", ", discreteActions)}] - useMLAgent: {UseMLAgent}, playerController: {(playerController != null ? "OK" : "NULL")}");
        
        if (!UseMLAgent || playerController == null) 
        {
            Debug.LogError($"[PlayerAgent] ❌ EARLY RETURN! UseMLAgent: {UseMLAgent}, playerController: {(playerController != null ? "OK" : "NULL")}");
            return;
        }
        
        Debug.Log("[PlayerAgent] ✅ Checks passed - continuing to create action...");
        
        episodeSteps++;
        needsDecision = false;
        
        int moveAction = discreteActions[0];
        Debug.Log($"Received action: {moveAction}");
        
        // Parse discrete actions
        int moveActionIndex = discreteActions[0]; // 0-8: movement directions
        int bombActionIndex = discreteActions.Length > 1 ? discreteActions[1] : 0; // 0-1: bomb placement
        
        // Update debug properties
        CurrentMoveIndex = moveActionIndex;
        CurrentBombIndex = bombActionIndex;
        
        Debug.Log($"[PlayerAgent] Received Actions - MoveIndex: {moveActionIndex}, BombIndex: {bombActionIndex} (DiscreteActions.Length: {discreteActions.Length})");
        Debug.Log($"[PlayerAgent] Will create bomb: {bombActionIndex >= 1}");
        
        // Debug: Show all received actions
        if (debugActions)
        {
            for (int i = 0; i < discreteActions.Length; i++)
            {
                Debug.Log($"[PlayerAgent] DiscreteAction[{i}] = {discreteActions[i]}");
            }
        }
        
        // Convert to IGameAction
        pendingAction = CreateGameAction(moveActionIndex, bombActionIndex);
        
        // Update debug info
        if (pendingAction != null)
        {
            CurrentActionType = pendingAction.GetType().Name;
            if (pendingAction is MoveAction moveActionDebug)
            {
                LastActionDirection = moveActionDebug.Direction;
                Debug.Log($"[PlayerAgent] 🏃 Created MoveAction with direction: {moveActionDebug.Direction}");
            }
            else if (pendingAction is PlaceBombAction)
            {
                LastActionDirection = Vector2Int.zero; // Bomb at current position
                Debug.Log($"[PlayerAgent] 💣 Created PlaceBombAction");
            }
        }
        else
        {
            CurrentActionType = "None";
            LastActionDirection = Vector2Int.zero;
            Debug.Log($"[PlayerAgent] ❌ No action created");
        }
        
        Debug.Log($"[PlayerAgent] ⚙️ PendingAction stored: {(pendingAction != null ? pendingAction.GetType().Name : "NULL")}");
        
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
        Debug.Log($"[PlayerAgent] CreateGameAction - Move: {moveActionIndex}, Bomb: {bombActionIndex}");
        
        // Priority: Move action > Bomb action > No action (Movement first!)
        Vector2Int moveDirection = ConvertMoveAction(moveActionIndex);
        Debug.Log($"[PlayerAgent] ConvertMoveAction({moveActionIndex}) returned: {moveDirection}");
        
        if (moveDirection != Vector2Int.zero)
        {
            Debug.Log($"[PlayerAgent] Creating MoveAction with direction: {moveDirection}");
            return new MoveAction(playerController, moveDirection);
        }
        
        if (bombActionIndex >= 1)
        {
            // Place bomb at nearby empty position (not on player)
            Vector2Int bombDirection = FindBombPlacement();
            Debug.Log($"[PlayerAgent] Creating PlaceBombAction with direction: {bombDirection}");
            return new PlaceBombAction(playerController, bombDirection);
        }
        
        // "No movement" is also a valid action - create MoveAction with zero vector
        Debug.Log("[PlayerAgent] No movement or bomb action - creating MoveAction with zero vector");
        return new MoveAction(playerController, Vector2Int.zero);
    }
    
    private Vector2Int FindBombPlacement()
    {
        // Check adjacent tiles for empty space to place bomb
        Vector2Int[] directions = { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        foreach (var dir in directions)
        {
            int targetX = playerController.X + dir.x;
            int targetY = playerController.Y + dir.y;
            
            var ll = LevelLoader.instance;
            if (ll != null && targetX >= 0 && targetX < ll.Width && targetY >= 0 && targetY < ll.Height)
            {
                if (TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY]) == TileType.Empty)
                {
                    Debug.Log($"[PlayerAgent] Found empty space for bomb at direction: {dir}");
                    return dir;
                }
            }
        }
        
        Debug.Log("[PlayerAgent] No empty space found for bomb, using current position");
        return Vector2Int.zero; // Fallback to current position
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
        
        Debug.Log($"[PlayerAgent] 🎯 HEURISTIC → Heuristic called - DiscreteActions length: {discreteActionsOut.Length}");
        
        if (discreteActionsOut.Length == 0) 
        {
            Debug.LogError("[PlayerAgent] Heuristic: DiscreteActions length is 0!");
            return;
        }
        
        // Test: Simple pattern - alternating movement and bomb
        int moveAction = 0;
        int bombAction = 0;
        
        if (enableManualInput)
        {
            // Try Unity's new Input System first
            if (Input.GetKey(KeyCode.W)) moveAction = 1;
            else if (Input.GetKey(KeyCode.D)) moveAction = 2; 
            else if (Input.GetKey(KeyCode.S)) moveAction = 3;
            else if (Input.GetKey(KeyCode.A)) moveAction = 4;
            
            bombAction = Input.GetKey(KeyCode.Space) ? 1 : 0;
        }
        else if (useRandomHeuristic)
        {
            // Random test pattern for debugging
            int randomChoice = UnityEngine.Random.Range(0, 20);
            if (randomChoice < 4) moveAction = 1; // Up (20% chance)
            else if (randomChoice < 8) moveAction = 2; // Right (20% chance)
            else if (randomChoice < 12) moveAction = 3; // Down (20% chance)
            else if (randomChoice < 16) moveAction = 4; // Left (20% chance)
            // Else stay still (20% chance)
            
            bombAction = (randomChoice >= 18) ? 1 : 0; // 10% bomb chance
            
            Debug.Log($"[PlayerAgent] Random heuristic - Choice: {randomChoice}, Move: {moveAction}, Bomb: {bombAction}");
        }
        
        discreteActionsOut[0] = moveAction;
        if (discreteActionsOut.Length > 1)
        {
            discreteActionsOut[1] = bombAction;
        }
        
        Debug.Log($"[PlayerAgent] Heuristic - Move: {moveAction}, Bomb: {bombAction}");
        Debug.Log($"[PlayerAgent] Input check - W:{Input.GetKey(KeyCode.W)}, A:{Input.GetKey(KeyCode.A)}, S:{Input.GetKey(KeyCode.S)}, D:{Input.GetKey(KeyCode.D)}, Space:{Input.GetKey(KeyCode.Space)}");
        
        if (debugActions && (moveAction != 0 || bombAction != 0))
        {
            Debug.Log($"[PlayerAgent] Heuristic action applied - Move: {moveAction}, Bomb: {bombAction}");
        }
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
        // Ensure RewardSystem is always available
        if (rewardSystem == null)
        {
            rewardSystem = FindObjectOfType<RewardSystem>();
            if (rewardSystem == null)
            {
                Debug.LogWarning("[PlayerAgent] RewardSystem not found during ApplyStepRewards!");
                return;
            }
        }
        
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
            
            Debug.Log("[PlayerAgent] Episode ended: Player died - ending episode only");
            EndEpisode();
            
            // Note: Level restart will be handled by TurnManager.HandlePlayerDeathEvent
            // through PlayerController.OnPlayerDied event - no need to restart here
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
                    
                    // Load next level for successful completion
                    if (TurnManager.Instance?.IsMLAgentActive == true)
                    {
                        StartCoroutine(LoadNextLevelDelayed());
                    }
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
            
            // Load next level for timeout (treated like death)
            if (TurnManager.Instance?.IsMLAgentActive == true)
            {
                StartCoroutine(LoadNextLevelDelayed());
            }
            return;
        }
    }
    
    /// <summary>
    /// Ensures RewardSystem is always available, finds it if lost
    /// </summary>
    private RewardSystem GetRewardSystem()
    {
        if (rewardSystem == null)
        {
            rewardSystem = FindObjectOfType<RewardSystem>();
            if (rewardSystem != null)
            {
                Debug.Log("[PlayerAgent] RewardSystem re-found and reconnected!");
            }
            else
            {
                Debug.LogError("[PlayerAgent] RewardSystem NOT FOUND in scene!");
            }
        }
        return rewardSystem;
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
            case TileType.Gate: return 0.2f;
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
        
        // Manual episode restart due to manual Academy stepping
        StartCoroutine(RestartEpisodeDelayed());
    }
    
    /// <summary>
    /// Load next level with delay for ML training
    /// </summary>
    private System.Collections.IEnumerator LoadNextLevelDelayed()
    {
        // Wait a frame for EndEpisode to complete
        yield return null;
        
        Debug.Log("[PlayerAgent] Loading next level in training sequence");
        
        // Load next level directly through LevelSequencer
        if (LevelSequencer.Instance != null)
        {
            LevelSequencer.Instance.LoadNextLevel();
        }
        else
        {
            Debug.LogWarning("[PlayerAgent] LevelSequencer not found for next level loading");
        }
    }
    
    /// <summary>
    /// Restart episode with delay for manual Academy stepping mode
    /// </summary>
    private System.Collections.IEnumerator RestartEpisodeDelayed()
    {
        // Wait a frame for EndEpisode to complete
        yield return null;
        
        Debug.Log("[PlayerAgent] Restarting episode manually");
        
        // Trigger level reload via TurnManager or LevelSequencer
        if (TurnManager.Instance != null && TurnManager.Instance.IsMLAgentActive)
        {
            // In ML training mode, let TurnManager handle the restart
            Debug.Log("[PlayerAgent] Calling TurnManager.HandlePlayerDeathEvent");
            TurnManager.Instance.HandlePlayerDeathEvent(playerController);
        }
        else
        {
            // Manual restart - call OnEpisodeBegin directly
            Debug.Log("[PlayerAgent] Manual restart - calling OnEpisodeBegin");
            OnEpisodeBegin();
        }
    }
    
    //=========================================================================
    // ITURNBASED INTERFACE IMPLEMENTATION
    //=========================================================================
    
    public void ResetTurn()
    {
        HasActedThisTurn = false;
        needsDecision = false;
        // DON'T clear pendingAction here - it might be from OnActionReceived
        if (pendingAction != null)
        {
            Debug.Log($"[PlayerAgent] ⚠️ WARNING: ResetTurn called but pendingAction exists: {pendingAction.GetType().Name} - keeping it for next GetAction");
            // pendingAction = null; // COMMENTED OUT
        }
        Debug.Log("[PlayerAgent] Turn reset - HasActedThisTurn = false, needsDecision = false");
    }
    
    public IGameAction GetAction()
    {
        Debug.Log($"[PlayerAgent] ⚡ GetAction called - UseMLAgent: {UseMLAgent}, playerController: {(playerController != null ? "OK" : "NULL")}");
        Debug.Log($"[PlayerAgent] Is registered with TurnManager: {(TurnManager.Instance != null && TurnManager.Instance.GetTurnBasedObjects().Contains(this))}");
        
        if (!UseMLAgent || playerController == null)
        {
            // Return null action if ML-Agent is disabled
            Debug.Log("[PlayerAgent] GetAction returning NULL - ML-Agent disabled or no PlayerController");
            return null;
        }
        
        Debug.Log($"[PlayerAgent] GetAction - HasActedThisTurn: {HasActedThisTurn}, PendingAction: {(pendingAction != null ? pendingAction.GetType().Name : "NULL")}, needsDecision: {needsDecision}");
        
        // If we already acted this turn, return null
        if (HasActedThisTurn)
        {
            Debug.Log("[PlayerAgent] Already acted this turn, returning null");
            return null;
        }
        
        // Check if we already have a pending action from ML decision
        if (pendingAction != null)
        {
            IGameAction action = pendingAction;
            pendingAction = null;
            HasActedThisTurn = true;
            // Debug action details
            if (action is MoveAction moveAction)
            {
                Debug.Log($"[PlayerAgent] ✅ RETURNING MoveAction with direction: {moveAction.Direction} → TurnManager will execute");
            }
            else
            {
                Debug.Log($"[PlayerAgent] ✅ RETURNING action: {action.GetType().Name} → TurnManager will execute");
            }
            return action;
        }
        
        // Only request decision once per turn - TurnManager controlled
        if (!needsDecision)
        {
            Debug.Log("[PlayerAgent] RequestDecision called - requesting fresh decision for this turn");
            needsDecision = true;
            
            // Manual decision request - bypassing DecisionRequester component
            RequestDecision();
            
            // Manual Academy step since AutomaticSteppingEnabled = false
            if (Unity.MLAgents.Academy.Instance != null)
            {
                Unity.MLAgents.Academy.Instance.EnvironmentStep();
                Debug.Log("[PlayerAgent] Academy EnvironmentStep called");
            }
        }
        
        // Check ML-Agents connection and training status
        bool pythonConnected = IsMLAgentsConnected();
        bool trainingActive = IsTrainingActive();
        
        Debug.Log($"[PlayerAgent] Python connected: {pythonConnected}, Training active: {trainingActive}");
        
        // If no Python connection but training is active, use heuristic
        if (!pythonConnected && trainingActive)
        {
            Debug.Log("[PlayerAgent] No Python connection - using heuristic mode");
            // In turn-based mode with manual Academy stepping, 
            // Heuristic should be called immediately via RequestDecision
            // and action should be available in the same turn
            return null; // Action will be available next turn via OnActionReceived after manual step
        }
        
        // If training is not active, agent stays still
        if (!trainingActive)
        {
            Debug.Log("[PlayerAgent] Training not active - agent will stay still");
            HasActedThisTurn = true; // Mark as acted to prevent multiple calls this turn
            return null; // Agent stays still when training disabled
        }
        
        // Return null for this turn, action will be available next turn via OnActionReceived  
        return null;
    }
    
    /// <summary>
    /// Check if ML-Agents Python server is connected
    /// </summary>
    private bool IsMLAgentsConnected()
    {
        // Check if Academy is connected to trainer
        return Unity.MLAgents.Academy.Instance.IsCommunicatorOn;
    }
    
    /// <summary>
    /// Check if training is actively enabled via MLAgentsTrainingController
    /// </summary>
    private bool IsTrainingActive()
    {
        var trainingController = MLAgentsTrainingController.Instance;
        return trainingController != null && trainingController.IsTraining;
    }
    
    /// <summary>
    /// Public property that derives from MLAgentsTrainingController.IsTraining
    /// This replaces the old useMLAgent checkbox as the master training control
    /// </summary>
    public bool UseMLAgent
    {
        get
        {
            var trainingController = MLAgentsTrainingController.Instance;
            bool isTraining = trainingController != null && trainingController.IsTraining;
            bool heuristicMode = trainingController != null && trainingController.HeuristicMode;
            
            bool useML = isTraining || heuristicMode;
            
            // Debug only when state changes
            if (Time.frameCount % 60 == 0) // Every 60 frames (~1 second)
            {
                Debug.Log($"[PlayerAgent] UseMLAgent status - IsTraining: {isTraining}, HeuristicMode: {heuristicMode}, Result: {useML}");
            }
            
            return useML;
        }
    }
    
    private void OnDestroy()
    {
        // Unregister from TurnManager when destroyed - centralized
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterMLAgent(this);
        }
        
        // Unsubscribe from events
        if (LevelLoader.instance != null)
        {
            LevelLoader.instance.OnEnemyListChanged -= InvalidateEnemyCache;
            LevelLoader.instance.OnCollectibleListChanged -= InvalidateCollectibleCache;
        }
    }
}