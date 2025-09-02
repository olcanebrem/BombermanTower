using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using Debug = UnityEngine.Debug;

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
            Debug.Log("[PlayerAgentManager] Singleton instance created");
        }
        else if (Instance != this)
        {
            Debug.Log("[PlayerAgentManager] Duplicate instance destroyed");
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
        
        // Register with TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
            Debug.Log("[PlayerAgentManager] Registered with TurnManager");
        }
    }
    
    private void OnEnable() 
    {
        Debug.Log($"[PlayerAgentManager] OnEnable() called");
        PlayerController.OnPlayerDeath += HandlePlayerDeath;
    }
    
    private void OnDisable() 
    {
        Debug.Log($"[PlayerAgentManager] OnDisable() called");
        PlayerController.OnPlayerDeath -= HandlePlayerDeath;
    }

    #endregion

    #region PLAYER REGISTRATION

    /// <summary>
    /// Called when a new PlayerController is created during level transitions
    /// </summary>
    public void RegisterPlayer(PlayerController playerController)
    {
        if (currentPlayerController != null)
        {
            Debug.Log($"[PlayerAgentManager] Switching from old player '{currentPlayerController.name}' to new player '{playerController.name}'");
        }
        
        currentPlayerController = playerController;
        
        // Check if the new player has PlayerAgent component (optional)
        currentPlayerAgent = playerController.GetComponent<PlayerAgent>();
        
        // If player has PlayerAgent component, disable it since we're managing ML-Agent functionality centrally
        if (currentPlayerAgent != null)
        {
            Debug.Log($"[PlayerAgentManager] Found PlayerAgent component on player - disabling it for centralized control");
            currentPlayerAgent.enabled = false;
        }
        
        Debug.Log($"[PlayerAgentManager] Registered new player: {playerController.name} (PlayerAgent disabled: {currentPlayerAgent != null})");
        
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
        Debug.Log($"[PlayerAgentManager] Unregistering current player: {currentPlayerController?.name ?? "null"}");
        currentPlayerController = null;
        currentPlayerAgent = null;
    }

    #endregion

    #region INITIALIZATION

    private void InitializeComponents()
    {
        // Call base Agent Initialize
        base.Initialize();
        
        Debug.Log($"[PlayerAgentManager] Components initialized");
    }
    
    private void InitializeHelperClasses()
    {
        if (currentPlayerController == null) return;
        
        // Initialize helper classes (these would need to be updated to work with singleton pattern)
        // observationHandler = new AgentObservationHandler(this);
        // actionHandler = new AgentActionHandler(this);
        // rewardHandler = new AgentRewardHandler(this);
        // episodeManager = new AgentEpisodeManager(this);
        
        Debug.Log($"[PlayerAgentManager] Helper classes initialized for player: {currentPlayerController.name}");
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
        Debug.Log($"[PlayerAgentManager] GetAction called - Current Player: {currentPlayerController?.name ?? "null"}, HasActed: {HasActedThisTurn}");
        
        // If no current player or already acted, return null
        if (currentPlayerController == null || HasActedThisTurn)
        {
            Debug.Log("[PlayerAgentManager] GetAction early return - no player or already acted");
            return null;
        }
        
        // Check if we have a pending action from ML-Agent system
        if (pendingAction != null)
        {
            IGameAction actionToExecute = pendingAction;
            pendingAction = null; 
            HasActedThisTurn = true;
            Debug.Log($"[PlayerAgentManager] ✅ Returning pending ML-Agent Action: {actionToExecute.GetType().Name}");
            return actionToExecute;
        }
        
        // If no pending ML-Agent action, request decision
        if (needsDecision)
        {
            needsDecision = false;
            Debug.Log("[PlayerAgentManager] Requesting ML-Agent decision...");
            RequestDecision();
            Academy.Instance.EnvironmentStep();
            
            // Epsilon-greedy decision: should we use heuristic?
            bool shouldUseHeuristic = false;
            
            // Check epsilon-greedy controller
            if (EpsilonGreedyController.Instance != null && Academy.Instance.IsCommunicatorOn)
            {
                shouldUseHeuristic = EpsilonGreedyController.Instance.ShouldUseHeuristic();
                Debug.Log($"[🎯 EPSILON] Decision: {(shouldUseHeuristic ? "🎲 Using Heuristic" : "🐍 Waiting for Python")}");
            }
            // Fallback to heuristic if no communicator
            else if (!Academy.Instance.IsCommunicatorOn || useRandomHeuristic)
            {
                shouldUseHeuristic = true;
                Debug.Log("[PlayerAgentManager] Fallback: Using heuristic (no communicator or random enabled)");
            }
            
            if (shouldUseHeuristic)
            {
                Debug.Log($"[🎯 HEURISTIC_CALL] About to call Heuristic() - useRandom:{useRandomHeuristic}, enableManual:{enableManualInput}");
                
                // Generate heuristic action immediately
                var discreteActionsFloat = new float[2]; // move + bomb actions
                var heuristicBuffers = ActionBuffers.FromDiscreteActions(discreteActionsFloat);
                
                Heuristic(in heuristicBuffers);
                
                // Get the actual modified values from the buffer and process them
                var actualActions = heuristicBuffers.DiscreteActions;
                Debug.Log($"[📤 HEURISTIC] Generated actions: [{actualActions[0]}, {actualActions[1]}]");
                
                // Process the heuristic action immediately
                OnActionReceived(heuristicBuffers);
                
                // Return the action that was just created
                if (pendingAction != null)
                {
                    IGameAction actionToExecute = pendingAction;
                    pendingAction = null;
                    HasActedThisTurn = true;
                    Debug.Log($"[PlayerAgentManager] ✅ Returning heuristic Action: {actionToExecute.GetType().Name}");
                    return actionToExecute;
                }
            }
        }
        
        // Fall back to PlayerController for manual input
        Debug.Log("[PlayerAgentManager] No ML-Agent action available, falling back to PlayerController");
        var playerAction = currentPlayerController.GetAction();
        if (playerAction != null)
        {
            HasActedThisTurn = true;
            episodeSteps++;
        }
        return playerAction;
    }

    public void ExecuteTurn()
    {
        if (HasActedThisTurn || currentPlayerController == null) return;

        Debug.Log($"[PlayerAgentManager] ExecuteTurn - Episode Steps: {episodeSteps}");
        
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
        Debug.Log($"[PlayerAgentManager] OnActionReceived called");
        
        // Handle action directly for the current player
        if (currentPlayerController == null)
        {
            Debug.LogWarning("[PlayerAgentManager] OnActionReceived: No current player!");
            return;
        }

        // Increment episode steps
        episodeSteps++;
        needsDecision = false;

        // Check if this is from Python (communicator) or heuristic
        bool fromPython = Academy.Instance.IsCommunicatorOn;
        string source = fromPython ? "🐍 Python" : "🧠 Heuristic";
        
        var discreteActions = actionBuffers.DiscreteActions;
        string actionStr = discreteActions.Length >= 2 ? $"Move:{discreteActions[0]}, Bomb:{discreteActions[1]}" : "No Actions";
        
        Debug.Log($"[PlayerAgentManager] {source} OnActionReceived! Step: {episodeSteps} - Actions: [{actionStr}]");
        
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
            
            Debug.Log($"[PlayerAgentManager] Created action: {CurrentActionType}");
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
            Debug.Log($"[PlayerAgentManager] Creating PlaceBombAction with direction: {bombDirection}");
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
                Debug.Log($"[PlayerAgentManager] Creating MoveAction: {moveDirection}");
                return new MoveAction(currentPlayerController, moveDirection);
            }
        }
        
        // No action
        Debug.Log($"[PlayerAgentManager] No valid action created (Move:{moveIndex}, Bomb:{bombIndex})");
        return null;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log($"[PlayerAgentManager] CollectObservations called");
        
        // Collect observations for the current player
        if (currentPlayerController == null)
        {
            Debug.LogWarning("[PlayerAgentManager] CollectObservations: No current player!");
            return;
        }
        
        // Basic observation collection - this needs proper implementation
        if (observationHandler != null)
        {
            // observationHandler.CollectObservations(sensor);
            Debug.Log("[PlayerAgentManager] Using observation handler");
        }
        else
        {
            // Basic fallback observations
            sensor.AddObservation(currentPlayerController.X);
            sensor.AddObservation(currentPlayerController.Y);
            sensor.AddObservation(currentPlayerController.CurrentHealth);
            Debug.Log($"[PlayerAgentManager] Basic observations: Pos({currentPlayerController.X},{currentPlayerController.Y}), Health({currentPlayerController.CurrentHealth})");
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        Debug.Log($"[PlayerAgentManager] Heuristic called");
        
        // Provide heuristic behavior directly
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (discreteActionsOut.Length == 0) 
        {
            Debug.LogWarning("[PlayerAgentManager] Heuristic: discreteActionsOut.Length is 0!");
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
            Debug.Log($"[🎮 MANUAL_HEURISTIC] Move={moveAction}, Bomb={bombAction}");
        }
        else if (useRandomHeuristic)
        {
            int randomChoice = UnityEngine.Random.Range(0, 10);
            if (randomChoice < 8)
                moveAction = UnityEngine.Random.Range(1, 5);
            
            bombAction = (UnityEngine.Random.Range(0, 10) < 1) ? 1 : 0;
            Debug.Log($"[🎲 RANDOM_HEURISTIC] Move={moveAction}, Bomb={bombAction}");
        }
        else
        {
            Debug.Log("[❌ HEURISTIC] Neither manual nor random enabled - returning no action");
        }
        
        discreteActionsOut[0] = moveAction;
        if (discreteActionsOut.Length > 1) discreteActionsOut[1] = bombAction;
        
        Debug.Log($"[✅ HEURISTIC] Final Output: [{moveAction}, {bombAction}]");
    }

    public override void Initialize()
    {
        Debug.Log($"[PlayerAgentManager] Agent Initialize() called");
        
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
        
        Debug.Log($"[PlayerAgentManager] Agent initialized - Academy found: {academy != null}");
    }

    #endregion

    #region EPISODE MANAGEMENT

    public void ResetEpisode()
    {
        episodeSteps = 0;
        pendingAction = null;
        needsDecision = true;
        HasActedThisTurn = false;
        
        Debug.Log("[PlayerAgentManager] Episode reset");
    }

    public void EndEpisode()
    {
        if (currentPlayerAgent != null)
        {
            currentPlayerAgent.EndEpisode();
        }
        
        ResetEpisode();
        Debug.Log("[PlayerAgentManager] Episode ended");
    }

    private void HandlePlayerDeath(PlayerController deadPlayer)
    {
        if (deadPlayer == currentPlayerController)
        {
            Debug.Log("[PlayerAgentManager] Current player died - ending episode");
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
        
        Debug.Log($"[PlayerAgentManager] IsMLAgentActive: {shouldUseML} (Training: {isTrainingActive}, PlayerAgent: {hasActivePlayerAgent})");
        
        return shouldUseML;
    }

    #endregion
}