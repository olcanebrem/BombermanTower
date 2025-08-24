using UnityEngine;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Bridge between Unity and Python PPO training script
/// Handles starting training, monitoring progress, and syncing results
/// </summary>
public class PPOTrainingBridge : MonoBehaviour
{
    [Header("Training Configuration")]
    public string pythonExecutable = "python";
    public string trainingScriptPath = "../../Python/train.py";
    public string configPath = "../../Python/ppo.yml";
    
    [Header("Training Control")]
    public bool startTrainingOnAwake = false;
    public bool autoSyncResults = true;
    public float syncInterval = 30f; // seconds
    
    [Header("Training Status")]
    [SerializeField] private bool isTraining = false;
    [SerializeField] private int currentTimesteps = 0;
    [SerializeField] private float currentReward = 0f;
    [SerializeField] private string trainingStatus = "Idle";
    
    // Events
    public event System.Action<RLTrainingData> OnTrainingCompleted;
    public event System.Action<string> OnTrainingStatusChanged;
    public event System.Action<float> OnTrainingProgressUpdated;
    
    // Private fields
    private Process trainingProcess;
    private string logFilePath;
    private string currentLevelName;
    private DateTime trainingStartTime;
    private RLTrainingData currentTrainingData;
    
    private void Awake()
    {
        // Setup paths
        SetupPaths();
        
        // Get current level name
        if (LevelManager.Instance != null)
        {
            currentLevelName = LevelManager.Instance.GetCurrentLevelName();
        }
        
        if (startTrainingOnAwake)
        {
            StartTraining();
        }
    }
    
    private void SetupPaths()
    {
        // Convert relative paths to absolute
        string projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));
        trainingScriptPath = Path.Combine(projectRoot, trainingScriptPath.Replace("../../", ""));
        configPath = Path.Combine(projectRoot, configPath.Replace("../../", ""));
        
        // Setup log file path
        logFilePath = Path.Combine(Application.persistentDataPath, "training_log.txt");
        
        UnityEngine.Debug.Log($"[PPOTrainingBridge] Training script: {trainingScriptPath}");
        UnityEngine.Debug.Log($"[PPOTrainingBridge] Config file: {configPath}");
        UnityEngine.Debug.Log($"[PPOTrainingBridge] Log file: {logFilePath}");
    }
    
    public void StartTraining()
    {
        if (isTraining)
        {
            UnityEngine.Debug.LogWarning("[PPOTrainingBridge] Training already in progress!");
            return;
        }
        
        if (!File.Exists(trainingScriptPath))
        {
            UnityEngine.Debug.LogError($"[PPOTrainingBridge] Training script not found: {trainingScriptPath}");
            return;
        }
        
        if (!File.Exists(configPath))
        {
            UnityEngine.Debug.LogError($"[PPOTrainingBridge] Config file not found: {configPath}");
            return;
        }
        
        // Create training data for this session
        currentTrainingData = CreateTrainingDataFromConfig();
        trainingStartTime = DateTime.Now;
        
        // Start Python training process
        StartTrainingProcess();
        
        // Start monitoring
        if (autoSyncResults)
        {
            InvokeRepeating(nameof(MonitorTrainingProgress), syncInterval, syncInterval);
        }
        
        isTraining = true;
        UpdateStatus("Training Started");
        UnityEngine.Debug.Log("[PPOTrainingBridge] Training started successfully!");
    }
    
    public void StopTraining()
    {
        if (!isTraining)
        {
            UnityEngine.Debug.LogWarning("[PPOTrainingBridge] No training in progress!");
            return;
        }
        
        // Stop monitoring
        CancelInvoke(nameof(MonitorTrainingProgress));
        
        // Terminate training process
        if (trainingProcess != null && !trainingProcess.HasExited)
        {
            try
            {
                trainingProcess.Kill();
                trainingProcess.WaitForExit(5000); // Wait up to 5 seconds
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[PPOTrainingBridge] Error stopping training: {e.Message}");
            }
        }
        
        // Final sync
        if (autoSyncResults)
        {
            SyncTrainingResults(final: true);
        }
        
        isTraining = false;
        UpdateStatus("Training Stopped");
        UnityEngine.Debug.Log("[PPOTrainingBridge] Training stopped!");
    }
    
    private void StartTrainingProcess()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = pythonExecutable,
                Arguments = $"\"{trainingScriptPath}\" --config \"{configPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            trainingProcess = new Process()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            
            // Event handlers
            trainingProcess.OutputDataReceived += OnTrainingOutputReceived;
            trainingProcess.ErrorDataReceived += OnTrainingErrorReceived;
            trainingProcess.Exited += OnTrainingProcessExited;
            
            trainingProcess.Start();
            trainingProcess.BeginOutputReadLine();
            trainingProcess.BeginErrorReadLine();
            
            UnityEngine.Debug.Log($"[PPOTrainingBridge] Training process started with PID: {trainingProcess.Id}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[PPOTrainingBridge] Failed to start training process: {e.Message}");
            isTraining = false;
        }
    }
    
    private RLTrainingData CreateTrainingDataFromConfig()
    {
        var trainingData = new RLTrainingData();
        
        try
        {
            // Read config file and extract parameters
            // For now, use default values - can be extended to parse YAML
            trainingData.seed = UnityEngine.Random.Range(10000, 99999);
            trainingData.learningRate = 0.0003f;
            trainingData.gamma = 0.99f;
            trainingData.epsilon = 0.2f;
            trainingData.gaeLambda = 0.95f;
            trainingData.entropyCoef = 0.01f;
            trainingData.vfCoef = 0.5f;
            trainingData.batchSize = 64;
            trainingData.nSteps = 2048;
            trainingData.nEpochs = 10;
            trainingData.maxGradNorm = 0.5f;
            trainingData.normalizeAdvantage = true;
            trainingData.maxSteps = 2000000; // total_timesteps from config
            trainingData.trainingNote = $"PPO Training started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            
            UnityEngine.Debug.Log($"[PPOTrainingBridge] Created training data: {trainingData}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[PPOTrainingBridge] Error creating training data: {e.Message}");
        }
        
        return trainingData;
    }
    
    private void MonitorTrainingProgress()
    {
        if (!isTraining || trainingProcess == null || trainingProcess.HasExited)
        {
            return;
        }
        
        // Parse training logs for progress
        ParseTrainingLogs();
        
        // Update training time
        if (currentTrainingData != null)
        {
            currentTrainingData.totalTrainingTime = (float)(DateTime.Now - trainingStartTime).TotalMinutes;
        }
        
        UnityEngine.Debug.Log($"[PPOTrainingBridge] Training progress - Timesteps: {currentTimesteps}, Reward: {currentReward:F3}");
    }
    
    private void ParseTrainingLogs()
    {
        if (!File.Exists(logFilePath))
        {
            return;
        }
        
        try
        {
            string[] lines = File.ReadAllLines(logFilePath);
            
            // Parse last few lines for latest metrics
            for (int i = Math.Max(0, lines.Length - 20); i < lines.Length; i++)
            {
                string line = lines[i];
                
                // Look for training metrics (this depends on your training script output format)
                if (line.Contains("timesteps"))
                {
                    // Parse timesteps
                    // Format example: "timesteps: 50000"
                    // Implement based on your actual log format
                }
                
                if (line.Contains("mean_reward"))
                {
                    // Parse reward
                    // Format example: "mean_reward: 1.25"
                    // Implement based on your actual log format
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[PPOTrainingBridge] Error parsing training logs: {e.Message}");
        }
    }
    
    private void SyncTrainingResults(bool final = false)
    {
        if (currentTrainingData == null)
        {
            return;
        }
        
        // Update current data with latest metrics
        currentTrainingData.totalTimesteps = currentTimesteps;
        currentTrainingData.avgReward = currentReward;
        currentTrainingData.totalTrainingTime = (float)(DateTime.Now - trainingStartTime).TotalMinutes;
        
        if (final)
        {
            // Final training completed
            currentTrainingData.trainingNote += " - Training completed successfully";
            
            // Save to level files
            if (LevelManager.Instance != null && !string.IsNullOrEmpty(currentLevelName))
            {
                LevelManager.Instance.SaveRLTrainingResults(currentTrainingData);
                UnityEngine.Debug.Log($"[PPOTrainingBridge] Training results saved for {currentLevelName}");
            }
            
            // Fire completion event
            OnTrainingCompleted?.Invoke(currentTrainingData);
        }
    }
    
    private void UpdateStatus(string status)
    {
        trainingStatus = status;
        OnTrainingStatusChanged?.Invoke(status);
        UnityEngine.Debug.Log($"[PPOTrainingBridge] Status: {status}");
    }
    
    #region Process Event Handlers
    
    private void OnTrainingOutputReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            // Log training output
            File.AppendAllText(logFilePath, e.Data + Environment.NewLine);
            UnityEngine.Debug.Log($"[PPO] {e.Data}");
        }
    }
    
    private void OnTrainingErrorReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            File.AppendAllText(logFilePath, $"ERROR: {e.Data}" + Environment.NewLine);
            UnityEngine.Debug.LogError($"[PPO Error] {e.Data}");
        }
    }
    
    private void OnTrainingProcessExited(object sender, EventArgs e)
    {
        isTraining = false;
        
        if (trainingProcess.ExitCode == 0)
        {
            UpdateStatus("Training Completed Successfully");
            SyncTrainingResults(final: true);
        }
        else
        {
            UpdateStatus($"Training Failed (Exit Code: {trainingProcess.ExitCode})");
        }
        
        // Stop monitoring
        CancelInvoke(nameof(MonitorTrainingProgress));
        
        UnityEngine.Debug.Log($"[PPOTrainingBridge] Training process exited with code: {trainingProcess.ExitCode}");
    }
    
    #endregion
    
    #region Public API
    
    public bool IsTraining => isTraining;
    public int CurrentTimesteps => currentTimesteps;
    public float CurrentReward => currentReward;
    public string TrainingStatus => trainingStatus;
    public RLTrainingData CurrentTrainingData => currentTrainingData;
    
    #endregion
    
    private void OnDestroy()
    {
        // Cleanup
        StopTraining();
    }
    
    #region Context Menu Actions
    
    [ContextMenu("Start Training")]
    public void StartTrainingMenu()
    {
        StartTraining();
    }
    
    [ContextMenu("Stop Training")]
    public void StopTrainingMenu()
    {
        StopTraining();
    }
    
    [ContextMenu("Sync Results")]
    public void SyncResultsMenu()
    {
        SyncTrainingResults();
    }
    
    #endregion
}