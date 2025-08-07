using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public float turnInterval = 0.2f;
    private float turnTimer = 0f;
    public int TurnCount { get; private set; } = 0;
    private List<ITurnBased> turnBasedObjects = new List<ITurnBased>();

    private bool isProcessingActions = false;
    private int activeAnimations = 0;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (isProcessingActions) return;
        turnTimer += Time.deltaTime;
        if (turnTimer >= turnInterval)
        {
            turnTimer -= turnInterval;
            StartCoroutine(ProcessTurn());
        }
    }
     // --- ANİMASYON KONTROL METODLARI ---
    public void ReportAnimationStart() => activeAnimations++;
    public void ReportAnimationEnd() => activeAnimations--;
    // ------------------------------------

    private string GetDebugSymbol(TileType type)
    {
        switch (type)
        {
            case TileType.Player: return "P";
            case TileType.Enemy: return "E";
            case TileType.EnemyShooter: return "F";
            case TileType.Wall: return "#";
            case TileType.Breakable: return "B";
            case TileType.Bomb: return "O";
            case TileType.Explosion: return "X";
            case TileType.Projectile: return "*";
            case TileType.Coin: return "$";
            case TileType.Health: return "H";
            case TileType.Stairs: return "S";
            case TileType.Empty: return ".";
            default: return "?";
        }
    }

    private void PrintDebugMap()
    {
        var ll = LevelLoader.instance;
        if (ll == null) return;

        
        // Create a grid to represent the map
        string[,] debugGrid = new string[ll.width, ll.height];
        
        // Initialize with empty spaces
        for (int y = 0; y < ll.height; y++)
            for (int x = 0; x < ll.width; x++)
                debugGrid[x, y] = GetDebugSymbol(TileSymbols.DataSymbolToType(ll.levelMap[x, y]));
        
        // Add objects from tileObjects
        for (int y = 0; y < ll.height; y++)
        {
            for (int x = 0; x < ll.width; x++)
            {
                var obj = ll.tileObjects[x, y];
                if (obj != null)
                {
                    var tileType = TileSymbols.DataSymbolToType(ll.levelMap[x, y]);
                    debugGrid[x, y] = GetDebugSymbol(tileType);
                }
            }
        }
        
        // Build the entire map as a single string
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"\n=== TURN {TurnCount} DEBUG MAP ===");
        
        // Add each row to the string builder
        for (int y = 0; y < ll.height; y++)
        {
            string row = "";
            for (int x = 0; x < ll.width; x++)
            {
                row += debugGrid[x, y] + " ";
            }
            sb.AppendLine(row);
        }
        sb.AppendLine("======================");
        
        // Log the entire map at once
        Debug.Log(sb.ToString());
    }

    private IEnumerator ProcessTurn()
    {
        isProcessingActions = true;
        TurnCount++;
        
        // Print debug map at the start of each turn
        PrintDebugMap();

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
                    // Remove the object if it causes an error during reset
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
                // Continue with other units even if one fails
            }
        }

        // --- AŞAMA 2: EYLEMLERİ SIRAYLA UYGULA ---
        while (actionQueue.Count > 0)
        {
            IGameAction currentAction = actionQueue.Dequeue();
            if (currentAction.Actor == null)
            {
                // Eğer sahip yok edilmişse, bu eylemi atla ve bir sonrakine geç.
                continue;
            }
            try
            {
                currentAction.Execute();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error executing action {currentAction}: {e.Message}");
                // Continue with the next action even if one fails
            }

            // Her eylem arasında, animasyonların bitmesini bekle.
            yield return new WaitUntil(() => activeAnimations == 0);
        }

        isProcessingActions = false;
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
        if (unit is PlayerController) return 0;
        if (unit is EnemyShooterTile || unit is EnemyTile) return 1;
        if (unit is ExplosionWave) return 2; // YENİ SIRA
        if (unit is Projectile) return 3;
        if (unit is BombTile) return 4;
        return 100;
    }
    
}