using UnityEngine;
using Unity.MLAgents;
using System.Collections.Generic;
using System.Collections;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class EnvManager : MonoBehaviour
{
    [Header("ML-Agent Settings")]
    public int MaxStepsPerEpisode = 3000;
    
    [Header("Component References")]
    public LevelManager levelManager;
    public LevelLoader levelLoader;
    
    // Object tracking from LevelLoader
    private List<GameObject> bombs = new List<GameObject>();
    private List<Vector2Int> explosions = new List<Vector2Int>();
    
    private PlayerAgent playerAgent;
    
    private void Start()
    {
        playerAgent = FindObjectOfType<PlayerAgent>();
        
        // Get references if not assigned
        if (levelManager == null)
            levelManager = FindObjectOfType<LevelManager>();
        if (levelLoader == null)
            levelLoader = FindObjectOfType<LevelLoader>();
            
        if (levelManager == null || levelLoader == null)
        {
            Debug.LogError("[EnvManager] LevelManager or LevelLoader not found!");
        }
    }
    
    // Episode reset for ML-Agent
    public void ResetEnvironment()
    {
        // Clear tracking lists
        bombs.Clear();
        explosions.Clear();
        
        // Reset level through LevelManager
        if (levelManager != null)
        {
            levelManager.ResetLevel();
        }
        
        Debug.Log("[EnvManager] Environment reset completed");
    }
    
    // Coordinate conversion - delegate to LevelLoader
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        if (levelLoader != null)
            return levelLoader.WorldToGrid(worldPosition);
        
        // Fallback
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x + 0.5f),
            Mathf.FloorToInt(worldPosition.y + 0.5f)
        );
    }
    
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        if (levelLoader != null)
            return levelLoader.GridToWorld(gridPosition);
            
        // Fallback
        return new Vector3(gridPosition.x, gridPosition.y, 0);
    }
    
    // Player and environment queries - delegate to LevelLoader/LevelManager
    public Vector3 GetPlayerSpawnPosition()
    {
        if (levelLoader != null)
        {
            Vector2Int spawnPos = levelLoader.GetPlayerSpawnPosition();
            return GridToWorld(spawnPos);
        }
        return Vector3.zero;
    }
    
    public Vector2 GetExitPosition()
    {
        if (levelLoader != null)
        {
            GameObject exitObj = levelLoader.GetExitObject();
            if (exitObj != null)
                return exitObj.transform.position;
        }
        return Vector2.zero;
    }
    
    // Object detection methods
    public bool HasEnemyAt(Vector2Int gridPos)
    {
        if (levelLoader != null)
        {
            List<GameObject> enemies = levelLoader.GetEnemies();
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    Vector2Int enemyGridPos = WorldToGrid(enemy.transform.position);
                    if (enemyGridPos == gridPos) return true;
                }
            }
        }
        return false;
    }
    
    public bool HasCollectibleAt(Vector2Int gridPos)
    {
        if (levelLoader != null)
        {
            List<GameObject> collectibles = levelLoader.GetCollectibles();
            foreach (var collectible in collectibles)
            {
                if (collectible != null)
                {
                    Vector2Int collectibleGridPos = WorldToGrid(collectible.transform.position);
                    if (collectibleGridPos == gridPos) return true;
                }
            }
        }
        return false;
    }
    
    public bool HasBombAt(Vector2Int gridPos)
    {
        foreach (var bomb in bombs)
        {
            if (bomb != null)
            {
                Vector2Int bombGridPos = WorldToGrid(bomb.transform.position);
                if (bombGridPos == gridPos) return true;
            }
        }
        return false;
    }
    
    public bool HasExplosionAt(Vector2Int gridPos)
    {
        return explosions.Contains(gridPos);
    }
    
    // RewardSystem interface methods
    public Vector2 GetNearestEnemyPosition(Vector3 playerPosition)
    {
        Vector2 nearestPos = Vector2.zero;
        float minDistance = float.MaxValue;
        
        if (levelLoader != null)
        {
            List<GameObject> enemies = levelLoader.GetEnemies();
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    float distance = Vector3.Distance(playerPosition, enemy.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPos = enemy.transform.position;
                    }
                }
            }
        }
        
        return nearestPos;
    }
    
    public Vector2 GetNearestCollectiblePosition(Vector3 playerPosition)
    {
        Vector2 nearestPos = Vector2.zero;
        float minDistance = float.MaxValue;
        
        if (levelLoader != null)
        {
            List<GameObject> collectibles = levelLoader.GetCollectibles();
            foreach (var collectible in collectibles)
            {
                if (collectible != null)
                {
                    float distance = Vector3.Distance(playerPosition, collectible.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPos = collectible.transform.position;
                    }
                }
            }
        }
        
        return nearestPos;
    }
    
    public int GetRemainingEnemyCount()
    {
        if (levelLoader != null)
            return levelLoader.GetEnemies().Count;
        return 0;
    }
    
    public int GetRemainingCollectibleCount()
    {
        if (levelLoader != null)
            return levelLoader.GetCollectibles().Count;
        return 0;
    }
    
    // Wall destruction - delegate to LevelLoader grid system
    public void DestroyBreakableWall(Vector2Int gridPos)
    {
        if (levelLoader != null)
        {
            // This would need to be implemented in LevelLoader if needed
            Debug.Log($"[EnvManager] Wall destruction at {gridPos} - implement in LevelLoader if needed");
        }
    }
    
    public void RegisterBomb(GameObject bomb)
    {
        bombs.Add(bomb);
    }
    
    public void UnregisterBomb(GameObject bomb)
    {
        bombs.Remove(bomb);
    }
    
    public void AddExplosion(Vector2Int gridPos, float duration = 1f)
    {
        explosions.Add(gridPos);
        StartCoroutine(RemoveExplosionAfterDelay(gridPos, duration));
    }
    
    private IEnumerator RemoveExplosionAfterDelay(Vector2Int gridPos, float delay)
    {
        yield return new WaitForSeconds(delay);
        explosions.Remove(gridPos);
    }
    
    // Object removal - delegate to LevelLoader
    public void RemoveEnemy(GameObject enemy)
    {
        if (levelLoader != null)
        {
            levelLoader.RemoveEnemy(enemy);
        }
    }
    
    public void RemoveCollectible(GameObject collectible)
    {
        if (levelLoader != null)
        {
            levelLoader.RemoveCollectible(collectible);
        }
    }
}