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
    public bool debugnow = false;
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


    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    void Start()
    {
        // Find ML-Agent components for central control
        mlAgent = FindObjectOfType<PlayerAgent>();
        trainingController = MLAgentsTrainingController.Instance;
        levelSequencer = LevelSequencer.Instance;
        
        // Subscribe to player death event
        PlayerController.OnPlayerDied += HandlePlayerDeathEvent;
        
        Debug.Log($"[TurnManager] ML-Agent found: {mlAgent != null}, Training controller: {trainingController != null}, Level sequencer: {levelSequencer != null}");
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        PlayerController.OnPlayerDied -= HandlePlayerDeathEvent;
    }

    void Update()
    {
        turnTimer += Time.deltaTime;
        if (turnTimer >= turnInterval)
        {
            turnTimer -= turnInterval;
            StartCoroutine(ProcessTurn());
        }

        if (debugnow) PrintDebugMap();
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
        
        // Check GameObject name or tag
        string name = obj.name.ToLower();
        
        if (name.Contains("player")) return TileType.Player;
        if (name.Contains("enemy")) return TileType.Enemy;
        if (name.Contains("shooter")) return TileType.EnemyShooter;
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
        if (ll == null) return;

        
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
        
        // Debug: Print a few levelMap samples
        Debug.Log($"[DEBUG] levelMap samples - [0,0]='{ll.levelMap[0,0]}' [5,5]='{ll.levelMap[5,5]}' [10,10]='{ll.levelMap[10,10]}'");
        
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
            PrintDebugMap();
        }
        else
        {
            TurnCount++;
        }

        // Reset turns for all valid objects
        foreach (var obj in turnBasedObjects.ToList())
        {
            if (obj != null && (obj as MonoBehaviour) != null)
            {
                try
                {
                    obj.ResetTurn();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error resetting turn for {obj}: {e.Message}");
                    turnBasedObjects.Remove(obj);
                }
            }
        }

        // --- AŞAMA 1: NİYETLERİ TOPLA ---
        Queue<IGameAction> actionQueue = new Queue<IGameAction>();
        var unitsToPlay = turnBasedObjects
            .Where(u => u != null && (u as MonoBehaviour) != null)
            .OrderBy(u => GetExecutionOrder(u))
            .ToList();

        // First, collect all actions without executing them
        foreach (var unit in unitsToPlay)
        {
            try
            {
                IGameAction action = unit.GetAction();
                if (action != null)
                {
                    actionQueue.Enqueue(action);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error getting action from {unit}: {e.Message}");
            }
        }

        // Then execute all actions without waiting between them
        while (actionQueue.Count > 0)
        {
            IGameAction currentAction = actionQueue.Dequeue();
            if (currentAction.Actor == null)
            {
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
        if (!turnBasedObjects.Contains(obj)) turnBasedObjects.Add(obj);
    }

    public void Unregister(ITurnBased obj)
    {
        if (turnBasedObjects.Contains(obj)) turnBasedObjects.Remove(obj);
    }
    
    // Sıralamayı belirleyen merkezi kural motoru.
    private int GetExecutionOrder(ITurnBased unit)
    {
        // ML-Agent gets priority when active
        if (IsMLAgentActive && unit is PlayerAgent) return -1;
        if (unit is PlayerController) return 0;
        if (unit is EnemyShooterTile || unit is EnemyTile) return 1;
        if (unit is ExplosionWave) return 2; // YENİ SIRA
        if (unit is Projectile) return 3;
        if (unit is BombTile) return 4;
        return 100;
    }
    
    // ML-Agent Central Control Properties and Methods
    public bool IsMLAgentActive
    {
        get
        {
            return trainingController != null && 
                   trainingController.IsTraining && 
                   mlAgent != null && 
                   mlAgent.UseMLAgent;
        }
    }
    
    /// <summary>
    /// Handle player death event - triggered by PlayerController.OnPlayerDied
    /// This maintains loose coupling through event system
    /// </summary>
    private void HandlePlayerDeathEvent(PlayerController deadPlayer)
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
        if (levelSequencer != null && levelSequencer.IsSequenceActive())
        {
            Debug.Log("[TurnManager] Loading next level in training sequence...");
            levelSequencer.LoadNextLevel();
        }
        else
        {
            Debug.LogWarning("[TurnManager] LevelSequencer not found or sequence disabled");
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
            Debug.Log("[TurnManager] ML-Agent registered for turn-based control");
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