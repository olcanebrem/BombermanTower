using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using Debug = UnityEngine.Debug;

/// <summary>
/// ML-Agent'in ana koordinasyon sınıfı. 
/// Gözlem, aksiyon, ödül ve bölüm yönetimi gibi sorumlulukları ilgili yardımcı sınıflara delege eder.
/// Bu sınıf, Agent yaşam döngüsünü ve ITurnBased arayüzünü yönetir.
/// </summary>
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

    // --- Helper Class References (Sorumlulukların Delege Edildiği Sınıflar) ---
    private AgentObservationHandler observationHandler;
    private AgentActionHandler actionHandler;
    private AgentRewardHandler rewardHandler;
    private AgentEpisodeManager episodeManager;

    // --- ITurnBased & State Tracking ---
    public bool HasActedThisTurn { get; set; }
    private IGameAction pendingAction;
    private bool needsDecision = false;
    private int episodeSteps = 0; // Sadece loglama ve step sayımı için burada tutulabilir.

    // --- Debug Panel Properties ---
    public int CurrentMoveIndex { get; private set; } = 0;
    public int CurrentBombIndex { get; private set; } = 0;
    public Vector2Int LastActionDirection { get; private set; } = Vector2Int.zero;
    public string CurrentActionType { get; private set; } = "None";


    #region LIFECYCLE

    private void OnEnable()
    {
        PlayerController.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        PlayerController.OnPlayerDeath -= HandlePlayerDeath;
    }

    /// <summary>
    /// Bileşenleri bulur, yardımcı sınıfları başlatır ve TurnManager'a kaydolur.
    /// </summary>
    public override void Initialize()
    {
        Debug.Log($"[PlayerAgent] Initialize() called.");

        // Academy ayarları (sıra tabanlı oyun için)
        var academy = Academy.Instance;
        if (academy != null) academy.AutomaticSteppingEnabled = false;

        // Davranış ismini ayarla
        if (!string.IsNullOrEmpty(behaviorName))
            GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorName = behaviorName;

        // Gerekli bileşenleri bul
        playerController = GetComponent<PlayerController>();
        envManager = FindObjectOfType<EnvManager>();
        rewardSystem = GetComponent<RewardSystem>(); // Önce bu objede ara
        if (rewardSystem == null) StartCoroutine(DelayedRewardSystemSearch());
        
        // Gerekli bileşenlerin kontrolü
        if (playerController == null)
        {
            Debug.LogError("[PlayerAgent] PlayerController component required!");
            return;
        }

        // --- Yardımcı Sınıfları Başlat (Dependency Injection) ---
        observationHandler = new AgentObservationHandler(playerController, envManager, observationRadius, useDistanceObservations, useGridObservations, debugObservations);
        actionHandler = new AgentActionHandler(playerController, debugActions);
        rewardHandler = new AgentRewardHandler(playerController, rewardSystem, envManager); // RewardSystem'i doğrudan ver
        episodeManager = new AgentEpisodeManager(this, playerController, envManager, rewardSystem, debugActions);

        // TurnManager'a kaydol
        if (UseMLAgent)
        {
            TurnManager.Instance?.Unregister(playerController);
            TurnManager.Instance?.RegisterMLAgent(this);
            Debug.Log("[PlayerAgent] ML-Agent registered with TurnManager.");
        }
    }
    
    /// <summary>
    /// Yeni bir bölüm başladığında durumu sıfırlar.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        episodeSteps = 0;
        
        // İşi EpisodeManager ve ObservationHandler'a delege et
        episodeManager.OnEpisodeBegin();
        observationHandler.CacheLevelData();
        
        // Olay abonelikleri
        if (LevelLoader.instance != null)
        {
            LevelLoader.instance.OnEnemyListChanged -= observationHandler.InvalidateCache;
            LevelLoader.instance.OnEnemyListChanged += observationHandler.InvalidateCache;
        }
    }
    
    /// <summary>
    /// Gözlemleri toplamak için ObservationHandler'ı çağırır.
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerController == null) return;
        observationHandler.CollectObservations(sensor);
    }
    
    /// <summary>
    /// Python'dan bir aksiyon geldiğinde tetiklenir.
    /// </summary>
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!UseMLAgent || playerController == null) return;

        Debug.Log($"[PlayerAgent] 🐍 OnActionReceived! Step: {episodeSteps + 1}");
        
        episodeSteps++;
        needsDecision = false;

        var discreteActions = actions.DiscreteActions;
        CurrentMoveIndex = discreteActions[0];
        CurrentBombIndex = discreteActions.Length > 1 ? discreteActions[1] : 0;

        // 1. Aksiyon oluştur (ActionHandler)
        pendingAction = actionHandler.CreateGameAction(CurrentMoveIndex, CurrentBombIndex);

        // 2. Adım ödüllerini uygula (RewardHandler)
        rewardHandler.ApplyStepRewards();
        
        // 3. Bölümün bitip bitmediğini kontrol et (EpisodeManager)
        episodeManager.CheckEpisodeTermination(episodeSteps);

        // Debug info güncelle
        CurrentActionType = pendingAction?.GetType().Name ?? "None";
        if (pendingAction is MoveAction ma) LastActionDirection = ma.Direction;
    }

    /// <summary>
    /// Heuristic (manuel kontrol) modunda aksiyonları belirler.
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (discreteActionsOut.Length == 0) return;

        int moveAction = 0;
        int bombAction = 0;

        if (enableManualInput)
        {
            if (Input.GetKey(KeyCode.W)) moveAction = 1;
            else if (Input.GetKey(KeyCode.D)) moveAction = 2;
            else if (Input.GetKey(KeyCode.S)) moveAction = 3;
            else if (Input.GetKey(KeyCode.A)) moveAction = 4;
            bombAction = Input.GetKey(KeyCode.Space) ? 1 : 0;
        }
        else if (useRandomHeuristic)
        {
            int randomChoice = Random.Range(0, 10);
            if (randomChoice < 8) // %80 hareket
                moveAction = Random.Range(1, 5);
            
            bombAction = (Random.Range(0, 10) < 1) ? 1 : 0; // %10 bomba
        }
        
        discreteActionsOut[0] = moveAction;
        if (discreteActionsOut.Length > 1) discreteActionsOut[1] = bombAction;
    }
    
    #endregion

    #region ITURNBASED

    /// <summary>
    /// TurnManager tarafından çağrılır. Sıra tabanlı sistem için aksiyon talep eder.
    /// </summary>
    public IGameAction GetAction()
    {
        if (!UseMLAgent || playerController == null || HasActedThisTurn)
        {
            return null;
        }

        // Eğer OnActionReceived'dan bir aksiyon gelmişse, onu TurnManager'a ver
        if (pendingAction != null)
        {
            IGameAction actionToExecute = pendingAction;
            pendingAction = null; // Aksiyonu verdikten sonra temizle
            HasActedThisTurn = true;
            Debug.Log($"[PlayerAgent] ✅ Returning Action: {actionToExecute.GetType().Name}");
            return actionToExecute;
        }

        // Eğer bu turda henüz karar istenmediyse, Python'dan yeni bir karar iste
        if (!needsDecision)
        {
            needsDecision = true;
            Debug.Log("[PlayerAgent] Requesting decision from Python...");
            RequestDecision();
            
            // Manuel adımlama modunda olduğumuz için Academy'i manuel tetikle
            Academy.Instance.EnvironmentStep();
        }
        
        // Bu frame'de null dönecek. Karar OnActionReceived'a geldiğinde pendingAction'a atanacak 
        // ve bir sonraki GetAction çağrısında TurnManager'a verilecek.
        return null;
    }

    public void ResetTurn()
    {
        HasActedThisTurn = false;
        needsDecision = false;
    }
    
    #endregion

    #region EVENT HANDLERS

    private void HandlePlayerDeath(PlayerController controller)
    {
        Debug.Log("[PlayerAgent] Player death event received. Ending episode.");
        EndEpisode(); // ML-Agents'e bölümün bittiğini bildir
        // Yeniden başlatma mantığı EpisodeManager'a devredilebilir veya TurnManager tarafından yönetilebilir.
    }
    
    // RewardSystem'i bulmak için gecikmeli arama
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
            // Bulunan RewardSystem referanslarını yardımcı sınıflara güncelle
            rewardHandler.SetRewardSystem(rewardSystem);
            episodeManager.SetRewardSystem(rewardSystem);
            Debug.Log($"[PlayerAgent] RewardSystem found after {attempts} attempts.");
        }
        else
        {
            Debug.LogError("[PlayerAgent] RewardSystem could not be found!");
        }
    }

    /// <summary>
    /// MLAgentsTrainingController'a göre ajanın aktif olup olmadığını belirler.
    /// </summary>
    public bool UseMLAgent => MLAgentsTrainingController.Instance != null && 
                              (MLAgentsTrainingController.Instance.IsTraining || MLAgentsTrainingController.Instance.HeuristicMode);

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterMLAgent(this);
        }
        if (LevelLoader.instance != null)
        {
            LevelLoader.instance.OnEnemyListChanged -= observationHandler.InvalidateCache;
        }
    }

    #endregion
}