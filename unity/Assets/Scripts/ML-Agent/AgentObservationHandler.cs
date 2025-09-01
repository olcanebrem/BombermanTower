using UnityEngine;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class AgentObservationHandler
{
    private PlayerController playerController;
    private EnvManager envManager;
    private int observationRadius;
    private bool useDistanceObservations;
    private bool useGridObservations;
    private bool debugObservations;

    // Cached values for observations
    private float cachedMapSize = 15f;
    private int cachedInitialEnemyCount = 1;
    private int cachedInitialCollectibleCount = 1;
    private bool levelDataCached = false;

    // Action mapping arrays
    private readonly Vector2Int[] moveDirections = new Vector2Int[]
    {
        Vector2Int.zero, new Vector2Int(0, -1), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(-1, 0),
        new Vector2Int(1, -1), new Vector2Int(1, 1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    public AgentObservationHandler(PlayerController player, EnvManager env, int radius, bool useDist, bool useGrid, bool debug)
    {
        playerController = player;
        envManager = env;
        observationRadius = radius;
        useDistanceObservations = useDist;
        useGridObservations = useGrid;
        debugObservations = debug;
    }

    public void CacheLevelData()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.GetCurrentLevelData() != null)
        {
            var levelData = LevelManager.Instance.GetCurrentLevelData();
            cachedMapSize = Mathf.Max(levelData.width, levelData.height);
            cachedInitialEnemyCount = Mathf.Max(1, levelData.enemyPositions.Count);
            cachedInitialCollectibleCount = Mathf.Max(1, levelData.collectiblePositions.Count);
            levelDataCached = true;
        }
        else
        {
            cachedMapSize = 15f;
            cachedInitialEnemyCount = 1;
            cachedInitialCollectibleCount = 1;
            levelDataCached = false;
        }
    }

    public void CollectObservations(VectorSensor sensor)
    {
        int observationCount = 0;

        // Player State Observations
        var ll = LevelLoader.instance;
        if (ll != null) { sensor.AddObservation((float)playerController.X / ll.Width); sensor.AddObservation((float)playerController.Y / ll.Height); }
        else { sensor.AddObservation(0f); sensor.AddObservation(0f); }
        observationCount += 2;

        float healthRatio = playerController.MaxHealth > 0 ? (float)playerController.CurrentHealth / playerController.MaxHealth : 0f;
        sensor.AddObservation(healthRatio);
        observationCount += 1;

        Vector2Int lastMove = GetLastMoveDirection();
        sensor.AddObservation((float)lastMove.x);
        sensor.AddObservation((float)lastMove.y);
        observationCount += 2;

        // Game State Observations
        if (envManager != null)
        {
            float enemyRatio = (float)envManager.GetRemainingEnemyCount() / cachedInitialEnemyCount;
            sensor.AddObservation(enemyRatio);
            float collectibleRatio = (float)envManager.GetRemainingCollectibleCount() / cachedInitialCollectibleCount;
            sensor.AddObservation(collectibleRatio);
            observationCount += 2;
        }
        else { sensor.AddObservation(0f); sensor.AddObservation(0f); observationCount += 2; }

        // Grid-based Observations
        if (useGridObservations) observationCount += CollectGridObservations(sensor);

        // Distance-based Observations
        if (useDistanceObservations) observationCount += CollectDistanceObservations(sensor);

        if (debugObservations) Debug.Log($"[PlayerAgent] Collected {observationCount} observations");
    }

    private int CollectGridObservations(VectorSensor sensor)
    {
        // ... (existing implementation)
    }

    private int CollectDistanceObservations(VectorSensor sensor)
    {
        // ... (existing implementation)
    }

    private float GetTileTypeValue(TileType tileType)
    {
        // ... (existing implementation)
    }
    
    private Vector2Int GetLastMoveDirection()
    {
        // Access last move direction from PlayerController using reflection if needed
        // Or if PlayerController exposes it publicly.
        var field = typeof(PlayerController).GetField("lastMoveDirection", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) return (Vector2Int)field.GetValue(playerController);
        return Vector2Int.zero;
    }
}