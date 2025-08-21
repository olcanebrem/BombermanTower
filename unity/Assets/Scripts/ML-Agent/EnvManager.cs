using UnityEngine;
using Unity.MLAgents;
using System.Collections.Generic;
using System.Collections;

public class EnvManager : MonoBehaviour
{
    [Header("Environment Settings")]
    public int MapWidth = 15;
    public int MapHeight = 15;
    public int MaxStepsPerEpisode = 3000;
    
    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject breakableWallPrefab;
    public GameObject enemyPrefab;
    public GameObject collectiblePrefab;
    public GameObject exitPrefab;
    public GameObject playerPrefab;
    
    [Header("Generation Settings")]
    [Range(0f, 1f)] public float breakableWallDensity = 0.3f;
    public int enemyCount = 3;
    public int collectibleCount = 5;
    
    private int[,] wallGrid; // 0 = empty, 1 = breakable, 2 = unbreakable
    private List<GameObject> enemies = new List<GameObject>();
    private List<GameObject> collectibles = new List<GameObject>();
    private List<GameObject> bombs = new List<GameObject>();
    private List<Vector2Int> explosions = new List<Vector2Int>();
    private GameObject exitObject;
    
    private PlayerAgent playerAgent;
    private Vector2Int playerSpawnPos;
    private Vector2Int exitPos;
    
    private void Start()
    {
        playerAgent = FindObjectOfType<PlayerAgent>();
        GenerateLevel();
    }
    
    public void GenerateLevel()
    {
        ClearLevel();
        InitializeWallGrid();
        SpawnWalls();
        SpawnPlayer();
        SpawnExit();
        SpawnEnemies();
        SpawnCollectibles();
    }
    
    private void ClearLevel()
    {
        // Clear enemies
        foreach (var enemy in enemies)
        {
            if (enemy != null) DestroyImmediate(enemy);
        }
        enemies.Clear();
        
        // Clear collectibles
        foreach (var collectible in collectibles)
        {
            if (collectible != null) DestroyImmediate(collectible);
        }
        collectibles.Clear();
        
        // Clear bombs
        foreach (var bomb in bombs)
        {
            if (bomb != null) DestroyImmediate(bomb);
        }
        bombs.Clear();
        
        // Clear exit
        if (exitObject != null) DestroyImmediate(exitObject);
        
        // Clear explosions
        explosions.Clear();
        
        // Clear walls
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }
    }
    
    private void InitializeWallGrid()
    {
        wallGrid = new int[MapWidth, MapHeight];
        
        // Create border walls (unbreakable)
        for (int x = 0; x < MapWidth; x++)
        {
            wallGrid[x, 0] = 2; // Bottom border
            wallGrid[x, MapHeight - 1] = 2; // Top border
        }
        
        for (int y = 0; y < MapHeight; y++)
        {
            wallGrid[0, y] = 2; // Left border
            wallGrid[MapWidth - 1, y] = 2; // Right border
        }
        
        // Create internal structure (unbreakable walls in grid pattern)
        for (int x = 2; x < MapWidth - 2; x += 2)
        {
            for (int y = 2; y < MapHeight - 2; y += 2)
            {
                wallGrid[x, y] = 2;
            }
        }
        
        // Add breakable walls randomly
        for (int x = 1; x < MapWidth - 1; x++)
        {
            for (int y = 1; y < MapHeight - 1; y++)
            {
                if (wallGrid[x, y] == 0 && Random.value < breakableWallDensity)
                {
                    // Don't place breakable walls too close to spawn area
                    if (Vector2Int.Distance(new Vector2Int(x, y), new Vector2Int(1, 1)) > 2)
                    {
                        wallGrid[x, y] = 1;
                    }
                }
            }
        }
    }
    
    private void SpawnWalls()
    {
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector3 position = GridToWorld(new Vector2Int(x, y));
                
                if (wallGrid[x, y] == 1) // Breakable wall
                {
                    GameObject wall = Instantiate(breakableWallPrefab, position, Quaternion.identity, transform);
                    wall.name = $"BreakableWall_{x}_{y}";
                }
                else if (wallGrid[x, y] == 2) // Unbreakable wall
                {
                    GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity, transform);
                    wall.name = $"Wall_{x}_{y}";
                }
            }
        }
    }
    
    private void SpawnPlayer()
    {
        playerSpawnPos = new Vector2Int(1, 1);
        Vector3 spawnPosition = GridToWorld(playerSpawnPos);
        
        if (playerAgent.transform != null)
        {
            playerAgent.transform.position = spawnPosition;
        }
    }
    
    private void SpawnExit()
    {
        // Find a suitable position for the exit (far from spawn)
        Vector2Int bestPos = new Vector2Int(MapWidth - 2, MapHeight - 2);
        
        // Try to find an empty position
        for (int attempts = 0; attempts < 10; attempts++)
        {
            Vector2Int candidate = new Vector2Int(
                Random.Range(MapWidth / 2, MapWidth - 1),
                Random.Range(MapHeight / 2, MapHeight - 1)
            );
            
            if (wallGrid[candidate.x, candidate.y] == 0)
            {
                bestPos = candidate;
                break;
            }
        }
        
        exitPos = bestPos;
        Vector3 exitPosition = GridToWorld(exitPos);
        exitObject = Instantiate(exitPrefab, exitPosition, Quaternion.identity, transform);
        exitObject.name = "Exit";
    }
    
    private void SpawnEnemies()
    {
        int spawnedEnemies = 0;
        int maxAttempts = 50;
        
        while (spawnedEnemies < enemyCount && maxAttempts > 0)
        {
            Vector2Int randomPos = new Vector2Int(
                Random.Range(1, MapWidth - 1),
                Random.Range(1, MapHeight - 1)
            );
            
            // Check if position is valid (empty and not too close to player spawn)
            if (wallGrid[randomPos.x, randomPos.y] == 0 && 
                Vector2Int.Distance(randomPos, playerSpawnPos) > 3 &&
                randomPos != exitPos)
            {
                Vector3 enemyPosition = GridToWorld(randomPos);
                GameObject enemy = Instantiate(enemyPrefab, enemyPosition, Quaternion.identity, transform);
                enemy.name = $"Enemy_{spawnedEnemies}";
                enemies.Add(enemy);
                spawnedEnemies++;
            }
            
            maxAttempts--;
        }
    }
    
    private void SpawnCollectibles()
    {
        int spawnedCollectibles = 0;
        int maxAttempts = 50;
        
        while (spawnedCollectibles < collectibleCount && maxAttempts > 0)
        {
            Vector2Int randomPos = new Vector2Int(
                Random.Range(1, MapWidth - 1),
                Random.Range(1, MapHeight - 1)
            );
            
            // Check if position is valid
            if (wallGrid[randomPos.x, randomPos.y] == 0 &&
                Vector2Int.Distance(randomPos, playerSpawnPos) > 2 &&
                randomPos != exitPos &&
                !HasEnemyAt(randomPos))
            {
                Vector3 collectiblePosition = GridToWorld(randomPos);
                GameObject collectible = Instantiate(collectiblePrefab, collectiblePosition, Quaternion.identity, transform);
                collectible.name = $"Collectible_{spawnedCollectibles}";
                collectibles.Add(collectible);
                spawnedCollectibles++;
            }
            
            maxAttempts--;
        }
    }
    
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x + 0.5f),
            Mathf.FloorToInt(worldPosition.y + 0.5f)
        );
    }
    
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector3(gridPosition.x, gridPosition.y, 0);
    }
    
    public Vector3 GetPlayerSpawnPosition()
    {
        return GridToWorld(playerSpawnPos);
    }
    
    public Vector2 GetExitPosition()
    {
        return GridToWorld(exitPos);
    }
    
    public int GetWallType(Vector2Int gridPos)
    {
        if (gridPos.x < 0 || gridPos.x >= MapWidth || gridPos.y < 0 || gridPos.y >= MapHeight)
            return 2; // Out of bounds = unbreakable wall
        
        return wallGrid[gridPos.x, gridPos.y];
    }
    
    public bool HasEnemyAt(Vector2Int gridPos)
    {
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                Vector2Int enemyGridPos = WorldToGrid(enemy.transform.position);
                if (enemyGridPos == gridPos) return true;
            }
        }
        return false;
    }
    
    public bool HasCollectibleAt(Vector2Int gridPos)
    {
        foreach (var collectible in collectibles)
        {
            if (collectible != null)
            {
                Vector2Int collectibleGridPos = WorldToGrid(collectible.transform.position);
                if (collectibleGridPos == gridPos) return true;
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
    
    public Vector2 GetNearestEnemyPosition(Vector3 playerPosition)
    {
        Vector2 nearestPos = Vector2.zero;
        float minDistance = float.MaxValue;
        
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
        
        return nearestPos;
    }
    
    public Vector2 GetNearestCollectiblePosition(Vector3 playerPosition)
    {
        Vector2 nearestPos = Vector2.zero;
        float minDistance = float.MaxValue;
        
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
        
        return nearestPos;
    }
    
    public void DestroyBreakableWall(Vector2Int gridPos)
    {
        if (gridPos.x >= 0 && gridPos.x < MapWidth && gridPos.y >= 0 && gridPos.y < MapHeight)
        {
            if (wallGrid[gridPos.x, gridPos.y] == 1)
            {
                wallGrid[gridPos.x, gridPos.y] = 0;
                
                // Find and destroy the wall GameObject
                foreach (Transform child in transform)
                {
                    if (child.name == $"BreakableWall_{gridPos.x}_{gridPos.y}")
                    {
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }
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
    
    public void RemoveEnemy(GameObject enemy)
    {
        enemies.Remove(enemy);
    }
    
    public void RemoveCollectible(GameObject collectible)
    {
        collectibles.Remove(collectible);
    }
    
    public int GetRemainingEnemyCount()
    {
        return enemies.Count;
    }
    
    public int GetRemainingCollectibleCount()
    {
        return collectibles.Count;
    }
}