using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;

/// <summary>
/// Singleton Manager for Player ML-Agent functionality
/// Separates training/agent logic from PlayerController for clean level transitions
/// Can act as ML-Agent proxy when no PlayerAgent component is on player
/// </summary>
public class PlayerAgentManager : Agent, ITurnBased
{
    public static PlayerAgentManager Instance { get; private set; }
    
    [Header("Behavior Settings")]
    [Tooltip("Behavior name must match YAML config - Default: PlayerAgent")]
    public string behaviorName = "PlayerAgent";

    [Header("Observation Settings")]
    [Range(1, 5)] public int observationRadius = 2;
    public bool useDistanceObservations = true;
    public bool useGridObservations = true;

    [Header("Debug")]
    public bool debugActions = true;
    public bool debugObservations = false;

    [Header("Heuristic Control")]
    public bool useRandomHeuristic = true;
    public bool enableManualInput = false;

    // --- Component References (Updated per level) ---
    private PlayerController currentPlayerController;
    private PlayerAgent currentPlayerAgent; // If still using PlayerAgent component
    private RewardSystem rewardSystem;
    private EnvManager envManager;

    // --- Helper Class References ---
    private AgentObservationHandler observationHandler;
    private AgentActionHandler actionHandler;
    private AgentRewardHandler rewardHandler;
    private AgentEpisodeManager episodeManager;

    // --- ITurnBased & State Tracking ---
    public bool HasActedThisTurn { get; set; }
    private IGameAction pendingAction;
    private bool needsDecision = false;
    private int episodeSteps = 0;

    // --- Debug Panel Properties ---
    public int CurrentMoveIndex { get; private set; } = 0;
    public int CurrentBombIndex { get; private set; } = 0;
    public Vector2Int LastActionDirection { get; private set; } = Vector2Int.zero;
    public string CurrentActionType { get; private set; } = "None";

    #region SINGLETON LIFECYCLE

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeComponents();
    }
    
    private void Start()
    {
        // Find existing managers
        envManager = FindObjectOfType<EnvManager>();
        rewardSystem = FindObjectOfType<RewardSystem>();
        
        // Subscribe to events
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Subscribe<PlayerSpawned>(OnPlayerSpawned);
            GameEventBus.Instance.Subscribe<LevelCleanupStarted>(OnLevelCleanupStarted);
        }
        
        // Register with TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
    }
    
    private void OnEnable() 
    {
        PlayerController.OnPlayerDeath += HandlePlayerDeath;
    }
    
    private void OnDisable() 
    {
        PlayerController.OnPlayerDeath -= HandlePlayerDeath;
        
        // Unsubscribe from events
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
            GameEventBus.Instance.Unsubscribe<LevelCleanupStarted>(OnLevelCleanupStarted);
        }
    }

    #endregion

    #region PLAYER REGISTRATION

    /// <summary>
    /// Called when a new PlayerController is created during level transitions
    /// Uses service-based approach for cleaner player management
    /// </summary>
    public void RegisterPlayer(PlayerController playerController)
    {
        if (playerController == null)
        {
            Debug.LogWarning("[AGENT_REG] RegisterPlayer called with null playerController");
            return;
        }
        
        currentPlayerController = playerController;
        
        // Check if the new player has PlayerAgent component (optional)
        currentPlayerAgent = playerController.GetComponent<PlayerAgent>();
        
        // If player has PlayerAgent component, disable it since we're managing ML-Agent functionality centrally
        if (currentPlayerAgent != null)
        {
            currentPlayerAgent.enabled = false;
        }
        
        // Re-initialize helper classes with new player
        InitializeHelperClasses();
        
        // Reset episode state
        ResetEpisode();
    }
    
    /// <summary>
    /// Called when player is destroyed during level cleanup
    /// </summary>
    public void UnregisterPlayer()
    {
        currentPlayerController = null;
        currentPlayerAgent = null;
    }

    #endregion

    #region INITIALIZATION

    private void InitializeComponents()
    {
        // Call base Agent Initialize
        base.Initialize();
    }
    
    private void InitializeHelperClasses()
    {
        if (currentPlayerController == null) return;
        
        // Initialize helper classes (these would need to be updated to work with singleton pattern)
        // observationHandler = new AgentObservationHandler(this);
        // actionHandler = new AgentActionHandler(this);
        // rewardHandler = new AgentRewardHandler(this);
        // episodeManager = new AgentEpisodeManager(this);
    }

    #endregion

    #region ITURMBASED IMPLEMENTATION

    public void ResetTurn() 
    {
        HasActedThisTurn = false;
        needsDecision = true;
    }

    public IGameAction GetAction()
    {
        // Debug GetAction calls (limit to avoid spam)
        if (UnityEngine.Random.Range(0, 100) < 5) // 5% chance to log
        {
            Debug.Log($"[🎮 AGENT_ACTION] GetAction called - Player: {currentPlayerController?.name}, HasActed: {HasActedThisTurn}");
        }
        
        // If no current player or already acted, return null
        if (currentPlayerController == null || HasActedThisTurn)
        {
            if (currentPlayerController == null)
                Debug.LogWarning("[🎮 AGENT_ACTION] GetAction: currentPlayerController is null!");
            return null;
        }
        
        // Check if we have a pending action from ML-Agent system
        if (pendingAction != null)
        {
            IGameAction actionToExecute = pendingAction;
            pendingAction = null; 
            HasActedThisTurn = true;
            return actionToExecute;
        }
        
        // If no pending ML-Agent action, request decision
        if (needsDecision)
        {
            needsDecision = false;
            RequestDecision();
            Academy.Instance.EnvironmentStep();
            
            // Epsilon-greedy decision: should we use heuristic?
            bool shouldUseHeuristic = false;
            
            // Publish decision requested event
            GameEventBus.Instance?.Publish(new AgentDecisionRequested(currentPlayerController, false));
            
            // Check epsilon-greedy controller
            if (EpsilonGreedyController.Instance != null && Academy.Instance.IsCommunicatorOn)
            {
                shouldUseHeuristic = EpsilonGreedyController.Instance.ShouldUseHeuristic();
            }
            // Fallback to heuristic if no communicator
            else if (!Academy.Instance.IsCommunicatorOn || useRandomHeuristic)
            {
                shouldUseHeuristic = true;
            }
            
            if (shouldUseHeuristic)
            {
                // Generate heuristic action immediately
                var discreteActionsFloat = new float[2]; // move + bomb actions
                var heuristicBuffers = ActionBuffers.FromDiscreteActions(discreteActionsFloat);
                
                Heuristic(in heuristicBuffers);
                
                // Get the actual modified values from the buffer and process them
                var actualActions = heuristicBuffers.DiscreteActions;
                
                // Process the heuristic action immediately
                OnActionReceived(heuristicBuffers);
                
                // Return the action that was just created
                if (pendingAction != null)
                {
                    IGameAction actionToExecute = pendingAction;
                    pendingAction = null;
                    HasActedThisTurn = true;
                    return actionToExecute;
                }
            }
        }
        
        // Fall back to PlayerController for manual input
        var playerAction = currentPlayerController.GetAction();
        if (playerAction != null)
        {
            Debug.Log($"[🎮 AGENT_ACTION] Manual input action received: {playerAction.GetType().Name}");
            HasActedThisTurn = true;
            episodeSteps++;
        }
        else if (UnityEngine.Random.Range(0, 100) < 2) // 2% chance to log null actions
        {
            Debug.Log($"[🎮 AGENT_ACTION] No manual input from PlayerController");
        }
        return playerAction;
    }

    public void ExecuteTurn()
    {
        if (HasActedThisTurn || currentPlayerController == null) return;

        // This method might not be needed anymore since TurnManager uses GetAction
        // But keeping for compatibility
        if (needsDecision && currentPlayerAgent != null)
        {
            currentPlayerAgent.RequestDecision();
            needsDecision = false;
        }
    }

    #endregion

    #region ML-AGENT OVERRIDE METHODS

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Handle action directly for the current player
        if (currentPlayerController == null)
        {
            return;
        }

        // Increment episode steps
        episodeSteps++;
        needsDecision = false;

        var discreteActions = actionBuffers.DiscreteActions;
        
        if (discreteActions.Length >= 2)
        {
            int moveAction = discreteActions[0];
            int bombAction = discreteActions[1];
            
            CurrentMoveIndex = moveAction;
            CurrentBombIndex = bombAction;
            
            // Create game action using same logic as PlayerAgent
            pendingAction = CreateGameAction(moveAction, bombAction);
            
            CurrentActionType = pendingAction?.GetType().Name ?? "None";
            if (pendingAction is MoveAction ma) 
            {
                LastActionDirection = ma.Direction;
            }
            
            // Publish action received event
            GameEventBus.Instance?.Publish(new AgentActionReceived(
                currentPlayerController, moveAction, bombAction, pendingAction));
        }
    }

    /// <summary>
    /// Create game action from ML-Agent action indices (copied from PlayerAgent logic)
    /// </summary>
    private IGameAction CreateGameAction(int moveIndex, int bombIndex)
    {
        // Priority: Bomb action first if requested
        if (bombIndex > 0 && currentPlayerController != null)
        {
            // Use last action direction for bomb placement, default to down if no direction
            Vector2Int bombDirection = LastActionDirection != Vector2Int.zero ? LastActionDirection : Vector2Int.down;
            return new PlaceBombAction(currentPlayerController, bombDirection);
        }
        
        // Movement action
        if (moveIndex > 0 && currentPlayerController != null)
        {
            Vector2Int moveDirection = moveIndex switch
            {
                1 => Vector2Int.up,    // North
                2 => Vector2Int.right, // East  
                3 => Vector2Int.down,  // South
                4 => Vector2Int.left,  // West
                _ => Vector2Int.zero
            };
            
            if (moveDirection != Vector2Int.zero)
            {
                return new MoveAction(currentPlayerController, moveDirection);
            }
        }
        
        // No action
        return null;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Collect observations for the current player
        if (currentPlayerController == null)
        {
            return;
        }
        
        // Basic observation collection - this needs proper implementation
        if (observationHandler != null)
        {
            // observationHandler.CollectObservations(sensor);
        }
        else
        {
            // Basic fallback observations
            sensor.AddObservation(currentPlayerController.X);
            sensor.AddObservation(currentPlayerController.Y);
            sensor.AddObservation(currentPlayerController.CurrentHealth);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Provide heuristic behavior directly
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (discreteActionsOut.Length == 0) 
        {
            return;
        }

        int moveAction = 0;
        int bombAction = 0;

        if (enableManualInput)
        {
            if (Input.GetKey(KeyCode.W)) moveAction = 1;      // North
            else if (Input.GetKey(KeyCode.D)) moveAction = 2; // East
            else if (Input.GetKey(KeyCode.S)) moveAction = 3; // South
            else if (Input.GetKey(KeyCode.A)) moveAction = 4; // West
            bombAction = Input.GetKey(KeyCode.Space) ? 1 : 0;
        }
        else if (useRandomHeuristic)
        {
            int randomChoice = UnityEngine.Random.Range(0, 10);
            if (randomChoice < 8)
                moveAction = UnityEngine.Random.Range(1, 5);
            
            bombAction = (UnityEngine.Random.Range(0, 10) < 1) ? 1 : 0;
        }
        
        discreteActionsOut[0] = moveAction;
        if (discreteActionsOut.Length > 1) discreteActionsOut[1] = bombAction;
    }

    public override void Initialize()
    {
        // Initialize Academy settings
        var academy = Academy.Instance;
        if (academy != null) academy.AutomaticSteppingEnabled = false;

        if (!string.IsNullOrEmpty(behaviorName))
        {
            var behaviorParams = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (behaviorParams != null)
            {
                behaviorParams.BehaviorName = behaviorName;
            }
        }
    }

    #endregion

    #region EPISODE MANAGEMENT

    public void ResetEpisode()
    {
        episodeSteps = 0;
        pendingAction = null;
        needsDecision = true;
        HasActedThisTurn = false;
    }

    public void EndEpisode()
    {
        if (currentPlayerController != null)
        {
            // Publish episode ended event
            GameEventBus.Instance?.Publish(new AgentEpisodeEnded(
                currentPlayerController, "Manual", episodeSteps));
        }
        
        if (currentPlayerAgent != null)
        {
            currentPlayerAgent.EndEpisode();
        }
        
        ResetEpisode();
    }

    private void HandlePlayerDeath(PlayerController deadPlayer)
    {
        if (deadPlayer == currentPlayerController)
        {
            // Publish player destroyed event
            GameEventBus.Instance?.Publish(new PlayerDestroyed(deadPlayer, "Death"));
            EndEpisode();
        }
    }

    #endregion

    #region PUBLIC ACCESSORS

    public PlayerController GetCurrentPlayer() => currentPlayerController;
    public bool HasCurrentPlayer() => currentPlayerController != null;
    
    /// <summary>
    /// Returns whether ML-Agent should be controlling the player
    /// For manual control, return false; for ML training, return true based on training controller
    /// </summary>
    public bool IsMLAgentActive()
    {
        // Check if we have a training controller and it's actively training
        var trainingController = MLAgentsTrainingController.Instance;
        bool isTrainingActive = trainingController != null && trainingController.IsTraining;
        
        // Check if we have an active player with PlayerAgent component that wants ML control  
        bool hasActivePlayerAgent = currentPlayerAgent != null && currentPlayerAgent.enabled && currentPlayerAgent.UseMLAgent;
        
        bool shouldUseML = isTrainingActive || hasActivePlayerAgent;
        
        return shouldUseML;
    }

    #endregion
    
    #region EVENT HANDLERS
    
    /// <summary>
    /// Handle player spawned event from PlayerService via event bus
    /// </summary>
    private void OnPlayerSpawned(PlayerSpawned eventData)
    {
        if (eventData.Player != null)
        {
            RegisterPlayer(eventData.Player);
        }
    }
    
    /// <summary>
    /// Handle level cleanup started event
    /// </summary>
    private void OnLevelCleanupStarted(LevelCleanupStarted eventData)
    {
        UnregisterPlayer();
    }
    
    #endregion
}