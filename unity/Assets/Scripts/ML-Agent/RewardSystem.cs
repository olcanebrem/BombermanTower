using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public enum CollectibleType
{
    Health,
    BombUpgrade,
    SpeedUpgrade,
    Score
}

public class RewardSystem : MonoBehaviour
{
    [Header("Reward Values")]
    [Space]
    [Header("Positive Rewards")]
    public float levelCompleteReward = 10f;
    public float enemyKillReward = 2f;
    public float collectibleReward = 1f;
    public float healthCollectibleReward = 1.5f;
    public float upgradeCollectibleReward = 2f;
    public float bombPlacedReward = 0.1f;
    public float wallDestroyReward = 0.2f;
    public float explorationReward = 0.05f;
    
    [Space]
    [Header("Negative Penalties")]
    public float deathPenalty = -5f;
    public float damagePenalty = -1f;
    public float wallCollisionPenalty = -0.1f;
    public float inactivityPenalty = -0.02f;
    public float stepPenalty = -0.001f;
    public float timeoutPenalty = -2f;
    public float bombSelfDamagePenalty = -0.5f;
    
    [Space]
    [Header("Distance-based Rewards")]
    public bool useDistanceRewards = true;
    public float enemyProximityReward = 0.01f;
    public float exitProximityReward = 0.02f;
    public float collectibleProximityReward = 0.01f;
    
    private PlayerAgent agent;
    private EnvManager envManager;
    private Vector3 lastPosition;
    private float lastEnemyDistance;
    private float lastExitDistance;
    private float lastCollectibleDistance;
    private int lastEnemyCount;
    private int lastCollectibleCount;
    
    private void Start()
    {
        agent = GetComponent<PlayerAgent>();
        envManager = FindObjectOfType<EnvManager>();
        
        // Subscribe to player events for reward triggers
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            SubscribeToPlayerEvents(playerController);
        }
        
        ResetDistanceTracking();
    }
    
    private void SubscribeToPlayerEvents(PlayerController playerController)
    {
        // Subscribe to damage/health events
        playerController.OnHealthChanged += OnPlayerHealthChanged;
        
        // Note: Other events like enemy killed, collectible gathered, etc.
        // will be handled by game systems calling the appropriate reward methods
    }
    
    private void OnPlayerHealthChanged()
    {
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null && lastHealth > 0)
        {
            int currentHealth = playerController.CurrentHealth;
            if (currentHealth < lastHealth)
            {
                // Player took damage
                int damage = lastHealth - currentHealth;
                ApplyDamageReward(damage);
                
                // Check if player died
                if (currentHealth <= 0)
                {
                    ApplyDeathPenalty();
                }
            }
            lastHealth = currentHealth;
        }
    }
    
    private void ResetDistanceTracking()
    {
        lastPosition = transform.position;
        lastEnemyDistance = GetNearestEnemyDistance();
        lastExitDistance = GetExitDistance();
        lastCollectibleDistance = GetNearestCollectibleDistance();
        lastEnemyCount = envManager.GetRemainingEnemyCount();
        lastCollectibleCount = envManager.GetRemainingCollectibleCount();
    }
    
    public void OnEpisodeBegin()
    {
        ResetDistanceTracking();
        
        // Initialize health tracking
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            lastHealth = playerController.CurrentHealth;
        }
    }
    
    // Positive Reward Methods
    public void ApplyLevelCompleteReward()
    {
        float bonus = 1f + (envManager.GetRemainingEnemyCount() * 0.5f); // Bonus for clearing enemies
        agent.AddReward(levelCompleteReward * bonus);
        LogReward("Level Complete", levelCompleteReward * bonus);
    }
    
    public void ApplyEnemyKillReward()
    {
        agent.AddReward(enemyKillReward);
        LogReward("Enemy Killed", enemyKillReward);
        
        // Check if all enemies are cleared
        if (envManager.GetRemainingEnemyCount() == 0)
        {
            float allEnemiesClearedBonus = 3f;
            agent.AddReward(allEnemiesClearedBonus);
            LogReward("All Enemies Cleared Bonus", allEnemiesClearedBonus);
        }
    }
    
    public void ApplyCollectibleReward(CollectibleType type)
    {
        float reward = collectibleReward;
        string rewardName = "Collectible";
        
        switch (type)
        {
            case CollectibleType.Health:
                reward = healthCollectibleReward;
                rewardName = "Health Collectible";
                break;
            case CollectibleType.BombUpgrade:
            case CollectibleType.SpeedUpgrade:
                reward = upgradeCollectibleReward;
                rewardName = "Upgrade Collectible";
                break;
            case CollectibleType.Score:
                reward = collectibleReward;
                rewardName = "Score Collectible";
                break;
        }
        
        agent.AddReward(reward);
        LogReward(rewardName, reward);
    }
    
    public void ApplyBombPlacedReward()
    {
        agent.AddReward(bombPlacedReward);
        LogReward("Bomb Placed", bombPlacedReward);
    }
    
    public void ApplyWallDestroyReward()
    {
        agent.AddReward(wallDestroyReward);
        LogReward("Wall Destroyed", wallDestroyReward);
    }
    
    public void ApplyExplorationReward()
    {
        agent.AddReward(explorationReward);
        LogReward("Exploration", explorationReward);
    }
    
    // Negative Penalty Methods
    public void ApplyDeathPenalty()
    {
        agent.AddReward(deathPenalty);
        LogReward("Death", deathPenalty);
    }
    
    public void ApplyDamageReward(int damage)
    {
        float penalty = damagePenalty * damage;
        agent.AddReward(penalty);
        LogReward($"Damage ({damage})", penalty);
    }
    
    public void ApplyWallCollisionPenalty()
    {
        agent.AddReward(wallCollisionPenalty);
        LogReward("Wall Collision", wallCollisionPenalty);
    }
    
    public void ApplyInactivityPenalty()
    {
        agent.AddReward(inactivityPenalty);
        LogReward("Inactivity", inactivityPenalty);
    }
    
    public void ApplyStepPenalty()
    {
        agent.AddReward(stepPenalty);
        // Don't log step penalty as it's called every frame
    }
    
    public void ApplyTimeoutPenalty()
    {
        agent.AddReward(timeoutPenalty);
        LogReward("Timeout", timeoutPenalty);
    }
    
    public void ApplyBombSelfDamagePenalty()
    {
        agent.AddReward(bombSelfDamagePenalty);
        LogReward("Bomb Self Damage", bombSelfDamagePenalty);
    }
    
    // Distance-based rewards (called each frame/step)
    public void UpdateDistanceRewards()
    {
        if (!useDistanceRewards) return;
        
        // Enemy proximity reward
        float currentEnemyDistance = GetNearestEnemyDistance();
        if (currentEnemyDistance > 0 && lastEnemyDistance > 0)
        {
            if (currentEnemyDistance < lastEnemyDistance)
            {
                float reward = enemyProximityReward * (lastEnemyDistance - currentEnemyDistance);
                agent.AddReward(reward);
            }
        }
        lastEnemyDistance = currentEnemyDistance;
        
        // Exit proximity reward (but only if most enemies are cleared)
        if (envManager.GetRemainingEnemyCount() <= 1)
        {
            float currentExitDistance = GetExitDistance();
            if (currentExitDistance < lastExitDistance)
            {
                float reward = exitProximityReward * (lastExitDistance - currentExitDistance);
                agent.AddReward(reward);
            }
            lastExitDistance = currentExitDistance;
        }
        
        // Collectible proximity reward
        float currentCollectibleDistance = GetNearestCollectibleDistance();
        if (currentCollectibleDistance > 0 && lastCollectibleDistance > 0)
        {
            if (currentCollectibleDistance < lastCollectibleDistance)
            {
                float reward = collectibleProximityReward * (lastCollectibleDistance - currentCollectibleDistance);
                agent.AddReward(reward);
            }
        }
        lastCollectibleDistance = currentCollectibleDistance;
    }
    
    // Helper methods for distance calculations
    private float GetNearestEnemyDistance()
    {
        Vector2 nearestEnemy = envManager.GetNearestEnemyPosition(transform.position);
        if (nearestEnemy == Vector2.zero) return 0f;
        return Vector2.Distance(transform.position, nearestEnemy);
    }
    
    private float GetExitDistance()
    {
        Vector2 exitPos = envManager.GetExitPosition();
        return Vector2.Distance(transform.position, exitPos);
    }
    
    private float GetNearestCollectibleDistance()
    {
        Vector2 nearestCollectible = envManager.GetNearestCollectiblePosition(transform.position);
        if (nearestCollectible == Vector2.zero) return 0f;
        return Vector2.Distance(transform.position, nearestCollectible);
    }
    
    // Progressive rewards based on game state
    public void ApplyProgressiveRewards()
    {
        // Reward for reducing enemy count
        int currentEnemyCount = envManager.GetRemainingEnemyCount();
        if (currentEnemyCount < lastEnemyCount)
        {
            float progressReward = 0.5f * (lastEnemyCount - currentEnemyCount);
            agent.AddReward(progressReward);
            LogReward("Enemy Progress", progressReward);
            lastEnemyCount = currentEnemyCount;
        }
        
        // Reward for collecting items
        int currentCollectibleCount = envManager.GetRemainingCollectibleCount();
        if (currentCollectibleCount < lastCollectibleCount)
        {
            float progressReward = 0.3f * (lastCollectibleCount - currentCollectibleCount);
            agent.AddReward(progressReward);
            LogReward("Collectible Progress", progressReward);
            lastCollectibleCount = currentCollectibleCount;
        }
    }
    
    
    // Curriculum learning support - adjust rewards based on performance
    public void ApplyCurriculumRewards(float episodePerformance)
    {
        // episodePerformance should be between 0-1 (0 = poor, 1 = excellent)
        float curriculumMultiplier = Mathf.Lerp(1.5f, 1.0f, episodePerformance);
        
        // Adjust certain rewards based on agent performance
        // This can help with learning progression
    }
    
    private void LogReward(string rewardType, float rewardValue)
    {
        if (rewardValue != 0)
        {
            Debug.Log($"[Reward] {rewardType}: {rewardValue:F3} | Total: {agent.GetCumulativeReward():F3}");
        }
    }
    
    // Public method to manually trigger distance reward updates
    public void UpdateRewards()
    {
        UpdateDistanceRewards();
        ApplyProgressiveRewards();
    }
    
}