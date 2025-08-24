using UnityEngine;
using System.Collections;

/// <summary>
/// Demo script showing the complete RL Training system
/// Add this to a GameObject in your scene to test the system
/// </summary>
public class RLTrainingDemo : MonoBehaviour
{
    [Header("Demo Settings")]
    [SerializeField] private bool runDemoOnStart = true;
    [SerializeField] private string demoLevelFile = "LEVEL_0001_v1.0.0_v4.4.txt";
    
    private void Start()
    {
        if (runDemoOnStart)
        {
            StartCoroutine(RunFullDemo());
        }
    }
    
    private IEnumerator RunFullDemo()
    {
        Debug.Log("🚀 === RL TRAINING SYSTEM DEMO STARTED ===");
        yield return new WaitForSeconds(1f);
        
        // Demo 1: Show existing training data
        Debug.Log("📊 DEMO 1: Reading existing training data...");
        ShowExistingTrainingData();
        yield return new WaitForSeconds(2f);
        
        // Demo 2: Add new training session
        Debug.Log("💾 DEMO 2: Adding new training session...");
        AddNewTrainingSession();
        yield return new WaitForSeconds(2f);
        
        // Demo 3: Show analytics
        Debug.Log("📈 DEMO 3: Showing training analytics...");
        ShowTrainingAnalytics();
        yield return new WaitForSeconds(2f);
        
        // Demo 4: Test difficulty assessment
        Debug.Log("🎯 DEMO 4: Testing difficulty assessment...");
        TestDifficultyAssessment();
        yield return new WaitForSeconds(2f);
        
        // Demo 5: Generate report
        Debug.Log("📋 DEMO 5: Generating training report...");
        GenerateTrainingReport();
        
        Debug.Log("✅ === RL TRAINING SYSTEM DEMO COMPLETED ===");
    }
    
    private void ShowExistingTrainingData()
    {
        if (LevelTrainingManager.Instance == null)
        {
            Debug.LogError("❌ LevelTrainingManager not found!");
            return;
        }
        
        var allTraining = LevelTrainingManager.Instance.GetAllTrainingVersions(demoLevelFile);
        Debug.Log($"📊 Found {allTraining.Count} existing training sessions:");
        
        foreach (var training in allTraining)
        {
            Debug.Log($"   v{training.version}: Reward={training.avgReward:F3}, Success={training.successRate:F1}%, " +
                     $"Episodes={training.episodes}, Deaths={training.deaths}");
            Debug.Log($"      Params: LR={training.learningRate:F6}, Gamma={training.gamma:F3}, Epsilon={training.epsilon:F3}");
            Debug.Log($"      Note: {training.trainingNote}");
        }
    }
    
    private void AddNewTrainingSession()
    {
        if (LevelTrainingManager.Instance == null) return;
        
        // Create new training data with realistic progression
        var newTraining = new RLTrainingData();
        
        // Advanced parameters
        newTraining.seed = Random.Range(100000, 999999);
        newTraining.learningRate = 0.001f;
        newTraining.gamma = 0.985f;
        newTraining.epsilon = 0.05f;  // Lower epsilon for exploitation
        newTraining.maxSteps = 80000;
        
        // Excellent results showing improvement
        newTraining.episodes = 72;  // Fewer episodes needed
        newTraining.avgReward = 0.95f;  // Higher reward
        newTraining.successRate = 89.5f;  // Much higher success
        newTraining.deaths = 8;  // Fewer deaths
        newTraining.collectiblesFound = 58;  // Better collectibles
        newTraining.totalCollectibles = 60;
        newTraining.totalTrainingTime = 142.3f;  // Less training time needed
        newTraining.trainingNote = "Advanced parameters with epsilon decay - achieved best performance";
        
        Debug.Log($"💾 Adding new training session: {newTraining}");
        LevelTrainingManager.Instance.SaveTrainingData(demoLevelFile, newTraining);
        Debug.Log($"✅ Successfully saved training v{newTraining.version}!");
    }
    
    private void ShowTrainingAnalytics()
    {
        if (LevelTrainingManager.Instance == null) return;
        
        // Show best performances
        var bestReward = LevelTrainingManager.Instance.GetBestTraining(demoLevelFile, TrainingMetric.AvgReward);
        var bestSuccess = LevelTrainingManager.Instance.GetBestTraining(demoLevelFile, TrainingMetric.SuccessRate);
        var fastestTraining = LevelTrainingManager.Instance.GetBestTraining(demoLevelFile, TrainingMetric.TrainingTime);
        
        Debug.Log("🏆 BEST PERFORMANCES:");
        if (bestReward != null)
            Debug.Log($"   Best Reward: v{bestReward.version} ({bestReward.avgReward:F3})");
        if (bestSuccess != null)
            Debug.Log($"   Best Success Rate: v{bestSuccess.version} ({bestSuccess.successRate:F1}%)");
        if (fastestTraining != null)
            Debug.Log($"   Fastest Training: v{fastestTraining.version} ({fastestTraining.totalTrainingTime:F1} min)");
        
        // Show progression
        var rewardProgression = LevelTrainingManager.Instance.GetTrainingProgression(demoLevelFile, TrainingMetric.AvgReward);
        Debug.Log("📈 Reward Progression:");
        foreach (var kvp in rewardProgression)
        {
            Debug.Log($"   v{kvp.Key}: {kvp.Value:F3}");
        }
        
        // Check if improving
        bool improving = LevelTrainingManager.Instance.IsTrainingImproving(demoLevelFile, 3);
        Debug.Log($"📊 Is training improving over last 3 versions? {(improving ? "✅ YES" : "❌ NO")}");
        
        // Show average improvement
        float avgImprovement = LevelTrainingManager.Instance.GetAverageImprovement(demoLevelFile, TrainingMetric.AvgReward);
        Debug.Log($"📊 Average reward improvement per version: {avgImprovement:F4}");
    }
    
    private void TestDifficultyAssessment()
    {
        if (LevelTrainingManager.Instance == null) return;
        
        var difficulty = LevelTrainingManager.Instance.CalculateLevelDifficulty(demoLevelFile);
        
        Debug.Log("🎯 LEVEL DIFFICULTY ASSESSMENT:");
        Debug.Log($"   Difficulty Rating: {difficulty.rating}");
        Debug.Log($"   Average Success Rate: {difficulty.averageSuccessRate:F1}%");
        Debug.Log($"   Average Deaths: {difficulty.averageDeaths:F1}");
        Debug.Log($"   Average Episodes: {difficulty.averageEpisodesToComplete:F1}");
        Debug.Log($"   Average Training Time: {difficulty.averageTrainingTime:F1} min");
        Debug.Log($"   Total Training Sessions: {difficulty.totalTrainingSessions}");
    }
    
    private void GenerateTrainingReport()
    {
        if (LevelTrainingManager.Instance == null) return;
        
        string report = LevelTrainingManager.Instance.GenerateTrainingReport(demoLevelFile);
        Debug.Log("📋 COMPREHENSIVE TRAINING REPORT:");
        Debug.Log($"\\n{report}");
    }
    
    #region Manual Test Methods (Right-click in Inspector)
    
    [ContextMenu("Show Training Data")]
    public void ShowTrainingDataManually()
    {
        ShowExistingTrainingData();
    }
    
    [ContextMenu("Add Random Training Session")]
    public void AddRandomTrainingSession()
    {
        AddNewTrainingSession();
    }
    
    [ContextMenu("Export Training Data")]
    public void ExportTrainingDataManually()
    {
        if (LevelTrainingManager.Instance == null) return;
        
        string exportPath = System.IO.Path.Combine(Application.persistentDataPath, 
            $"training_export_{demoLevelFile}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
        
        LevelTrainingManager.Instance.ExportTrainingData(demoLevelFile, exportPath);
        Debug.Log($"📤 Training data exported to: {exportPath}");
    }
    
    [ContextMenu("Test All Analytics")]
    public void TestAllAnalytics()
    {
        ShowTrainingAnalytics();
    }
    
    #endregion
}