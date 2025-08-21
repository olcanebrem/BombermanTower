using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class PlayerAgent : Agent
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public LayerMask obstacleLayerMask;
    
    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public int maxBombs = 3;
    public float bombCooldown = 0.5f;
    
    [Header("Health Settings")]
    public int maxHealth = 3;
    public int currentHealth;
    
    [Header("Components")]
    private Rigidbody2D rb;
    private EnvManager envManager;
    private RewardSystem rewardSystem;
    
    private int activeBombs = 0;
    private float lastBombTime;
    private Vector2 lastPosition;
    private int steps;
    
    // 8-directional movement vectors
    private Vector2[] moveDirections = new Vector2[]
    {
        Vector2.up,           // 0: Up
        Vector2.right,        // 1: Right  
        Vector2.down,         // 2: Down
        Vector2.left,         // 3: Left
        new Vector2(1, 1).normalized,    // 4: Up-Right
        new Vector2(1, -1).normalized,   // 5: Down-Right
        new Vector2(-1, -1).normalized,  // 6: Down-Left
        new Vector2(-1, 1).normalized    // 7: Up-Left
    };

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        envManager = FindObjectOfType<EnvManager>();
        rewardSystem = GetComponent<RewardSystem>();
        
        currentHealth = maxHealth;
        lastPosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        // Reset player state
        currentHealth = maxHealth;
        activeBombs = 0;
        steps = 0;
        lastBombTime = 0f;
        
        // Reset position to spawn point
        transform.position = envManager.GetPlayerSpawnPosition();
        lastPosition = transform.position;
        
        // Reset velocity
        rb.velocity = Vector2.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Player position (normalized)
        Vector2 normalizedPos = new Vector2(
            transform.position.x / envManager.MapWidth,
            transform.position.y / envManager.MapHeight
        );
        sensor.AddObservation(normalizedPos);
        
        // Player health (normalized)
        sensor.AddObservation((float)currentHealth / maxHealth);
        
        // Bomb status
        sensor.AddObservation((float)activeBombs / maxBombs);
        sensor.AddObservation(Time.time - lastBombTime > bombCooldown ? 1f : 0f);
        
        // Player velocity
        sensor.AddObservation(rb.velocity.normalized);
        
        // Grid-based observations around player (9x9 grid)
        CollectGridObservations(sensor, 4); // 4 cells in each direction
        
        // Distance to nearest enemy, collectible, exit
        CollectDistanceObservations(sensor);
    }
    
    private void CollectGridObservations(VectorSensor sensor, int radius)
    {
        Vector2Int playerGridPos = envManager.WorldToGrid(transform.position);
        
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int checkPos = playerGridPos + new Vector2Int(x, y);
                
                // Wall detection (0 = empty, 1 = breakable, 2 = unbreakable)
                int wallType = envManager.GetWallType(checkPos);
                sensor.AddObservation(wallType == 0 ? 0f : wallType == 1 ? 0.5f : 1f);
                
                // Enemy detection
                bool hasEnemy = envManager.HasEnemyAt(checkPos);
                sensor.AddObservation(hasEnemy ? 1f : 0f);
                
                // Collectible detection
                bool hasCollectible = envManager.HasCollectibleAt(checkPos);
                sensor.AddObservation(hasCollectible ? 1f : 0f);
                
                // Bomb/explosion detection
                bool hasBomb = envManager.HasBombAt(checkPos);
                bool hasExplosion = envManager.HasExplosionAt(checkPos);
                sensor.AddObservation(hasBomb ? 1f : 0f);
                sensor.AddObservation(hasExplosion ? 1f : 0f);
            }
        }
    }
    
    private void CollectDistanceObservations(VectorSensor sensor)
    {
        // Nearest enemy distance and direction
        Vector2 nearestEnemy = envManager.GetNearestEnemyPosition(transform.position);
        if (nearestEnemy != Vector2.zero)
        {
            Vector2 enemyDirection = (nearestEnemy - (Vector2)transform.position).normalized;
            float enemyDistance = Vector2.Distance(transform.position, nearestEnemy) / envManager.MapWidth;
            sensor.AddObservation(enemyDirection);
            sensor.AddObservation(enemyDistance);
        }
        else
        {
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(1f);
        }
        
        // Nearest collectible distance and direction
        Vector2 nearestCollectible = envManager.GetNearestCollectiblePosition(transform.position);
        if (nearestCollectible != Vector2.zero)
        {
            Vector2 collectibleDirection = (nearestCollectible - (Vector2)transform.position).normalized;
            float collectibleDistance = Vector2.Distance(transform.position, nearestCollectible) / envManager.MapWidth;
            sensor.AddObservation(collectibleDirection);
            sensor.AddObservation(collectibleDistance);
        }
        else
        {
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(1f);
        }
        
        // Exit distance and direction
        Vector2 exitPos = envManager.GetExitPosition();
        Vector2 exitDirection = (exitPos - (Vector2)transform.position).normalized;
        float exitDistance = Vector2.Distance(transform.position, exitPos) / envManager.MapWidth;
        sensor.AddObservation(exitDirection);
        sensor.AddObservation(exitDistance);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        steps++;
        
        // Get discrete actions
        int moveAction = actions.DiscreteActions[0]; // 0-8 (8 directions + no movement)
        int bombAction = actions.DiscreteActions[1]; // 0-1 (no bomb, place bomb)
        
        // Apply movement
        if (moveAction > 0 && moveAction <= 8)
        {
            Vector2 moveDirection = moveDirections[moveAction - 1];
            MovePlayer(moveDirection);
        }
        
        // Apply bomb action
        if (bombAction == 1)
        {
            TryPlaceBomb();
        }
        
        // Apply penalties for time and inactivity
        rewardSystem.ApplyStepPenalty();
        
        // Check if player moved (encourage exploration)
        float distanceMoved = Vector2.Distance(transform.position, lastPosition);
        if (distanceMoved < 0.1f)
        {
            rewardSystem.ApplyInactivityPenalty();
        }
        
        lastPosition = transform.position;
        
        // End episode if too many steps
        if (steps >= envManager.MaxStepsPerEpisode)
        {
            rewardSystem.ApplyTimeoutPenalty();
            EndEpisode();
        }
    }
    
    private void MovePlayer(Vector2 direction)
    {
        Vector2 targetPosition = (Vector2)transform.position + direction * moveSpeed * Time.fixedDeltaTime;
        
        // Check for obstacles
        if (!Physics2D.OverlapCircle(targetPosition, 0.4f, obstacleLayerMask))
        {
            rb.velocity = direction * moveSpeed;
        }
        else
        {
            rb.velocity = Vector2.zero;
            rewardSystem.ApplyWallCollisionPenalty();
        }
    }
    
    private void TryPlaceBomb()
    {
        if (activeBombs < maxBombs && Time.time - lastBombTime > bombCooldown)
        {
            Vector2Int gridPos = envManager.WorldToGrid(transform.position);
            Vector3 bombPosition = envManager.GridToWorld(gridPos);
            
            if (!envManager.HasBombAt(gridPos))
            {
                GameObject bomb = Instantiate(bombPrefab, bombPosition, Quaternion.identity);
                BombController bombController = bomb.GetComponent<BombController>();
                bombController.SetOwner(this);
                
                activeBombs++;
                lastBombTime = Time.time;
                
                rewardSystem.ApplyBombPlacedReward();
            }
        }
    }
    
    public void OnBombExploded()
    {
        activeBombs = Mathf.Max(0, activeBombs - 1);
    }
    
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        rewardSystem.ApplyDamageReward(damage);
        
        if (currentHealth <= 0)
        {
            rewardSystem.ApplyDeathPenalty();
            EndEpisode();
        }
    }
    
    public void OnCollectibleCollected(CollectibleType type)
    {
        rewardSystem.ApplyCollectibleReward(type);
    }
    
    public void OnEnemyKilled()
    {
        rewardSystem.ApplyEnemyKillReward();
    }
    
    public void OnExitReached()
    {
        rewardSystem.ApplyLevelCompleteReward();
        EndEpisode();
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual control for testing
        var discreteActions = actionsOut.DiscreteActions;
        
        // Movement
        discreteActions[0] = 0; // Default: no movement
        
        if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.D)) discreteActions[0] = 4; // Up-Right
        else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.D)) discreteActions[0] = 5; // Down-Right
        else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.A)) discreteActions[0] = 6; // Down-Left
        else if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.A)) discreteActions[0] = 7; // Up-Left
        else if (Input.GetKey(KeyCode.W)) discreteActions[0] = 1; // Up
        else if (Input.GetKey(KeyCode.D)) discreteActions[0] = 2; // Right
        else if (Input.GetKey(KeyCode.S)) discreteActions[0] = 3; // Down
        else if (Input.GetKey(KeyCode.A)) discreteActions[0] = 4; // Left
        
        // Bomb
        discreteActions[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }
}