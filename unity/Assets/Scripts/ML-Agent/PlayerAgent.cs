using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using Debug = UnityEngine.Debug;

public class PlayerAgent : Agent, ITurnBased
{
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

    // --- Component References ---
    private PlayerController playerController;
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

    #region UNITY & ML-AGENTS LIFECYCLE

    private void OnEnable() 
    {
        Debug.Log($"[PlayerAgent] OnEnable() called on {gameObject.name}");
        PlayerController.OnPlayerDeath += HandlePlayerDeath;
    }
    
    private void OnDisable() 
    {
        Debug.Log($"[PlayerAgent] OnDisable() called on {gameObject.name}");
        PlayerController.OnPlayerDeath -= HandlePlayerDeath;
    }
    
    private void Awake()
    {
        Debug.Log($"[PlayerAgent] Awake() called on {gameObject.name}");
    }
    
    private void Start()
    {
        Debug.Log($"[PlayerAgent] Start() called on {gameObject.name}");
        // Ensure Initialize is called if not already
        if (playerController == null)
        {
            Debug.Log($"[PlayerAgent] playerController is null in Start(), calling Initialize()");
            Initialize();
        }
    }

    public override void Initialize()
    {
        Debug.Log($"[PlayerAgent] Initialize() called.");

        var academy = Academy.Instance;
        if (academy != null) academy.AutomaticSteppingEnabled = false;

        if (!string.IsNullOrEmpty(behaviorName))
            GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorName = behaviorName;

        playerController = GetComponent<PlayerController>();
        envManager = FindObjectOfType<EnvManager>();
        
        Debug.Log($"[PlayerAgent] Initialize - Found PlayerController: {playerController != null} on GameObject: {gameObject.name}");
        
        if (playerController == null)
        {
            Debug.LogError($"[PlayerAgent] PlayerController component required on {gameObject.name}!");
            // Try to find PlayerController components in scene
            var allPlayerControllers = FindObjectsOfType<PlayerController>();
            Debug.LogError($"[PlayerAgent] Found {allPlayerControllers.Length} PlayerController(s) in scene:");
            foreach(var pc in allPlayerControllers)
            {
                Debug.LogError($"  - PlayerController on: {pc.gameObject.name}");
            }
            return;
        }

        rewardSystem = FindObjectOfType<RewardSystem>();
        
        observationHandler = new AgentObservationHandler(playerController, envManager, observationRadius, useDistanceObservations, useGridObservations, debugObservations);
        actionHandler = new AgentActionHandler(playerController, debugActions);
        rewardHandler = new AgentRewardHandler(playerController, rewardSystem, envManager, debugActions);
        episodeManager = new AgentEpisodeManager(this, playerController, envManager, rewardSystem, debugActions);

        if (rewardSystem == null)
        {
            StartCoroutine(DelayedRewardSystemSearch());
        }

        Debug.Log($"[PlayerAgent] Initialize complete - UseMLAgent: {UseMLAgent}, TurnManager.Instance: {TurnManager.Instance != null}");
        
        if (UseMLAgent)
        {
            Debug.Log($"[PlayerAgent] Unregistering PlayerController: {playerController?.gameObject.name}");
            TurnManager.Instance?.Unregister(playerController);
            
            Debug.Log($"[PlayerAgent] Registering PlayerAgent: {gameObject.name}");
            TurnManager.Instance?.RegisterMLAgent(this);
            Debug.Log("[PlayerAgent] ML-Agent registered with TurnManager (mlAgent reference updated automatically)");
        }
        else
        {
            Debug.Log("[PlayerAgent] UseMLAgent is false - PlayerAgent will not be used");
        }
    }
    
    public override void OnEpisodeBegin()
    {
        episodeSteps = 0;
        
        episodeManager.OnEpisodeBegin();
        observationHandler.CacheLevelData();
        
        if (LevelLoader.instance != null)
        {
            LevelLoader.instance.OnEnemyListChanged -= observationHandler.InvalidateCache;
            LevelLoader.instance.OnEnemyListChanged += observationHandler.InvalidateCache;
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerController == null) return;
        observationHandler.CollectObservations(sensor);
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!UseMLAgent || playerController == null) return;

        // Increment epsilon-greedy step counter
        if (EpsilonGreedyController.Instance != null)
        {
            EpsilonGreedyController.Instance.IncrementStep();
        }

        // Check if this is from Python (communicator) or heuristic
        bool fromPython = Academy.Instance.IsCommunicatorOn;
        string source = fromPython ? "🐍 Python" : "🧠 Heuristic";
        var receivedActions = actions.DiscreteActions;
        string actionStr = receivedActions.Length >= 2 ? $"Move:{receivedActions[0]}, Bomb:{receivedActions[1]}" : "No Actions";
        
        // Add epsilon info to debug
        string epsilonInfo = "";
        if (EpsilonGreedyController.Instance != null)
        {
            float epsilon = EpsilonGreedyController.Instance.CurrentEpsilon;
            int steps = EpsilonGreedyController.Instance.TotalSteps;
            epsilonInfo = $" (ε={epsilon:F3}, Step:{steps})";
        }
        
        Debug.Log($"[PlayerAgent] {source} OnActionReceived! Step: {episodeSteps + 1} - Actions: [{actionStr}]{epsilonInfo}");
        
        episodeSteps++;
        needsDecision = false;

        var discreteActions = actions.DiscreteActions;
        CurrentMoveIndex = discreteActions[0];
        CurrentBombIndex = discreteActions.Length > 1 ? discreteActions[1] : 0;
        
        pendingAction = actionHandler.CreateGameAction(CurrentMoveIndex, CurrentBombIndex);
        rewardHandler.ApplyStepRewards();
        episodeManager.CheckEpisodeTermination(episodeSteps);

        CurrentActionType = pendingAction?.GetType().Name ?? "None";
        if (pendingAction is MoveAction ma) LastActionDirection = ma.Direction;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (discreteActionsOut.Length == 0) 
        {
            Debug.LogWarning("[PlayerAgent] Heuristic: discreteActionsOut.Length is 0!");
            return;
        }

        int moveAction = 0;
        int bombAction = 0;

        if (enableManualInput)
        {
            if (Input.GetKey(KeyCode.W)) moveAction = 1;
            else if (Input.GetKey(KeyCode.D)) moveAction = 2;
            else if (Input.GetKey(KeyCode.S)) moveAction = 3;
            else if (Input.GetKey(KeyCode.A)) moveAction = 4;
            bombAction = Input.GetKey(KeyCode.Space) ? 1 : 0;
            Debug.Log($"[🎮 HEURISTIC] Manual: Move={moveAction}, Bomb={bombAction}");
        }
        else if (useRandomHeuristic)
        {
            int randomChoice = Random.Range(0, 10);
            if (randomChoice < 8)
                moveAction = Random.Range(1, 5);
            
            bombAction = (Random.Range(0, 10) < 1) ? 1 : 0;
            Debug.Log($"[🎲 HEURISTIC] Random: Move={moveAction}, Bomb={bombAction} (choice={randomChoice})");
        }
        else
        {
            Debug.Log("[❌ HEURISTIC] Neither manual nor random enabled!");
        }
        
        discreteActionsOut[0] = moveAction;
        if (discreteActionsOut.Length > 1) discreteActionsOut[1] = bombAction;
        
        Debug.Log($"[✅ HEURISTIC] Final Output: [{moveAction}, {bombAction}]");
    }
    
    #endregion

    #region ITURNBASED IMPLEMENTATION

    public IGameAction GetAction()
    {
        Debug.Log($"[PlayerAgent] GetAction called - UseMLAgent:{UseMLAgent}, playerController:{playerController != null}, HasActedThisTurn:{HasActedThisTurn}");
        
        if (!UseMLAgent || playerController == null || HasActedThisTurn)
        {
            Debug.Log("[PlayerAgent] GetAction early return - conditions not met");
            return null;
        }
        
        if (pendingAction != null)
        {
            IGameAction actionToExecute = pendingAction;
            pendingAction = null; 
            HasActedThisTurn = true;
            Debug.Log($"[PlayerAgent] ✅ Returning Action: {actionToExecute.GetType().Name}");
            return actionToExecute;
        }
        
        if (!needsDecision)
        {
            needsDecision = true;
            Debug.Log("[PlayerAgent] Requesting decision from Python...");
            RequestDecision();
            Academy.Instance.EnvironmentStep();
            
            // Epsilon-greedy decision: should we use heuristic instead of waiting for Python?
            bool shouldUseHeuristic = false;
            
            // Check epsilon-greedy controller
            if (EpsilonGreedyController.Instance != null && Academy.Instance.IsCommunicatorOn)
            {
                shouldUseHeuristic = EpsilonGreedyController.Instance.ShouldUseHeuristic();
                Debug.Log($"[🎯 EPSILON] Decision: {(shouldUseHeuristic ? "🎲 Using Heuristic" : "🐍 Waiting for Python")}");
            }
            // Fallback to original logic if no communicator or no epsilon controller
            else if (Academy.Instance.IsCommunicatorOn == false || useRandomHeuristic)
            {
                shouldUseHeuristic = true;
                Debug.Log("[PlayerAgent] Fallback: Using heuristic (no communicator or random enabled)");
            }
            
            if (shouldUseHeuristic)
            {
                Debug.Log($"[🎯 HEURISTIC_CALL] About to call Heuristic() - useRandom:{useRandomHeuristic}, enableManual:{enableManualInput}");
                
                // Generate heuristic action immediately
                var discreteActionsFloat = new float[2]; // move + bomb actions
                var heuristicBuffers = ActionBuffers.FromDiscreteActions(discreteActionsFloat);
                
                Debug.Log($"[📥 BEFORE] Heuristic input: [{discreteActionsFloat[0]}, {discreteActionsFloat[1]}]");
                Heuristic(in heuristicBuffers);
                
                // Get the actual modified values from the buffer
                var actualActions = heuristicBuffers.DiscreteActions;
                Debug.Log($"[📤 AFTER] Heuristic output: [{actualActions[0]}, {actualActions[1]}]");
                Debug.Log($"[🔄 TRANSFER] Heuristic → OnActionReceived: [{actualActions[0]}, {actualActions[1]}]");
                
                // Process the heuristic action with updated values
                OnActionReceived(heuristicBuffers);
                
                // Return the action if we have one now
                if (pendingAction != null)
                {
                    IGameAction actionToExecute = pendingAction;
                    pendingAction = null; 
                    HasActedThisTurn = true;
                    Debug.Log($"[PlayerAgent] ✅ Returning Heuristic Action: {actionToExecute.GetType().Name}");
                    return actionToExecute;
                }
            }
        }
        
        return null;
    }

    public void ResetTurn()
    {
        HasActedThisTurn = false;
        needsDecision = false;
    }
    
    #endregion
    
    #region HELPER & EVENT HANDLERS
    
    private void HandlePlayerDeath(PlayerController controller)
    {
        Debug.Log("[PlayerAgent] Player death event received. Ending episode.");
        EndEpisode();
    }
    
    IEnumerator DelayedRewardSystemSearch()
    {
        int attempts = 0;
        while (rewardSystem == null && attempts < 5)
        {
            yield return new WaitForSeconds(0.2f);
            rewardSystem = FindObjectOfType<RewardSystem>();
            attempts++;
        }

        if (rewardSystem != null)
        {
            rewardHandler.SetRewardSystem(rewardSystem);
            episodeManager.SetRewardSystem(rewardSystem);
            Debug.Log($"[PlayerAgent] RewardSystem found after {attempts} attempts.");
        }
        else
        {
            Debug.LogError("[PlayerAgent] RewardSystem could not be found!");
        }
    }

    public bool UseMLAgent 
    { 
        get 
        {
            bool hasController = MLAgentsTrainingController.Instance != null;
            bool isTraining = hasController && MLAgentsTrainingController.Instance.IsTraining;
            bool heuristicMode = hasController && MLAgentsTrainingController.Instance.HeuristicMode;
            bool result = hasController && (isTraining || heuristicMode);
            
            return result;
        }
    }

    private void OnDestroy()
    {
        Debug.Log($"[PlayerAgent] OnDestroy() called on {gameObject.name} - This should NOT happen during normal gameplay!");
        Debug.Log($"[PlayerAgent] OnDestroy stack trace:");
        Debug.Log(System.Environment.StackTrace);
        
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterMLAgent(this);
        }
        if (LevelLoader.instance != null && observationHandler != null)
        {
            LevelLoader.instance.OnEnemyListChanged -= observationHandler.InvalidateCache;
        }
    }

    #endregion
}