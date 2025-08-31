using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Debug = UnityEngine.Debug;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }
    
    // ML-Agent Integration
    private PlayerAgent mlAgent;
    private MLAgentsTrainingController trainingController;
    private LevelSequencer levelSequencer;

    public float turnInterval = 0.2f;
    [Header("Animasyonlar için ek süre")]
    public float animationInterval = 0.1f; // turnInterval * 0.5f başlangıçta

    private float turnTimer = 0f;
    public bool debugnow = true;
    public int TurnCount { get; private set; } = 0;
    private List<ITurnBased> turnBasedObjects = new List<ITurnBased>();

    // Animasyon bekleme ve işlemde olma bayrakları kaldırıldı
    // private bool isProcessingActions = false;
    private int activeAnimations = 0;

    // --- TUR HIZI TAKİBİ ---
    private float prevBatchTime = 0f;
    private int prevBatchTurnCount = 0;
    private float prevTurnStartTime = 0f; // Her tur için
    private List<float> turnDurations = new List<float>(); // Her turun süresini tutacak liste

    private void OnEnable()
    {
        PlayerController.OnPlayerDeath += HandlePlayerDeathEvent;
    }

    private void OnDisable()
    {
        PlayerController.OnPlayerDeath -= HandlePlayerDeathEvent;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    void Start()
    {
        // Find ML-Agent components for central control - delayed search since player is instantiated later
        trainingController = MLAgentsTrainingController.Instance;
        levelSequencer = LevelSequencer.Instance;
        
        // If LevelSequencer doesn't exist, create it
        if (levelSequencer == null)
        {
            GameObject levelSequencerGO = new GameObject("LevelSequencer");
            levelSequencer = levelSequencerGO.AddComponent<LevelSequencer>();
            
            // Also add components for new system
            levelSequencerGO.AddComponent<HoudiniLevelParser>();
            levelSequencerGO.AddComponent<LevelImporter>();
            
            Debug.Log("[TurnManager] Created LevelSequencer, HoudiniLevelParser and LevelImporter GameObjects automatically");
        }
        
        // Player death handling is now done directly via PlayerAgent
        
        // Start coroutine to find PlayerAgent after level loads
        StartCoroutine(DelayedPlayerAgentSearch());
        
        Debug.Log($"[TurnManager] Training controller: {trainingController != null}, Level sequencer: {levelSequencer != null}");
    }
    
    /// <summary>
    /// Search for PlayerAgent after level loading is complete
    /// </summary>
    private System.Collections.IEnumerator DelayedPlayerAgentSearch()
    {
        int attempts = 0;
        int maxAttempts = 20; // 20 attempts over 4 seconds
        
        while (mlAgent == null && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.2f);
            attempts++;
            
            mlAgent = FindObjectOfType<PlayerAgent>();
            Debug.Log($"[TurnManager] PlayerAgent search attempt {attempts}: {mlAgent?.name ?? "NULL"}");
        }
        
        if (mlAgent != null)
        {
            Debug.Log($"[TurnManager] PlayerAgent found after {attempts} attempts: {mlAgent.name}");
            
            // Now that we found the PlayerAgent, check if it needs to be registered
            if (IsMLAgentActive && !turnBasedObjects.Contains(mlAgent))
            {
                RegisterMLAgent(mlAgent);
                Debug.Log("[TurnManager] PlayerAgent auto-registered after delayed discovery");
            }
        }
        else
        {
            Debug.LogError($"[TurnManager] PlayerAgent not found after {maxAttempts} attempts!");
        }
    }
    
    void OnDestroy()
    {
        // No event unsubscription needed - direct handling via PlayerAgent
    }

    void Update()
    {
        turnTimer += Time.deltaTime;
        if (turnTimer >= turnInterval)
        {
            turnTimer -= turnInterval;
            StartCoroutine(ProcessTurn());
        }

        // PrintDebugMap removed - already called in ProcessTurn()
    }

     // --- ANİMASYON KONTROL METODLARI ---
    public void ReportAnimationStart() => activeAnimations++;
    public void ReportAnimationEnd() => activeAnimations--;
    // ------------------------------------

    private string GetDebugSymbol(TileType type)
    {
        // Use TileSymbols.TypeToDataSymbol for consistency
        return TileSymbols.TypeToDataSymbol(type).ToString();
    }
    
    private TileType GetTileTypeFromGameObject(GameObject obj)
    {
        if (obj == null) return TileType.Empty;
        
        // First, try to get TileType from TileBase component (most reliable)
        var tileBase = obj.GetComponent<TileBase>();
        if (tileBase != null)
        {
            return tileBase.TileType;
        }
        
        // Fallback: Check GameObject name
        string name = obj.name.ToLower();
        
        if (name.Contains("player")) return TileType.Player;
        // Check EnemyShooter BEFORE Enemy to prevent false matches
        if (name.Contains("shooter")) return TileType.EnemyShooter;
        if (name.Contains("enemy")) return TileType.Enemy;
        if (name.Contains("bomb")) return TileType.Bomb;
        if (name.Contains("coin")) return TileType.Coin;
        if (name.Contains("health")) return TileType.Health;
        if (name.Contains("breakable")) return TileType.Breakable;
        if (name.Contains("wall")) return TileType.Wall;
        if (name.Contains("gate")) return TileType.Gate;

        // Fallback: use tag if available (with safety check)
        try 
        {
            if (obj.CompareTag("Player")) return TileType.Player;
            if (obj.tag == "Enemy") return TileType.Enemy;
        }
        catch (System.Exception)
        {
            // Tag doesn't exist, ignore
        }
        
        return TileType.Empty; // Unknown object
    }
    
    private bool IsDynamicObject(TileType type)
    {
        // Only these objects should override the levelMap
        return type == TileType.Player || 
               type == TileType.Enemy || 
               type == TileType.EnemyShooter ||
               type == TileType.Bomb ||
               type == TileType.Explosion ||
               type == TileType.Projectile;
    }
    
    private void PrintDebugMap()
    {
        var ll = LevelLoader.instance;
        if (ll == null || ll.levelMap == null) return;

        
        // Create a grid to represent the map
        string[,] debugGrid = new string[ll.Width, ll.Height];
        
        // Initialize with empty spaces
        for (int y = 0; y < ll.Height; y++)
            for (int x = 0; x < ll.Width; x++)
                debugGrid[x, y] = GetDebugSymbol(TileSymbols.DataSymbolToType(ll.levelMap[x, y]));
        
        // Add dynamic objects from tileObjects (only for moving/dynamic objects)
        for (int y = 0; y < ll.Height; y++)
        {
            for (int x = 0; x < ll.Width; x++)
            {
                var obj = ll.tileObjects[x, y];
                if (obj != null)
                {
                    // Only override for dynamic objects (Player, Enemy, Bomb)
                    TileType objectType = GetTileTypeFromGameObject(obj);
                    if (IsDynamicObject(objectType))
                    {
                        debugGrid[x, y] = GetDebugSymbol(objectType);
                    }
                }
            }
        }
        
        // Debug: Print a few levelMap samples (with bounds check)
        string sample00 = (ll.Width > 0 && ll.Height > 0) ? ll.levelMap[0,0].ToString() : "N/A";
        string sample55 = (ll.Width > 5 && ll.Height > 5) ? ll.levelMap[5,5].ToString() : "N/A";
        string sample1010 = (ll.Width > 10 && ll.Height > 10) ? ll.levelMap[10,10].ToString() : "N/A";
        Debug.Log($"[DEBUG] levelMap samples - [0,0]='{sample00}' [5,5]='{sample55}' [10,10]='{sample1010}'");
        
        // Build the entire map as a single string
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"\n=== TURN {TurnCount} DEBUG MAP ===");
        
        // Add each row to the string builder
        for (int y = 0; y < ll.Height; y++)
        {
            string row = "";
            for (int x = 0; x < ll.Width; x++)
            {
                row += debugGrid[x, y];
            }
            sb.AppendLine(row);
        }
        sb.AppendLine("======================");
        
        // Log the entire map at once
        Debug.Log(sb.ToString());
    }

    private IEnumerator ProcessTurn()
    {
        if (debugnow){
            // Her tur başlangıcında geçen süreyi hesapla ve logla
            float now = Time.realtimeSinceStartup;
            if (prevTurnStartTime > 0f)
            {
                float elapsed = now - prevTurnStartTime;
                turnDurations.Add(elapsed); // Her tur süresini listeye ekle
            }
            prevTurnStartTime = now;

            TurnCount++;
        }
        else
        {
            TurnCount++;
        }

        // Reset turns for all valid objects and clean up invalid ones
        var objectsToRemove = new List<ITurnBased>();
        foreach (var obj in turnBasedObjects.ToList())
        {
            if (obj == null || (obj as MonoBehaviour) == null || (obj as MonoBehaviour).gameObject == null)
            {
                Debug.LogWarning($"[TurnManager] Found null or destroyed object in turnBasedObjects, removing: {obj?.GetType().Name ?? "NULL"}");
                objectsToRemove.Add(obj);
                continue;
            }
            
            try
            {
                obj.ResetTurn();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TurnManager] Error resetting turn for {obj}: {e.Message}");
                objectsToRemove.Add(obj);
            }
        }
        
        // Remove invalid objects from the list
        foreach (var obj in objectsToRemove)
        {
            turnBasedObjects.Remove(obj);
        }

        // --- AŞAMA 1.5: ML-Agent Step ---
        if (IsMLAgentActive)
        {
            var academy = Unity.MLAgents.Academy.Instance;
            if (academy != null)
            {
                academy.EnvironmentStep();
                Debug.Log("[TurnManager] Academy stepped BEFORE executing actions");
            }
        }
        // --- AŞAMA 2: NİYETLERİ TOPLA ---
        Queue<IGameAction> actionQueue = new Queue<IGameAction>();
        var unitsToPlay = turnBasedObjects
            .Where(u => u != null && (u as MonoBehaviour) != null)
            .OrderBy(u => GetExecutionOrder(u))
            .ToList();

        // First, collect all actions without executing them
        Debug.Log($"[TurnManager] Processing {unitsToPlay.Count} turn-based objects this turn");
        foreach (var unit in unitsToPlay)
        {
            try
            {
                Debug.Log($"[TurnManager] Getting action from {unit.GetType().Name} (execution order: {GetExecutionOrder(unit)})");
                IGameAction action = unit.GetAction();
                if (action != null)
                {
                    Debug.Log($"[TurnManager] {unit.GetType().Name} provided action: {action.GetType().Name}");
                    actionQueue.Enqueue(action);
                }
                else
                {
                    Debug.Log($"[TurnManager] {unit.GetType().Name} returned NULL action");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error getting action from {unit}: {e.Message}");
            }
        }

        // Then execute all actions without waiting between them
        Debug.Log($"[TurnManager] Executing {actionQueue.Count} actions");
        while (actionQueue.Count > 0)
        {
            IGameAction currentAction = actionQueue.Dequeue();
            if (currentAction.Actor == null)
            {
                Debug.LogWarning($"[TurnManager] Skipping action {currentAction.GetType().Name} - Actor is null");
                continue;
            }
            
            // Check if Actor GameObject is destroyed (Unity-specific check)
            if (ReferenceEquals(currentAction.Actor, null) || currentAction.Actor == null)
            {
                Debug.LogWarning($"[TurnManager] Skipping action {currentAction.GetType().Name} - Actor GameObject destroyed");
                continue;
            }
            
            try
            {
                currentAction.Execute();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error executing action {currentAction}: {e.Message}");
            }
        }

        // Debug map after all actions are executed
        if (debugnow) PrintDebugMap();

        // Wait for all animations to complete
        while (activeAnimations > 0)
        {
            yield return null;
        }
        
        
        // Wait for the turn interval before starting the next turn
        yield return new WaitForSeconds(turnInterval);
    }

    public void Register(ITurnBased obj)
    {
        if (!turnBasedObjects.Contains(obj))
        {
            turnBasedObjects.Add(obj);
            string objName = (obj as MonoBehaviour)?.gameObject?.name ?? "Unknown";
            Debug.Log($"[TurnManager] Registered {obj.GetType().Name} ({objName}) - Total: {turnBasedObjects.Count}");
        }
        else
        {
            string objName = (obj as MonoBehaviour)?.gameObject?.name ?? "Unknown";
            Debug.LogWarning($"[TurnManager] Attempted to register duplicate {obj.GetType().Name} ({objName})");
        }
    }

    public void Unregister(ITurnBased obj)
    {
        if (turnBasedObjects.Contains(obj))
        {
            turnBasedObjects.Remove(obj);
            string objName = (obj as MonoBehaviour)?.gameObject?.name ?? "Unknown";
            Debug.Log($"[TurnManager] Unregistered {obj.GetType().Name} ({objName}) - Total: {turnBasedObjects.Count}");
        }
    }
    
    /// <summary>
    /// Clear all registered turn-based objects except ML-Agent and Player
    /// Used when loading new levels to clean up old objects
    /// </summary>
    public void ClearAllRegistersExceptPlayer()
    {
        Debug.Log($"[TurnManager] ClearAllRegistersExceptPlayer - Before: {turnBasedObjects.Count} objects");
        
        // Log all objects before clearing
        for (int i = 0; i < turnBasedObjects.Count; i++)
        {
            var obj = turnBasedObjects[i];
            if (obj != null)
            {
                Debug.Log($"[TurnManager] Object {i}: {obj.GetType().Name} (GameObject: {(obj as MonoBehaviour)?.gameObject?.name ?? "NULL"})");
            }
            else
            {
                Debug.Log($"[TurnManager] Object {i}: NULL");
            }
        }
        
        // Sadece Player ve ML-Agent'ı koru, diğerlerini temizle
        int removedCount = turnBasedObjects.RemoveAll(obj => 
        {
            if (obj == null || (obj as MonoBehaviour) == null || (obj as MonoBehaviour).gameObject == null)
            {
                Debug.Log("[TurnManager] Removing null/destroyed object from turnBasedObjects");
                return true; // Null objeleri temizle
            }
            
            // Player ve PlayerAgent'ı koru
            if (obj is PlayerController || obj is PlayerAgent)
            {
                Debug.Log($"[TurnManager] Preserving {obj.GetType().Name}");
                return false; // Koru
            }
            
            // Diğer tüm objeleri temizle (Enemy, Bomb, vb.)
            Debug.Log($"[TurnManager] Removing {obj.GetType().Name} ({(obj as MonoBehaviour).gameObject?.name}) from turnBasedObjects");
            return true; // Temizle
        });
        
        Debug.Log($"[TurnManager] ClearAllRegistersExceptPlayer - Removed {removedCount} objects, After: {turnBasedObjects.Count} objects");
    }
    
    // Sıralamayı belirleyen merkezi kural motoru.
    private int GetExecutionOrder(ITurnBased unit)
    {
        // ML-Agent gets priority when active
        if (IsMLAgentActive && unit is PlayerAgent) return -1;
        if (unit is PlayerController) return 0;
        if (unit is EnemyShooterTile || unit is EnemyTile) return 1;
        if (unit is MovingExplosion) return 2; // Moving explosions execute before static explosions
        if (unit is ExplosionTile) return 3; // New explosion system
        if (unit is Projectile) return 4;
        if (unit is BombTile) return 5;
        return 100;
    }
    
    // ML-Agent Central Control Properties and Methods
    public bool IsMLAgentActive
    {
        get
        {
            bool tcExists = trainingController != null;
            bool tcTraining = tcExists && trainingController.IsTraining;
            bool agentExists = mlAgent != null;
            bool agentUseML = agentExists && mlAgent.UseMLAgent;
            
            bool isActive = tcExists && tcTraining && agentExists && agentUseML;
            
            
            return isActive;
        }
    }
    
    /// <summary>
    /// Handle player death event - triggered by PlayerController.OnPlayerDied
    /// This maintains loose coupling through event system
    /// </summary>
    public void HandlePlayerDeathEvent(PlayerController deadPlayer)
    {
        Debug.Log($"[TurnManager] Player {deadPlayer.name} died - handling death sequence");
        
        if (IsMLAgentActive)
        {
            // ML-Agent training mode: Load next level and continue
            LoadNextLevelInTrainingSequence();
            
            // End current episode - new level will start fresh episode
            mlAgent.ForceEndEpisode();
        }
        else
        {
            // Manual gameplay mode: Handle differently (game over, restart, etc.)
            HandleManualGameplayDeath(deadPlayer);
        }
    }
    
    /// <summary>
    /// Handle player death in ML-Agent training mode
    /// Loads next level in sequence and continues training
    /// </summary>
    private void LoadNextLevelInTrainingSequence()
    {
        Debug.Log($"[TurnManager] LoadNextLevelInTrainingSequence - LevelSequencer: {levelSequencer != null}, IsSequenceActive: {levelSequencer?.IsSequenceActive()}");
        
        if (levelSequencer != null && levelSequencer.IsSequenceActive())
        {
            Debug.Log("[TurnManager] Loading next level in training sequence...");
            levelSequencer.LoadNextLevel();
        }
        else
        {
            Debug.LogWarning($"[TurnManager] LevelSequencer not found or sequence disabled. LevelSequencer null: {levelSequencer == null}, Available levels: {levelSequencer?.AvailableLevels.Count}");
        }
    }
    
    /// <summary>
    /// Handle player death in manual gameplay mode
    /// Can be extended for game over screens, lives system, etc.
    /// </summary>
    private void HandleManualGameplayDeath(PlayerController deadPlayer)
    {
        Debug.Log($"[TurnManager] Manual gameplay - player {deadPlayer.name} died");
        
        // TODO: Implement manual gameplay death handling
        // - Game over screen
        // - Lives system
        // - Level restart
        // - Menu return
        
        // For now, just restart current level
        if (levelSequencer != null && levelSequencer.IsSequenceActive())
        {
            Debug.Log("[TurnManager] Restarting current level for manual gameplay");
            // Could add a delay or UI confirmation here
            StartCoroutine(RestartCurrentLevelDelayed());
        }
    }
    
    /// <summary>
    /// Restart current level with a small delay
    /// </summary>
    private System.Collections.IEnumerator RestartCurrentLevelDelayed()
    {
        yield return new WaitForSeconds(1f); // Give time to see death
        
        if (levelSequencer != null)
        {
            // Reload same level
            levelSequencer.RestartCurrentLevel();
        }
    }
    
    /// <summary>
    /// Register ML-Agent when training starts
    /// This ensures proper turn-based integration
    /// </summary>
    public void RegisterMLAgent(PlayerAgent agent)
    {
        if (!turnBasedObjects.Contains(agent))
        {
            mlAgent = agent;
            Register(agent);
            Debug.Log($"[TurnManager] ML-Agent registered for turn-based control - Total objects: {turnBasedObjects.Count}");
        }
        else
        {
            Debug.LogWarning("[TurnManager] ML-Agent already registered!");
        }
    }
    
    /// <summary>
    /// Get current turn-based objects for debugging
    /// </summary>
    public System.Collections.Generic.List<ITurnBased> GetTurnBasedObjects()
    {
        return new System.Collections.Generic.List<ITurnBased>(turnBasedObjects);
    }
    
    /// <summary>
    /// Debug method to log all currently registered objects
    /// </summary>
    public void LogAllRegisteredObjects()
    {
        Debug.Log($"[TurnManager] Currently registered objects: {turnBasedObjects.Count}");
        for (int i = 0; i < turnBasedObjects.Count; i++)
        {
            var obj = turnBasedObjects[i];
            if (obj != null && (obj as MonoBehaviour) != null)
            {
                string gameObjName = (obj as MonoBehaviour).gameObject?.name ?? "NULL";
                string position = "";
                if (obj is IMovable movable)
                {
                    position = $" at ({movable.X}, {movable.Y})";
                }
                Debug.Log($"[TurnManager] {i}: {obj.GetType().Name} ({gameObjName}){position}");
            }
            else
            {
                Debug.Log($"[TurnManager] {i}: NULL or destroyed object");
            }
        }
    }
    
    /// <summary>
    /// Unregister ML-Agent when training stops
    /// </summary>
    public void UnregisterMLAgent(PlayerAgent agent)
    {
        if (turnBasedObjects.Contains(agent))
        {
            Unregister(agent);
            Debug.Log("[TurnManager] ML-Agent unregistered from turn-based control");
        }
    }
}