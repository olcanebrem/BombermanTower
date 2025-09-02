using UnityEngine;
using System.Collections;
using Debug = UnityEngine.Debug;

public class AgentEpisodeManager
{
    private readonly PlayerAgent agent;
    private readonly PlayerController playerController;
    private readonly EnvManager envManager;
    private RewardSystem rewardSystem;
    private readonly bool debugActions;

    private float episodeStartTime;

    public AgentEpisodeManager(PlayerAgent agentRef, PlayerController player, EnvManager env, RewardSystem rewards, bool debug)
    {
        agent = agentRef;
        playerController = player;
        envManager = env;
        rewardSystem = rewards;
        debugActions = debug;
    }

    public void OnEpisodeBegin()
    {
        if (debugActions) Debug.Log("[AgentEpisodeManager] Episode starting...");
        episodeStartTime = Time.time;
        
        // Only reset environment on first episode or manual reset, not on every episode begin
        if (envManager != null && !envManager.IsResettingEnvironment)
        {
            envManager.ResetEnvironment();
        }
        
        rewardSystem?.OnEpisodeBegin();
    }

    public void CheckEpisodeTermination(int currentSteps)
    {
        if (playerController.CurrentHealth <= 0)
        {
            rewardSystem?.ApplyDeathPenalty();
            if (debugActions) Debug.Log("[AgentEpisodeManager] Episode ended: Player died.");
            agent.EndEpisode();
            return;
        }

        if (envManager != null && envManager.GetRemainingEnemyCount() == 0)
        {
            Vector2Int playerPos = new Vector2Int(playerController.X, playerController.Y);
            Vector2Int exitPos = Vector2Int.RoundToInt(envManager.GetExitPosition());
            if (playerPos == exitPos)
            {
                rewardSystem?.ApplyLevelCompleteReward();
                if (debugActions) Debug.Log("[AgentEpisodeManager] Episode ended: Level completed!");
                agent.EndEpisode();
                if (agent.UseMLAgent) agent.StartCoroutine(LoadNextLevelDelayed());
                return;
            }
        }

        if (currentSteps >= 3000)
        {
            rewardSystem?.ApplyTimeoutPenalty();
            if (debugActions) Debug.Log("[AgentEpisodeManager] Episode ended: Timeout");
            agent.EndEpisode();
            if (agent.UseMLAgent) agent.StartCoroutine(LoadNextLevelDelayed());
            return;
        }
    }

    public void SetRewardSystem(RewardSystem newRewardSystem)
    {
        this.rewardSystem = newRewardSystem;
    }

    public IEnumerator LoadNextLevelDelayed()
    {
        yield return null;
        Debug.Log("[AgentEpisodeManager] Loading next level in training sequence...");
        LevelSequencer.Instance?.LoadNextLevel();
    }
}