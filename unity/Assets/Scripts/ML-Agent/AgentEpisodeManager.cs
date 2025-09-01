using UnityEngine;
using System.Collections;

public class AgentEpisodeManager
{
    private PlayerAgent agent; // Reference back to the main agent to call EndEpisode
    private PlayerController playerController;
    private EnvManager envManager;
    private RewardSystem rewardSystem;
    private bool debugActions;

    private int episodeSteps = 0;
    private float episodeStartTime;

    public AgentEpisodeManager(PlayerAgent agentRef, PlayerController player, EnvManager env, RewardSystem rewards, bool debug)
    {
        agent = agentRef;
        playerController = player;
        envManager = env;
        rewardSystem = rewards;
        debugActions = debug;
    }

    public void OnEpisodeBegin(float currentStartTime)
    {
        if (debugActions) Debug.Log("[AgentEpisodeManager] Episode starting...");
        
        episodeSteps = 0;
        episodeStartTime = currentStartTime;

        // Reset environment
        envManager?.ResetEnvironment();

        // Reset reward system
        rewardSystem?.OnEpisodeBegin();

        // Re-find RewardSystem if lost during scene lifecycle (PlayerAgent handles this initially)
        if (rewardSystem == null)
        {
            rewardSystem = Object.FindObjectOfType<RewardSystem>();
            if (rewardSystem == null) Debug.LogWarning("[AgentEpisodeManager] RewardSystem not found in OnEpisodeBegin!");
        }

        if (debugActions) Debug.Log($"[AgentEpisodeManager] Episode began");
    }

    public void CheckEpisodeTermination()
    {
        // Death condition
        if (playerController.CurrentHealth <= 0)
        {
            rewardSystem?.ApplyDeathPenalty();
            Debug.Log("[AgentEpisodeManager] Episode ended: Player died - ending episode only");
            agent.EndEpisode(); // Call EndEpisode on the main Agent
            return;
        }

        // Victory condition
        if (envManager != null && envManager.GetRemainingEnemyCount() == 0)
        {
            Vector2Int playerPos = new Vector2Int(playerController.X, playerController.Y);
            Vector2Int exitPos = new Vector2Int(
                Mathf.RoundToInt(envManager.GetExitPosition().x),
                Mathf.RoundToInt(envManager.GetExitPosition().y)
            );

            if (playerPos == exitPos)
            {
                rewardSystem?.ApplyLevelCompleteReward();
                if (debugActions) Debug.Log("[AgentEpisodeManager] Episode ended: Level completed!");
                agent.EndEpisode();
                if (agent.UseMLAgent) agent.StartCoroutine(LoadNextLevelDelayed());
                return;
            }
        }

        // Timeout condition (max steps from config)
        if (episodeSteps >= 3000)
        {
            rewardSystem?.ApplyTimeoutPenalty();
            if (debugActions) Debug.Log("[AgentEpisodeManager] Episode ended: Timeout");
            agent.EndEpisode();
            if (agent.UseMLAgent) agent.StartCoroutine(LoadNextLevelDelayed());
            return;
        }
        episodeSteps++; // Increment step count here, or after action applied
    }

    public void ForceEndEpisode()
    {
        Debug.Log("[AgentEpisodeManager] Episode manually ended");
        agent.EndEpisode();
        agent.StartCoroutine(RestartEpisodeDelayed());
    }

    public IEnumerator LoadNextLevelDelayed()
    {
        yield return null; // Wait a frame for EndEpisode to complete
        Debug.Log("[AgentEpisodeManager] Loading next level in training sequence");
        LevelSequencer.Instance?.LoadNextLevel();
    }

    public IEnumerator RestartEpisodeDelayed()
    {
        yield return null; // Wait a frame for EndEpisode to complete
        Debug.Log("[AgentEpisodeManager] Restarting episode manually");
        if (TurnManager.Instance != null && TurnManager.Instance.IsMLAgentActive)
        {
            Debug.Log("[AgentEpisodeManager] Calling TurnManager.HandlePlayerDeathEvent");
            TurnManager.Instance.HandlePlayerDeathEvent(playerController);
        }
        else
        {
            Debug.Log("[AgentEpisodeManager] Manual restart - calling OnEpisodeBegin");
            OnEpisodeBegin(Time.time); // Pass current time for fresh episodeStartTime
        }
    }

    public string GetEpisodeStats()
    {
        float episodeTime = Time.time - episodeStartTime;
        return $"Episode: {episodeSteps} steps, {episodeTime:F1}s, " +
               $"Health: {playerController.CurrentHealth}/{playerController.MaxHealth}, " +
               $"Enemies: {(envManager != null ? envManager.GetRemainingEnemyCount() : 0)}";
    }
}