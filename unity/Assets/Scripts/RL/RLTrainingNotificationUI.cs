using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Linq;
using TMPro;

public class RLTrainingNotificationUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject notificationPanel;
    public TextMeshProUGUI notificationTitle;
    public TextMeshProUGUI notificationContent;
    public Button closeButton;
    public Button viewReportButton;
    public Button exportButton;
    
    [Header("Notification Settings")]
    public float autoHideDelay = 10f;
    public bool showImprovementOnly = false;
    
    private RLTrainingData currentTrainingData;
    private TrainingComparison currentComparison;
    private string currentLevelName;
    
    private void Start()
    {
        // Subscribe to training manager events
        if (LevelTrainingManager.Instance != null)
        {
            LevelTrainingManager.Instance.OnTrainingCompleted += HandleTrainingCompleted;
            LevelTrainingManager.Instance.OnDifficultyAssessed += HandleDifficultyAssessed;
        }
        
        // Setup UI events
        if (closeButton != null)
            closeButton.onClick.AddListener(HideNotification);
            
        if (viewReportButton != null)
            viewReportButton.onClick.AddListener(ShowDetailedReport);
            
        if (exportButton != null)
            exportButton.onClick.AddListener(ExportTrainingData);
        
        // Hide notification panel initially
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (LevelTrainingManager.Instance != null)
        {
            LevelTrainingManager.Instance.OnTrainingCompleted -= HandleTrainingCompleted;
            LevelTrainingManager.Instance.OnDifficultyAssessed -= HandleDifficultyAssessed;
        }
    }
    
    private void HandleTrainingCompleted(RLTrainingData trainingData, TrainingComparison comparison)
    {
        currentTrainingData = trainingData;
        currentComparison = comparison;
        currentLevelName = comparison.levelName;
        
        // Check if we should show notification
        if (showImprovementOnly && !IsImprovement(trainingData, comparison))
            return;
        
        ShowTrainingCompletedNotification(trainingData, comparison);
    }
    
    private void HandleDifficultyAssessed(string levelName, LevelDifficulty difficulty)
    {
        if (LevelTrainingManager.Instance.enableTrainingLogging)
        {
            Debug.Log($"[RLTrainingNotificationUI] Level {levelName} assessed as {difficulty.rating}");
        }
    }
    
    private bool IsImprovement(RLTrainingData newTraining, TrainingComparison comparison)
    {
        if (comparison.allVersions.Count < 2) return true; // First training is always shown
        
        // Get previous training (second latest)
        var previousTraining = comparison.allVersions
            .OrderByDescending(t => t.version)
            .Skip(1)
            .FirstOrDefault();
        
        if (previousTraining == null) return true;
        
        // Check for improvements
        return newTraining.avgReward > previousTraining.avgReward ||
               newTraining.successRate > previousTraining.successRate ||
               newTraining.CollectiblesPercentage > previousTraining.CollectiblesPercentage;
    }
    
    private void ShowTrainingCompletedNotification(RLTrainingData trainingData, TrainingComparison comparison)
    {
        if (notificationPanel == null) return;
        
        // Build notification content
        var content = new StringBuilder();
        content.AppendLine($"<b>Training v{trainingData.version} Completed!</b>");
        content.AppendLine();
        
        // Show key metrics
        content.AppendLine($"<color=yellow>Results:</color>");
        content.AppendLine($"• Episodes: {trainingData.episodes}");
        content.AppendLine($"• Avg Reward: {trainingData.avgReward:F3}");
        content.AppendLine($"• Success Rate: {trainingData.successRate:F1}%");
        content.AppendLine($"• Collectibles: {trainingData.CollectiblesRatio}");
        content.AppendLine($"• Deaths: {trainingData.deaths}");
        
        // Show improvements if available
        if (comparison.allVersions.Count > 1)
        {
            var improvements = GetImprovements(trainingData, comparison);
            if (!string.IsNullOrEmpty(improvements))
            {
                content.AppendLine();
                content.AppendLine($"<color=green>Improvements:</color>");
                content.AppendLine(improvements);
            }
        }
        
        // Show best records
        content.AppendLine();
        content.AppendLine($"<color=cyan>Best Records:</color>");
        content.AppendLine($"• Best Reward: v{comparison.bestReward.version} ({comparison.bestReward.avgReward:F3})");
        content.AppendLine($"• Best Success: v{comparison.bestSuccessRate.version} ({comparison.bestSuccessRate.successRate:F1}%)");
        
        // Update UI
        if (notificationTitle != null)
            notificationTitle.text = $"Level: {comparison.levelName}";
            
        if (notificationContent != null)
            notificationContent.text = content.ToString();
        
        // Show notification
        notificationPanel.SetActive(true);
        
        // Auto-hide after delay
        if (autoHideDelay > 0)
        {
            Invoke(nameof(HideNotification), autoHideDelay);
        }
    }
    
    private string GetImprovements(RLTrainingData newTraining, TrainingComparison comparison)
    {
        var improvements = new StringBuilder();
        
        // Get previous training
        var previousTraining = comparison.allVersions
            .OrderByDescending(t => t.version)
            .Skip(1)
            .FirstOrDefault();
        
        if (previousTraining == null) return "";
        
        // Check reward improvement
        float rewardDiff = newTraining.avgReward - previousTraining.avgReward;
        if (rewardDiff > 0.001f)
        {
            improvements.AppendLine($"• Reward: +{rewardDiff:F3} ({(rewardDiff / previousTraining.avgReward * 100):F1}%)");
        }
        
        // Check success rate improvement
        float successDiff = newTraining.successRate - previousTraining.successRate;
        if (successDiff > 0.1f)
        {
            improvements.AppendLine($"• Success Rate: +{successDiff:F1}%");
        }
        
        // Check collectibles improvement
        float collectiblesDiff = newTraining.CollectiblesPercentage - previousTraining.CollectiblesPercentage;
        if (collectiblesDiff > 0.01f)
        {
            improvements.AppendLine($"• Collectibles: +{collectiblesDiff * 100:F1}%");
        }
        
        // Check death reduction
        int deathDiff = previousTraining.deaths - newTraining.deaths;
        if (deathDiff > 0)
        {
            improvements.AppendLine($"• Deaths Reduced: -{deathDiff}");
        }
        
        return improvements.ToString();
    }
    
    private void HideNotification()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
            
        // Cancel auto-hide if manually closed
        CancelInvoke(nameof(HideNotification));
    }
    
    private void ShowDetailedReport()
    {
        if (LevelTrainingManager.Instance != null && !string.IsNullOrEmpty(currentLevelName))
        {
            string report = LevelTrainingManager.Instance.GenerateTrainingReport(currentLevelName);
            
            // Show in Unity console for now (could be extended to show in UI)
            Debug.Log($"[RLTrainingReport]\n{report}");
            
            // Could also show in a separate UI panel
            ShowReportInPanel(report);
        }
    }
    
    private void ShowReportInPanel(string report)
    {
        // This could be implemented to show the report in a separate detailed panel
        // For now, we'll just log it to console
        Debug.Log($"Detailed Training Report:\n{report}");
    }
    
    private void ExportTrainingData()
    {
        if (LevelTrainingManager.Instance != null && !string.IsNullOrEmpty(currentLevelName))
        {
            string exportPath = System.IO.Path.Combine(Application.persistentDataPath, 
                $"training_export_{currentLevelName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
            
            LevelTrainingManager.Instance.ExportTrainingData(currentLevelName, exportPath);
            
            Debug.Log($"[RLTrainingNotificationUI] Training data exported to: {exportPath}");
            
            // Show success message (could be a toast notification)
            ShowExportSuccessMessage(exportPath);
        }
    }
    
    private void ShowExportSuccessMessage(string exportPath)
    {
        // Simple success indication - could be enhanced with a toast notification
        if (notificationContent != null)
        {
            string originalText = notificationContent.text;
            notificationContent.text = $"<color=green>Export Successful!</color>\nSaved to: {exportPath}";
            
            // Restore original text after 3 seconds
            Invoke(() => {
                if (notificationContent != null)
                    notificationContent.text = originalText;
            }, 3f);
        }
    }
    
    // Helper method for delayed action
    private void Invoke(System.Action action, float delay)
    {
        StartCoroutine(DelayedAction(action, delay));
    }
    
    private System.Collections.IEnumerator DelayedAction(System.Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }
    
    #region Public API for Manual Usage
    
    public void ShowCustomNotification(string title, string content)
    {
        if (notificationTitle != null) notificationTitle.text = title;
        if (notificationContent != null) notificationContent.text = content;
        if (notificationPanel != null) notificationPanel.SetActive(true);
    }
    
    public void ShowImprovementNotification(RLTrainingData newData, RLTrainingData previousBest)
    {
        var content = new StringBuilder();
        content.AppendLine($"<b>New Personal Best!</b>");
        content.AppendLine();
        content.AppendLine($"<color=green>New Record:</color>");
        content.AppendLine($"• Reward: {newData.avgReward:F3} (was {previousBest.avgReward:F3})");
        content.AppendLine($"• Success Rate: {newData.successRate:F1}% (was {previousBest.successRate:F1}%)");
        
        ShowCustomNotification("Training Achievement!", content.ToString());
    }
    
    #endregion
}