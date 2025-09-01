using UnityEngine;

public class AgentRewardHandler
{
    private PlayerController playerController;
    private RewardSystem rewardSystem;
    private EnvManager envManager;
    private bool debugActions;

    private Vector2Int lastPlayerPosition;
    private int lastEnemyCount;

    public AgentRewardHandler(PlayerController player, RewardSystem rewards, EnvManager env, bool debug)
    {
        playerController = player;
        rewardSystem = rewards; // Should be passed from PlayerAgent, which handles finding it
        envManager = env;
        debugActions = debug;

        if (playerController != null)
        {
            lastPlayerPosition = new Vector2Int(playerController.X, playerController.Y);
        }
        if (envManager != null)
        {
            lastEnemyCount = envManager.GetRemainingEnemyCount();
        }
    }

    /// <summary>
    /// Ensures RewardSystem is always available, finds it if lost
    /// </summary>
    private RewardSystem GetRewardSystem()
    {
        if (rewardSystem == null)
        {
            rewardSystem = Object.FindObjectOfType<RewardSystem>();
            if (rewardSystem != null) Debug.Log("[AgentRewardHandler] RewardSystem re-found and reconnected!");
            else Debug.LogError("[AgentRewardHandler] RewardSystem NOT FOUND in scene!");
        }
        return rewardSystem;
    }

    public void ApplyStepRewards()
    {
        if (GetRewardSystem() == null) return;

        rewardSystem.UpdateRewards();

        Vector2Int currentPos = new Vector2Int(playerController.X, playerController.Y);

        // Movement reward (exploration)
        if (currentPos != lastPlayerPosition)
        {
            rewardSystem.ApplyExplorationReward();
            lastPlayerPosition = currentPos;
        }

        // Enemy count change detection
        if (envManager != null)
        {
            int currentEnemyCount = envManager.GetRemainingEnemyCount();
            if (currentEnemyCount < lastEnemyCount)
            {
                int enemiesKilled = lastEnemyCount - currentEnemyCount;
                for (int i = 0; i < enemiesKilled; i++) rewardSystem.ApplyEnemyKillReward();
                lastEnemyCount = currentEnemyCount;
            }
        }
    }
}