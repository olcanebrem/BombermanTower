using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;

public class LevelTrainingManager : MonoBehaviour
{
    public static LevelTrainingManager Instance { get; private set; }
    
    [Header("Training Settings")]
    public bool enableTrainingLogging = true;
    public bool showTrainingNotifications = true;
    
    private Dictionary<string, List<RLTrainingData>> cachedTrainingData;
    private string levelsPath;
    
    public event System.Action<RLTrainingData, TrainingComparison> OnTrainingCompleted;
    public event System.Action<string, LevelDifficulty> OnDifficultyAssessed;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void Initialize()
    {
        cachedTrainingData = new Dictionary<string, List<RLTrainingData>>();
        levelsPath = Path.Combine(Application.dataPath, "Levels");
        
        if (enableTrainingLogging)
        {
            Debug.Log("[LevelTrainingManager] Initialized with levels path: " + levelsPath);
        }
    }
    
    #region Training Data Management
    
    public void SaveTrainingData(string levelFileName, RLTrainingData trainingData)
    {
        try
        {
            string levelPath = GetLevelFilePath(levelFileName);
            if (!File.Exists(levelPath))
            {
                Debug.LogError($"[LevelTrainingManager] Level file not found: {levelPath}");
                return;
            }
            
            int nextVersion = GetNextVersionNumber(levelFileName);
            trainingData.version = nextVersion;
            trainingData.trainingDate = DateTime.Now;
            
            WriteTrainingToINI(levelPath, trainingData);
            
            UpdateCache(levelFileName, trainingData);
            
            if (enableTrainingLogging)
            {
                Debug.Log($"[LevelTrainingManager] Saved training data v{nextVersion} for {levelFileName}");
            }
            
            if (showTrainingNotifications)
            {
                var comparison = GetTrainingComparison(levelFileName);
                OnTrainingCompleted?.Invoke(trainingData, comparison);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelTrainingManager] Failed to save training data: {e.Message}");
        }
    }
    
    public List<RLTrainingData> GetAllTrainingVersions(string levelFileName)
    {
        if (cachedTrainingData.ContainsKey(levelFileName))
        {
            return new List<RLTrainingData>(cachedTrainingData[levelFileName]);
        }
        
        // Load from INI file and cache
        List<RLTrainingData> trainingData = LoadAllTrainingFromINI(GetLevelFilePath(levelFileName));
        cachedTrainingData[levelFileName] = trainingData;
        
        return trainingData;
    }
    
    public RLTrainingData GetLatestTraining(string levelFileName)
    {
        var allVersions = GetAllTrainingVersions(levelFileName);
        return allVersions.OrderByDescending(t => t.version).FirstOrDefault();
    }
    
    public RLTrainingData GetTrainingVersion(string levelFileName, int version)
    {
        var allVersions = GetAllTrainingVersions(levelFileName);
        return allVersions.FirstOrDefault(t => t.version == version);
    }
    
    public int GetNextVersionNumber(string levelFileName)
    {
        var allVersions = GetAllTrainingVersions(levelFileName);
        if (allVersions.Count == 0) return 1;
        
        return allVersions.Max(t => t.version) + 1;
    }
    
    public bool HasTrainingData(string levelFileName)
    {
        return GetAllTrainingVersions(levelFileName).Count > 0;
    }
    
    #endregion
    
    #region Training Analysis
    
    public TrainingComparison GetTrainingComparison(string levelFileName)
    {
        var comparison = new TrainingComparison(levelFileName);
        comparison.allVersions = GetAllTrainingVersions(levelFileName);
        comparison.UpdateComparison();
        return comparison;
    }
    
    public RLTrainingData GetBestTraining(string levelFileName, TrainingMetric metric)
    {
        var allVersions = GetAllTrainingVersions(levelFileName);
        if (allVersions.Count == 0) return null;
        
        return metric switch
        {
            TrainingMetric.AvgReward => allVersions.OrderByDescending(t => t.avgReward).First(),
            TrainingMetric.SuccessRate => allVersions.OrderByDescending(t => t.successRate).First(),
            TrainingMetric.Episodes => allVersions.OrderBy(t => t.episodes).First(),
            TrainingMetric.CollectiblesRatio => allVersions.OrderByDescending(t => t.CollectiblesPercentage).First(),
            TrainingMetric.Deaths => allVersions.OrderBy(t => t.deaths).First(),
            TrainingMetric.TrainingTime => allVersions.Where(t => t.totalTrainingTime > 0).OrderBy(t => t.totalTrainingTime).FirstOrDefault(),
            _ => allVersions.OrderByDescending(t => t.avgReward).First()
        };
    }
    
    public Dictionary<int, float> GetTrainingProgression(string levelFileName, TrainingMetric metric)
    {
        var allVersions = GetAllTrainingVersions(levelFileName)
            .OrderBy(t => t.version).ToList();
        
        var progression = new Dictionary<int, float>();
        
        foreach (var training in allVersions)
        {
            float value = metric switch
            {
                TrainingMetric.AvgReward => training.avgReward,
                TrainingMetric.SuccessRate => training.successRate,
                TrainingMetric.Episodes => training.episodes,
                TrainingMetric.CollectiblesRatio => training.CollectiblesPercentage * 100f,
                TrainingMetric.Deaths => training.deaths,
                TrainingMetric.TrainingTime => training.totalTrainingTime,
                _ => training.avgReward
            };
            
            progression[training.version] = value;
        }
        
        return progression;
    }
    
    public bool IsTrainingImproving(string levelFileName, int lastNVersions = 3)
    {
        var progression = GetTrainingProgression(levelFileName, TrainingMetric.AvgReward);
        if (progression.Count < lastNVersions) return false;
        
        var lastVersions = progression.OrderByDescending(p => p.Key).Take(lastNVersions).ToList();
        
        for (int i = 0; i < lastVersions.Count - 1; i++)
        {
            if (lastVersions[i].Value <= lastVersions[i + 1].Value)
                return false;
        }
        
        return true;
    }
    
    public float GetAverageImprovement(string levelFileName, TrainingMetric metric)
    {
        var progression = GetTrainingProgression(levelFileName, metric);
        if (progression.Count < 2) return 0f;
        
        var sortedVersions = progression.OrderBy(p => p.Key).ToList();
        float totalImprovement = 0f;
        int improvementCount = 0;
        
        for (int i = 1; i < sortedVersions.Count; i++)
        {
            float improvement = sortedVersions[i].Value - sortedVersions[i - 1].Value;
            totalImprovement += improvement;
            improvementCount++;
        }
        
        return improvementCount > 0 ? totalImprovement / improvementCount : 0f;
    }
    
    public string GenerateTrainingReport(string levelFileName)
    {
        var comparison = GetTrainingComparison(levelFileName);
        if (comparison.allVersions.Count == 0)
            return $"No training data found for {levelFileName}";
        
        var report = new StringBuilder();
        report.AppendLine($"=== TRAINING REPORT: {levelFileName} ===");
        report.AppendLine($"Total Training Sessions: {comparison.totalVersions}");
        report.AppendLine();
        
        report.AppendLine("BEST PERFORMANCES:");
        report.AppendLine($"• Highest Reward: v{comparison.bestReward.version} ({comparison.bestReward.avgReward:F3})");
        report.AppendLine($"• Best Success Rate: v{comparison.bestSuccessRate.version} ({comparison.bestSuccessRate.successRate:F1}%)");
        report.AppendLine($"• Fastest Training: v{comparison.fastestTraining.version} ({comparison.fastestTraining.totalTrainingTime:F1} min)");
        report.AppendLine();
        
        var difficulty = CalculateLevelDifficulty(levelFileName);
        report.AppendLine($"DIFFICULTY ASSESSMENT: {difficulty.rating}");
        report.AppendLine($"• Average Success Rate: {difficulty.averageSuccessRate:F1}%");
        report.AppendLine($"• Average Deaths: {difficulty.averageDeaths:F1}");
        report.AppendLine($"• Average Episodes: {difficulty.averageEpisodesToComplete:F1}");
        
        return report.ToString();
    }
    
    #endregion
    
    #region Difficulty Assessment
    
    public LevelDifficulty CalculateLevelDifficulty(string levelFileName)
    {
        var difficulty = new LevelDifficulty(levelFileName);
        var allTraining = GetAllTrainingVersions(levelFileName);
        difficulty.CalculateDifficulty(allTraining);
        
        OnDifficultyAssessed?.Invoke(levelFileName, difficulty);
        return difficulty;
    }
    
    #endregion
    
    #region Export/Import
    
    public void ExportTrainingData(string levelFileName, string exportPath)
    {
        try
        {
            var allTraining = GetAllTrainingVersions(levelFileName);
            var exportData = new TrainingExportData
            {
                levelName = levelFileName,
                exportDate = DateTime.Now,
                trainingData = allTraining
            };
            
            string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            File.WriteAllText(exportPath, json);
            
            if (enableTrainingLogging)
            {
                Debug.Log($"[LevelTrainingManager] Exported training data to: {exportPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelTrainingManager] Export failed: {e.Message}");
        }
    }
    
    public void ImportTrainingData(string levelFileName, string importPath)
    {
        try
        {
            if (!File.Exists(importPath))
            {
                Debug.LogError($"[LevelTrainingManager] Import file not found: {importPath}");
                return;
            }
            
            string json = File.ReadAllText(importPath);
            
            // Create a proper class for import data instead of using dynamic
            var importData = JsonConvert.DeserializeObject<TrainingExportData>(json);
            var trainingList = importData.trainingData;
            
            foreach (var training in trainingList)
            {
                SaveTrainingData(levelFileName, training);
            }
            
            if (enableTrainingLogging)
            {
                Debug.Log($"[LevelTrainingManager] Imported {trainingList.Count} training sessions for {levelFileName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelTrainingManager] Import failed: {e.Message}");
        }
    }
    
    public void ExportAllTrainingData(string exportPath)
    {
        try
        {
            var allData = new Dictionary<string, List<RLTrainingData>>();
            
            string[] levelFiles = Directory.GetFiles(levelsPath, "*.txt");
            foreach (string levelFile in levelFiles)
            {
                string fileName = Path.GetFileName(levelFile);
                allData[fileName] = GetAllTrainingVersions(fileName);
            }
            
            var exportData = new AllLevelsExportData
            {
                exportDate = DateTime.Now,
                totalLevels = allData.Keys.Count,
                allLevelsData = allData
            };
            
            string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            File.WriteAllText(exportPath, json);
            
            if (enableTrainingLogging)
            {
                Debug.Log($"[LevelTrainingManager] Exported all training data to: {exportPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelTrainingManager] Export all failed: {e.Message}");
        }
    }
    
    #endregion
    
    #region Integration Methods
    
    public void SaveCurrentLevelTraining(RLTrainingData trainingData)
    {
        if (LevelManager.Instance != null)
        {
            string currentLevel = LevelManager.Instance.GetCurrentLevelName();
            if (!string.IsNullOrEmpty(currentLevel))
            {
                SaveTrainingData(currentLevel, trainingData);
            }
        }
    }
    
    public RLTrainingData GetCurrentLevelTraining()
    {
        if (LevelManager.Instance != null)
        {
            string currentLevel = LevelManager.Instance.GetCurrentLevelName();
            if (!string.IsNullOrEmpty(currentLevel))
            {
                return GetLatestTraining(currentLevel);
            }
        }
        return null;
    }
    
    #endregion
    
    #region INI File Operations
    
    private void WriteTrainingToINI(string levelFilePath, RLTrainingData trainingData)
    {
        try
        {
            List<string> lines = File.ReadAllLines(levelFilePath).ToList();
            
            // Check if training data section already exists and find insertion point
            int insertionPoint = lines.Count;
            bool hasTrainingSection = false;
            
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains("# RL TRAINING DATA") || lines[i].StartsWith("[training_params"))
                {
                    hasTrainingSection = true;
                    break;
                }
            }
            
            // If no training section exists, add it at the end
            if (!hasTrainingSection)
            {
                lines.Add("");
                lines.Add("# ===================================");
                lines.Add("# RL TRAINING DATA");
                lines.Add("# ===================================");
                lines.Add("");
            }
            
            // Add the new training data version
            lines.Add($"# ===================================");
            lines.Add($"# RL TRAINING DATA v{trainingData.version}");
            lines.Add($"# ===================================");
            lines.Add("");
            
            // Training parameters section
            lines.Add($"[training_params_v{trainingData.version}]");
            lines.Add($"version={trainingData.version}");
            lines.Add($"seed={trainingData.seed}");
            lines.Add($"learning_rate={trainingData.learningRate.ToString("F6", CultureInfo.InvariantCulture)}");
            lines.Add($"gamma={trainingData.gamma.ToString("F3", CultureInfo.InvariantCulture)}");
            lines.Add($"epsilon={trainingData.epsilon.ToString("F3", CultureInfo.InvariantCulture)}");
            lines.Add($"max_steps={trainingData.maxSteps}");
            
            // PPO Hyperparameters
            lines.Add($"gae_lambda={trainingData.gaeLambda.ToString("F3", CultureInfo.InvariantCulture)}");
            lines.Add($"entropy_coef={trainingData.entropyCoef.ToString("F6", CultureInfo.InvariantCulture)}");
            lines.Add($"vf_coef={trainingData.vfCoef.ToString("F3", CultureInfo.InvariantCulture)}");
            lines.Add($"batch_size={trainingData.batchSize}");
            lines.Add($"buffer_size={trainingData.bufferSize}");
            lines.Add($"n_steps={trainingData.nSteps}");
            lines.Add($"n_epochs={trainingData.nEpochs}");
            lines.Add($"max_grad_norm={trainingData.maxGradNorm.ToString("F3", CultureInfo.InvariantCulture)}");
            lines.Add($"normalize_advantage={trainingData.normalizeAdvantage.ToString().ToLower()}");
            if (trainingData.clipRangeVf >= 0)
                lines.Add($"clip_range_vf={trainingData.clipRangeVf.ToString("F3", CultureInfo.InvariantCulture)}");
            if (trainingData.targetKl >= 0)
                lines.Add($"target_kl={trainingData.targetKl.ToString("F6", CultureInfo.InvariantCulture)}");
                
            // Network Architecture
            lines.Add($"hidden_units={trainingData.hiddenUnits}");
            lines.Add($"num_layers={trainingData.numLayers}");
            lines.Add($"normalize={trainingData.normalize.ToString().ToLower()}");
            
            // Schedules
            if (!string.IsNullOrEmpty(trainingData.learningRateSchedule))
                lines.Add($"learning_rate_schedule={trainingData.learningRateSchedule}");
            if (!string.IsNullOrEmpty(trainingData.betaSchedule))
                lines.Add($"beta_schedule={trainingData.betaSchedule}");
            if (!string.IsNullOrEmpty(trainingData.epsilonSchedule))
                lines.Add($"epsilon_schedule={trainingData.epsilonSchedule}");
            if (!string.IsNullOrEmpty(trainingData.visEncodeType))
                lines.Add($"vis_encode_type={trainingData.visEncodeType}");
                
            // Training Schedule
            lines.Add($"summary_freq={trainingData.summaryFreq}");
            lines.Add($"checkpoint_interval={trainingData.checkpointInterval}");
            lines.Add($"keep_checkpoints={trainingData.keepCheckpoints}");
            lines.Add($"reward_strength={trainingData.rewardStrength.ToString("F3", CultureInfo.InvariantCulture)}");
            
            lines.Add($"training_date={trainingData.trainingDate:yyyy-MM-ddTHH:mm:ssZ}");
            if (!string.IsNullOrEmpty(trainingData.trainingNote))
            {
                lines.Add($"training_note={trainingData.trainingNote}");
            }
            lines.Add("");
            
            // Training results section
            lines.Add($"[training_results_v{trainingData.version}]");
            lines.Add($"episodes={trainingData.episodes}");
            lines.Add($"avg_reward={trainingData.avgReward.ToString("F6", CultureInfo.InvariantCulture)}");
            lines.Add($"success_rate={trainingData.successRate.ToString("F3", CultureInfo.InvariantCulture)}");
            lines.Add($"deaths={trainingData.deaths}");
            lines.Add($"collectibles={trainingData.collectiblesFound}/{trainingData.totalCollectibles}");
            if (trainingData.totalTrainingTime > 0)
            {
                lines.Add($"training_time={trainingData.totalTrainingTime.ToString("F1", CultureInfo.InvariantCulture)}");
            }
            if (trainingData.totalTimesteps > 0)
            {
                lines.Add($"total_timesteps={trainingData.totalTimesteps}");
            }
            if (trainingData.fps > 0)
            {
                lines.Add($"training_fps={trainingData.fps.ToString("F1", CultureInfo.InvariantCulture)}");
            }
            
            // Advanced metrics
            if (trainingData.finalLoss != 0 || trainingData.policyLoss != 0)
            {
                lines.Add($"final_loss={trainingData.finalLoss.ToString("F6", CultureInfo.InvariantCulture)}");
                lines.Add($"policy_loss={trainingData.policyLoss.ToString("F6", CultureInfo.InvariantCulture)}");
                lines.Add($"value_loss={trainingData.valueLoss.ToString("F6", CultureInfo.InvariantCulture)}");
                lines.Add($"entropy_loss={trainingData.entropyLoss.ToString("F6", CultureInfo.InvariantCulture)}");
                lines.Add($"kl_divergence={trainingData.klDivergence.ToString("F6", CultureInfo.InvariantCulture)}");
                lines.Add($"explained_variance={trainingData.explainedVariance.ToString("F6", CultureInfo.InvariantCulture)}");
                lines.Add($"approx_kl={trainingData.approxKl.ToString("F6", CultureInfo.InvariantCulture)}");
            }
            lines.Add("");
            
            File.WriteAllLines(levelFilePath, lines);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelTrainingManager] Failed to write training data to INI: {e.Message}");
        }
    }
    
    private List<RLTrainingData> LoadAllTrainingFromINI(string levelFilePath)
    {
        var trainingDataList = new List<RLTrainingData>();
        
        try
        {
            if (!File.Exists(levelFilePath)) return trainingDataList;
            
            string[] lines = File.ReadAllLines(levelFilePath);
            RLTrainingData currentTraining = null;
            bool inParamsSection = false;
            bool inResultsSection = false;
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                // Check for training params section
                if (trimmedLine.StartsWith("[training_params_v") && trimmedLine.EndsWith("]"))
                {
                    currentTraining = new RLTrainingData();
                    inParamsSection = true;
                    inResultsSection = false;
                    continue;
                }
                
                // Check for training results section
                if (trimmedLine.StartsWith("[training_results_v") && trimmedLine.EndsWith("]"))
                {
                    inParamsSection = false;
                    inResultsSection = true;
                    continue;
                }
                
                // Check for section end
                if (trimmedLine.StartsWith("[") && !trimmedLine.StartsWith("[training_"))
                {
                    if (currentTraining != null)
                    {
                        trainingDataList.Add(currentTraining);
                        currentTraining = null;
                    }
                    inParamsSection = false;
                    inResultsSection = false;
                    continue;
                }
                
                // Parse parameters
                if (inParamsSection && currentTraining != null && trimmedLine.Contains("="))
                {
                    ParseTrainingParameter(currentTraining, trimmedLine);
                }
                
                // Parse results
                if (inResultsSection && currentTraining != null && trimmedLine.Contains("="))
                {
                    ParseTrainingResult(currentTraining, trimmedLine);
                }
            }
            
            // Add the last training data if exists
            if (currentTraining != null)
            {
                trainingDataList.Add(currentTraining);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelTrainingManager] Failed to load training data from INI: {e.Message}");
        }
        
        return trainingDataList;
    }
    
    private void ParseTrainingParameter(RLTrainingData training, string line)
    {
        var parts = line.Split('=');
        if (parts.Length != 2) return;
        
        string key = parts[0].Trim();
        string value = parts[1].Trim();
        
        try
        {
            switch (key.ToLower())
            {
                case "version":
                    training.version = int.Parse(value);
                    break;
                case "seed":
                    training.seed = int.Parse(value);
                    break;
                case "learning_rate":
                    training.learningRate = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "gamma":
                    training.gamma = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "epsilon":
                    training.epsilon = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "max_steps":
                    training.maxSteps = int.Parse(value);
                    break;
                case "training_date":
                    if (DateTime.TryParse(value, out DateTime date))
                        training.trainingDate = date;
                    break;
                case "training_note":
                    training.trainingNote = value;
                    break;
                case "gae_lambda":
                    training.gaeLambda = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "entropy_coef":
                    training.entropyCoef = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "vf_coef":
                    training.vfCoef = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "batch_size":
                    training.batchSize = int.Parse(value);
                    break;
                case "n_steps":
                    training.nSteps = int.Parse(value);
                    break;
                case "n_epochs":
                    training.nEpochs = int.Parse(value);
                    break;
                case "max_grad_norm":
                    training.maxGradNorm = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "normalize_advantage":
                    training.normalizeAdvantage = bool.Parse(value);
                    break;
                case "clip_range_vf":
                    training.clipRangeVf = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "target_kl":
                    training.targetKl = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "buffer_size":
                    training.bufferSize = int.Parse(value);
                    break;
                case "hidden_units":
                    training.hiddenUnits = int.Parse(value);
                    break;
                case "num_layers":
                    training.numLayers = int.Parse(value);
                    break;
                case "normalize":
                    training.normalize = bool.Parse(value);
                    break;
                case "learning_rate_schedule":
                    training.learningRateSchedule = value;
                    break;
                case "beta_schedule":
                    training.betaSchedule = value;
                    break;
                case "epsilon_schedule":
                    training.epsilonSchedule = value;
                    break;
                case "vis_encode_type":
                    training.visEncodeType = value;
                    break;
                case "summary_freq":
                    training.summaryFreq = int.Parse(value);
                    break;
                case "checkpoint_interval":
                    training.checkpointInterval = int.Parse(value);
                    break;
                case "keep_checkpoints":
                    training.keepCheckpoints = int.Parse(value);
                    break;
                case "reward_strength":
                    training.rewardStrength = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelTrainingManager] Failed to parse parameter {key}={value}: {e.Message}");
        }
    }
    
    private void ParseTrainingResult(RLTrainingData training, string line)
    {
        var parts = line.Split('=');
        if (parts.Length != 2) return;
        
        string key = parts[0].Trim();
        string value = parts[1].Trim();
        
        try
        {
            switch (key.ToLower())
            {
                case "episodes":
                    training.episodes = int.Parse(value);
                    break;
                case "avg_reward":
                    training.avgReward = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "success_rate":
                    training.successRate = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "deaths":
                    training.deaths = int.Parse(value);
                    break;
                case "collectibles":
                    ParseCollectiblesRatio(training, value);
                    break;
                case "training_time":
                    training.totalTrainingTime = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "total_timesteps":
                    training.totalTimesteps = int.Parse(value);
                    break;
                case "training_fps":
                    training.fps = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "final_loss":
                    training.finalLoss = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "policy_loss":
                    training.policyLoss = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "value_loss":
                    training.valueLoss = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "entropy_loss":
                    training.entropyLoss = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "kl_divergence":
                    training.klDivergence = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "explained_variance":
                    training.explainedVariance = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "approx_kl":
                    training.approxKl = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelTrainingManager] Failed to parse result {key}={value}: {e.Message}");
        }
    }
    
    private void ParseCollectiblesRatio(RLTrainingData training, string value)
    {
        var parts = value.Split('/');
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out int found) && int.TryParse(parts[1], out int total))
            {
                training.collectiblesFound = found;
                training.totalCollectibles = total;
            }
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private void UpdateCache(string levelFileName, RLTrainingData trainingData)
    {
        if (!cachedTrainingData.ContainsKey(levelFileName))
        {
            cachedTrainingData[levelFileName] = new List<RLTrainingData>();
        }
        
        cachedTrainingData[levelFileName].Add(trainingData);
    }
    
    private string GetLevelFilePath(string levelFileName)
    {
        return Path.Combine(levelsPath, levelFileName);
    }
    
    #endregion
}