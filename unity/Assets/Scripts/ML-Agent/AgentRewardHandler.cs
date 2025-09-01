using UnityEngine;
using Debug = UnityEngine.Debug;

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
        rewardSystem = rewards;
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

    public void ApplyStepRewards()
    {
        if (rewardSystem == null) return;

        rewardSystem.UpdateRewards();

        Vector2Int currentPos = new Vector2Int(playerController.X, playerController.Y);

        if (currentPos != lastPlayerPosition)
        {
            rewardSystem.ApplyExplorationReward();
            lastPlayerPosition = currentPos;
        }

        if (envManager != null)
        {
            int currentEnemyCount = envManager.GetRemainingEnemyCount();
            if (currentEnemyCount < lastEnemyCount)
            {
                int enemiesKilled = lastEnemyCount - currentEnemyCount;
                for (int i = 0; i < enemiesKilled; i++)
                    rewardSystem.ApplyEnemyKillReward();
                lastEnemyCount = currentEnemyCount;
            }
        }
    }

    public void SetRewardSystem(RewardSystem newRewardSystem)
    {
        this.rewardSystem = newRewardSystem;
    }
}