using UnityEngine;
using Unity.MLAgents.Sensors;
using System.Collections.Generic; // Gerekirse
using Debug = UnityEngine.Debug;

public class AgentObservationHandler
{
    private readonly PlayerController playerController;
    private readonly EnvManager envManager;
    private readonly int observationRadius;
    private readonly bool useDistanceObservations;
    private readonly bool useGridObservations;
    private readonly bool debugObservations;

    private float cachedMapSize = 15f;
    private int cachedInitialEnemyCount = 1;
    private int cachedInitialCollectibleCount = 1;

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
        var levelData = LevelManager.Instance?.GetCurrentLevelData();
        if (levelData != null)
        {
            cachedMapSize = Mathf.Max(levelData.width, levelData.height);
            cachedInitialEnemyCount = Mathf.Max(1, levelData.enemyPositions.Count);
            cachedInitialCollectibleCount = Mathf.Max(1, levelData.collectiblePositions.Count);
        }
        else
        {
            // Fallback values
            cachedMapSize = 15f;
            cachedInitialEnemyCount = 1;
            cachedInitialCollectibleCount = 1;
        }
    }

    public void CollectObservations(VectorSensor sensor)
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
        
        // Player velocity/last move direction (simplified - always zero for now)
        Vector2Int lastMove = Vector2Int.zero;
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
    
    public void InvalidateCache()
    {
        if(debugObservations) Debug.Log("[AgentObservationHandler] Cache invalidation triggered.");
    }
    
    private int CollectGridObservations(VectorSensor sensor)
    {
        int observationCount = 0;
        var layeredGrid = LayeredGridService.Instance;
        var ll = LevelLoader.instance;
        
        if (layeredGrid == null || ll == null) return observationCount;
        
        // Grid observations around player position
        for (int dy = -observationRadius; dy <= observationRadius; dy++)
        {
            for (int dx = -observationRadius; dx <= observationRadius; dx++)
            {
                Vector2Int checkPos = new Vector2Int(playerController.X + dx, playerController.Y + dy);
                
                // Bounds check
                if (!layeredGrid.IsValidPosition(checkPos.x, checkPos.y))
                {
                    sensor.AddObservation(GetTileTypeValue(TileType.Wall)); // Out of bounds = wall
                    observationCount += 1;
                    continue;
                }
                
                // Get tile type at position
                TileType tileType = layeredGrid.GetTileTypeAt(checkPos.x, checkPos.y);
                sensor.AddObservation(GetTileTypeValue(tileType));
                observationCount += 1;
            }
        }
        
        return observationCount;
    }
    
    private int CollectDistanceObservations(VectorSensor sensor)
    {
        int observationCount = 0;
        var layeredGrid = LayeredGridService.Instance;
        
        if (layeredGrid == null) return observationCount;
        
        // Distance to nearest enemy (simplified)
        float nearestEnemyDistance = 10f; // Max distance
        sensor.AddObservation(nearestEnemyDistance);
        observationCount += 1;
        
        // Distance to nearest collectible item (simplified)
        float nearestItemDistance = 10f; // Max distance  
        sensor.AddObservation(nearestItemDistance);
        observationCount += 1;
        
        return observationCount;
    }
    
    private float GetTileTypeValue(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Empty: return 0f;
            case TileType.Wall: return 1f;
            case TileType.Breakable: return 2f;
            case TileType.Player: return 3f;
            case TileType.Enemy: return 4f;
            case TileType.EnemyShooter: return 5f;
            case TileType.Bomb: return 6f;
            case TileType.Explosion: return 7f;
            case TileType.Coin: return 8f;
            case TileType.Health: return 9f;
            case TileType.Gate: return 10f;
            case TileType.Projectile: return 11f;
            default: return 0f;
        }
    }
}