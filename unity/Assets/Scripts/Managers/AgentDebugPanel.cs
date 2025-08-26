using UnityEngine;
using Unity.MLAgents;
using Debug = UnityEngine.Debug;

public class AgentDebugPanel : MonoBehaviour
{
    [Header("Agent Reference")]
    public Agent agent;                   // ML-Agents Agent
    public PlayerAgent playerAgent;       // Kendi agent script'in varsa
    
    [Header("Component References")]
    public MLAgentsTrainingController trainingController;
    public TurnManager turnManager;
    public LevelSequencer levelSequencer;

    [Header("Debug Options")]
    public bool showDebugPanel = true;
    public bool showTrainingInfo = true;
    public bool showLevelInfo = true;
    public bool showActionInfo = true;
    public Color panelColor = new Color(0f, 0f, 0f, 0.7f); // Koyu şeffaf
    public Color textColor = Color.white;
    public Vector2 panelPosition = new Vector2(10, 10);
    public Vector2 panelSize = new Vector2(300, 400);

    private Rect panelRect;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    
    void Start()
    {
        // Auto-find components if not assigned
        if (agent == null) agent = FindObjectOfType<PlayerAgent>();
        if (playerAgent == null) playerAgent = FindObjectOfType<PlayerAgent>();
        if (trainingController == null) trainingController = MLAgentsTrainingController.Instance;
        if (turnManager == null) turnManager = TurnManager.Instance;
        if (levelSequencer == null) levelSequencer = LevelSequencer.Instance;
        
        // Initialize panel rect
        UpdatePanelRect();
    }
    
    void Update()
    {
        // Inspector'dan pozisyon ve boyut güncelle
        UpdatePanelRect();
    }
    
    private void UpdatePanelRect()
    {
        panelRect = new Rect(panelPosition.x, panelPosition.y, panelSize.x, panelSize.y);
    }

    void OnGUI()
    {
        if (!showDebugPanel) return;

        // Initialize styles if needed
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = textColor }
            };
            
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };
        }

        // Panel arkaplanı
        GUI.color = panelColor;
        GUI.Box(panelRect, "");
        GUI.color = Color.white;

        float yOffset = 10;
        float lineHeight = 18;
        float sectionSpacing = 25;

        // === HEADER ===
        GUI.Label(new Rect(panelRect.x + 10, panelRect.y + yOffset, panelSize.x - 20, lineHeight),
                  "🤖 ML-AGENT DEBUG PANEL", headerStyle);
        yOffset += sectionSpacing;

        // === TRAINING INFO ===
        if (showTrainingInfo)
        {
            GUI.Label(new Rect(panelRect.x + 10, panelRect.y + yOffset, panelSize.x - 20, lineHeight),
                      "📊 TRAINING STATUS", headerStyle);
            yOffset += lineHeight + 5;

            bool isTraining = trainingController != null && trainingController.IsTraining;
            bool isMLActive = turnManager != null && turnManager.IsMLAgentActive;
            
            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Training: {(isTraining ? "🟢 ACTIVE" : "🔴 INACTIVE")}", labelStyle);
            yOffset += lineHeight;
            
            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"ML-Agent: {(isMLActive ? "🟢 ACTIVE" : "🔴 INACTIVE")}", labelStyle);
            yOffset += lineHeight;

            if (trainingController != null)
            {
                GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                          $"Run ID: {trainingController.CurrentRunId}", labelStyle);
                yOffset += lineHeight;
            }

            yOffset += 10;
        }

        // === LEVEL INFO ===
        if (showLevelInfo && levelSequencer != null)
        {
            GUI.Label(new Rect(panelRect.x + 10, panelRect.y + yOffset, panelSize.x - 20, lineHeight),
                      "🎯 LEVEL INFO", headerStyle);
            yOffset += lineHeight + 5;

            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Current: {levelSequencer.GetCurrentLevelInfo()}", labelStyle);
            yOffset += lineHeight;
            
            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Cycles: {levelSequencer.GetCompletedCycles()}", labelStyle);
            yOffset += lineHeight;

            if (turnManager != null)
            {
                GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                          $"Turn: {turnManager.TurnCount}", labelStyle);
                yOffset += lineHeight;
            }

            yOffset += 10;
        }

        // === AGENT INFO ===
        if (agent != null)
        {
            GUI.Label(new Rect(panelRect.x + 10, panelRect.y + yOffset, panelSize.x - 20, lineHeight),
                      "🧠 AGENT METRICS", headerStyle);
            yOffset += lineHeight + 5;

            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Step: {agent.StepCount}", labelStyle);
            yOffset += lineHeight;

            float reward = agent.GetCumulativeReward();
            Color rewardColor = reward > 0 ? Color.green : reward < 0 ? Color.red : Color.white;
            GUI.color = rewardColor;
            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Reward: {reward:F3}", labelStyle);
            GUI.color = Color.white;
            yOffset += lineHeight;

            yOffset += 10;
        }

        // === ACTION INFO ===
        if (showActionInfo && playerAgent != null)
        {
            GUI.Label(new Rect(panelRect.x + 10, panelRect.y + yOffset, panelSize.x - 20, lineHeight),
                      "⚡ ACTIONS", headerStyle);
            yOffset += lineHeight + 5;

            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Action: {playerAgent.CurrentActionType}", labelStyle);
            yOffset += lineHeight;

            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Move Index: {playerAgent.CurrentMoveIndex}", labelStyle);
            yOffset += lineHeight;

            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Bomb Index: {playerAgent.CurrentBombIndex}", labelStyle);
            yOffset += lineHeight;

            GUI.Label(new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                      $"Direction: {playerAgent.LastActionDirection}", labelStyle);
            yOffset += lineHeight;

            yOffset += 10;
        }

        // === RUNTIME CONTROLS ===
        GUI.Label(new Rect(panelRect.x + 10, panelRect.y + yOffset, panelSize.x - 20, lineHeight),
                  "🎛️ CONTROLS", headerStyle);
        yOffset += lineHeight + 5;

        // Training toggle
        if (trainingController != null)
        {
            bool newTrainingState = GUI.Toggle(
                new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                trainingController.IsTraining,
                "Enable Training"
            );
            
            if (newTrainingState != trainingController.IsTraining)
            {
                trainingController.isTraining = newTrainingState;
                if (newTrainingState)
                    trainingController.StartTraining();
                else
                    trainingController.StopTraining();
            }
            yOffset += lineHeight + 2;
        }

        // Level sequence controls
        if (levelSequencer != null && levelSequencer.IsSequenceActive())
        {
            if (GUI.Button(new Rect(panelRect.x + 15, panelRect.y + yOffset, 100, 20), "Next Level"))
            {
                levelSequencer.LoadNextLevel();
            }
            
            if (GUI.Button(new Rect(panelRect.x + 125, panelRect.y + yOffset, 100, 20), "Reset Level"))
            {
                levelSequencer.RestartCurrentLevel();
            }
            yOffset += lineHeight + 5;
        }

        // Debug controls
        if (turnManager != null)
        {
            turnManager.debugnow = GUI.Toggle(
                new Rect(panelRect.x + 15, panelRect.y + yOffset, panelSize.x - 30, lineHeight),
                turnManager.debugnow,
                "Show Turn Debug"
            );
        }
    }
}