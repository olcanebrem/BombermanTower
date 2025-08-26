using UnityEngine;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using Debug = UnityEngine.Debug;

/// <summary>
/// Controller for ML-Agents training from within Unity
/// Replaces PPOTrainingBridge for native ML-Agents workflow
/// </summary>
public class MLAgentsTrainingController : MonoBehaviour
{
    public static MLAgentsTrainingController Instance { get; private set; }
    
    [Header("ML-Agents Training Configuration")]
    [SerializeField] private string configPath = "../../Python/config/bomberman_ppo_simple.yaml";
    [SerializeField] private string runId = "bomberman_training";
    [SerializeField] private bool autoGenerateRunId = true;
    [SerializeField] private bool generateConfigFromUnity = true;
    
    [Header("PPO Hyperparameters")]
    [SerializeField, Range(0.0001f, 0.01f), Tooltip("Learning rate for PPO optimizer")]
    private float learningRate = 0.0003f;
    
    [SerializeField, Range(0.8f, 0.999f), Tooltip("Discount factor for future rewards")]
    private float gamma = 0.99f;
    
    [SerializeField, Range(0.01f, 0.5f), Tooltip("PPO clipping parameter for policy updates")]
    private float epsilon = 0.2f;
    
    [SerializeField, Range(0.8f, 0.99f), Tooltip("GAE lambda parameter for advantage estimation")]
    private float gaeLambda = 0.95f;
    
    [SerializeField, Range(0.001f, 0.1f), Tooltip("Entropy coefficient to encourage exploration")]
    private float beta = 0.01f;
    
    [SerializeField, Range(32, 512), Tooltip("Batch size for PPO training")]
    private int batchSize = 64;
    
    [SerializeField, Range(512, 20480), Tooltip("Buffer size for experience replay")]
    private int bufferSize = 10240;
    
    [SerializeField, Range(3, 20), Tooltip("Number of epochs per PPO update")]
    private int numEpoch = 10;
    
    [SerializeField, Range(1024, 4096), Tooltip("Steps per environment before updating policy")]
    private int timeHorizon = 2048;
    
    [Header("Network Architecture")]
    [SerializeField, Range(128, 512), Tooltip("Hidden units per neural network layer")]
    private int hiddenUnits = 256;
    
    [SerializeField, Range(1, 4), Tooltip("Number of hidden layers in neural network")]
    private int numLayers = 2;
    
    [SerializeField, Tooltip("Normalize vector observations")]
    private bool normalize = false;
    
    [SerializeField, Tooltip("Normalize advantages during training")]
    private bool normalizeAdvantage = true;
    
    [Header("Additional PPO Parameters")]
    [SerializeField, Range(0.1f, 1.0f), Tooltip("Value function coefficient")]
    private float vfCoef = 0.5f;
    
    [SerializeField, Range(0.1f, 1.0f), Tooltip("Maximum gradient norm for clipping")]
    private float maxGradNorm = 0.5f;
    
    [SerializeField, Range(-1f, 1.0f), Tooltip("Clipping range for value function (-1 to disable)")]
    private float clipRangeVf = -1f;
    
    [SerializeField, Range(-1f, 0.1f), Tooltip("Target KL divergence (-1 to disable)")]
    private float targetKl = -1f;
    
    [SerializeField, Tooltip("Learning rate schedule type")]
    private string learningRateSchedule = "linear";
    
    [SerializeField, Tooltip("Beta schedule type")]  
    private string betaSchedule = "constant";
    
    [SerializeField, Tooltip("Epsilon schedule type")]
    private string epsilonSchedule = "linear";
    
    [SerializeField, Tooltip("Visual encoder type")]
    private string visEncodeType = "simple";
    
    [SerializeField, Tooltip("Reward signal strength")]
    private float rewardStrength = 1.0f;
    
    [Header("Training Schedule")]
    [SerializeField, Range(100000, 10000000), Tooltip("Total training timesteps")]
    private int maxSteps = 2000000;
    
    [SerializeField, Range(10000, 100000), Tooltip("Frequency of TensorBoard logging")]
    private int summaryFreq = 50000;
    
    [SerializeField, Range(50000, 500000), Tooltip("Frequency of model checkpoints")]
    private int checkpointInterval = 100000;
    
    [SerializeField, Range(1, 10), Tooltip("Number of checkpoints to keep")]
    private int keepCheckpoints = 5;
    
    [Header("Training Control")]
    [Tooltip("Master training control - controls all ML-Agent behavior")]
    public bool isTraining = false;
    [SerializeField] private bool autoParseResults = true;
    private bool startTrainingOnAwake = false; // Now private and controlled by isTraining
    [SerializeField] private float statusCheckInterval = 10f;
    
    [Header("Paths")]
    [SerializeField] private string pythonExecutable = @"C:\Users\olcan\AppData\Local\Programs\Python\Python310\python.exe";
    [SerializeField] private string mlagentsLearnPath = "mlagents-learn";
    [SerializeField] private string resultsParserPath = "../../Python/mlagents_results_parser.py";
    
    [Header("Training Status")]
    [SerializeField, TextArea(3, 8)] private string trainingStatus = "Ready to start training...";
    private bool internalIsTraining = false; // Internal training state
    [SerializeField] private string currentRunId = "";
    [SerializeField] private float trainingStartTime = 0f;
    
    // Events
    public event System.Action<string> OnTrainingStarted;
    public event System.Action<string> OnTrainingCompleted;
    public event System.Action<string> OnTrainingFailed;
    public event System.Action<RLTrainingData> OnResultsParsed;
    
    // Private fields
    private Process trainingProcess;
    private string actualConfigPath;
    private string actualResultsParserPath;
    
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
        // Setup paths
        SetupPaths();
        
        // Generate run ID if needed
        if (autoGenerateRunId)
        {
            currentRunId = GenerateRunId();
        }
        else
        {
            currentRunId = runId;
        }
        
        // Subscribe to level sequencer events for multi-level training
        if (LevelSequencer.Instance != null)
        {
            LevelSequencer.Instance.OnLevelSequenceChanged += OnLevelSequenceChanged;
            LevelSequencer.Instance.OnAllLevelsCycled += OnAllLevelsCycled;
        }
        
        UpdateStatus($"Initialized. Ready to start training with run ID: {currentRunId}");
        
        // Auto-start training if enabled
        if (isTraining)
        {
            StartCoroutine(DelayedStartTraining());
        }
    }
    
    private void SetupPaths()
    {
        string projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));
        
        actualConfigPath = Path.GetFullPath(Path.Combine(projectRoot, configPath.Replace("../../", "")));
        actualResultsParserPath = Path.GetFullPath(Path.Combine(projectRoot, resultsParserPath.Replace("../../", "")));
        
        UnityEngine.Debug.Log($"[MLAgentsTrainingController] Config path: {actualConfigPath}");
        UnityEngine.Debug.Log($"[MLAgentsTrainingController] Results parser: {actualResultsParserPath}");
    }
    
    private string GenerateRunId()
    {
        return $"{runId}_{DateTime.Now:yyyyMMdd_HHmmss}";
    }
    
    private IEnumerator DelayedStartTraining()
    {
        yield return new WaitForSeconds(2f); // Wait for everything to initialize
        StartTraining();
    }
    
    public void StartTraining()
    {
        if (isTraining)
        {
            UnityEngine.Debug.LogWarning("[MLAgentsTrainingController] Training already in progress!");
            return;
        }
        
        // Initialize multi-level sequence when training starts
        if (LevelSequencer.Instance != null && LevelSequencer.Instance.IsSequenceActive())
        {
            LevelSequencer.Instance.InitializeSequence(0);
            Debug.Log("[MLAgentsTrainingController] Initialized multi-level training sequence");
        }
        
        if (!File.Exists(actualConfigPath))
        {
            string error = $"Config file not found: {actualConfigPath}";
            UnityEngine.Debug.LogError($"[MLAgentsTrainingController] {error}");
            UpdateStatus($"❌ ERROR: {error}");
            return;
        }
        
        StartTrainingProcess();
    }
    
    public void StopTraining()
    {
        if (!internalIsTraining)
        {
            UnityEngine.Debug.LogWarning("[MLAgentsTrainingController] No training in progress!");
            return;
        }
        
        StopTrainingProcess();
    }
    
    private void StartTrainingProcess()
    {
        try
        {
            // Build mlagents-learn command
            string arguments = $"{mlagentsLearnPath} \"{actualConfigPath}\" --run-id={currentRunId} --force";
            
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = pythonExecutable,
                Arguments = $"-m mlagents.trainers.learn \"{actualConfigPath}\" --run-id={currentRunId} --force",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(actualConfigPath)
            };
            
            // Add protobuf compatibility environment variable
            startInfo.EnvironmentVariables["PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION"] = "python";
            
            trainingProcess = new Process()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            
            trainingProcess.OutputDataReceived += OnTrainingOutputReceived;
            trainingProcess.ErrorDataReceived += OnTrainingErrorReceived;
            trainingProcess.Exited += OnTrainingProcessExited;
            
            trainingProcess.Start();
            trainingProcess.BeginOutputReadLine();
            trainingProcess.BeginErrorReadLine();
            
            internalIsTraining = true;
            isTraining = true; // Update public property
            trainingStartTime = Time.time;
            
            UpdateStatus($"🚀 Training started with run ID: {currentRunId}\\nProcess ID: {trainingProcess.Id}\\nWaiting for Unity connection...");
            
            OnTrainingStarted?.Invoke(currentRunId);
            
            // Start status monitoring
            InvokeRepeating(nameof(UpdateTrainingStatus), statusCheckInterval, statusCheckInterval);
            
            UnityEngine.Debug.Log($"[MLAgentsTrainingController] Training started: {currentRunId}");
            
        }
        catch (Exception e)
        {
            string error = $"Failed to start training: {e.Message}";
            UnityEngine.Debug.LogError($"[MLAgentsTrainingController] {error}");
            UpdateStatus($"❌ ERROR: {error}");
            internalIsTraining = false;
            isTraining = false; // Update public property
        }
    }
    
    private void StopTrainingProcess()
    {
        CancelInvoke(nameof(UpdateTrainingStatus));
        
        if (trainingProcess != null && !trainingProcess.HasExited)
        {
            try
            {
                trainingProcess.Kill();
                trainingProcess.WaitForExit(5000);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[MLAgentsTrainingController] Error stopping process: {e.Message}");
            }
        }
        
        internalIsTraining = false;
        isTraining = false; // Update public property
        UpdateStatus($"⏹️ Training stopped manually at {DateTime.Now:HH:mm:ss}");
        
        UnityEngine.Debug.Log("[MLAgentsTrainingController] Training stopped manually");
    }
    
    private void UpdateTrainingStatus()
    {
        if (internalIsTraining && trainingProcess != null && !trainingProcess.HasExited)
        {
            float elapsedTime = Time.time - trainingStartTime;
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            
            UpdateStatus($"⏳ Training in progress...\\nRun ID: {currentRunId}\\nElapsed: {minutes:00}:{seconds:00}\\nProcess: Active");
        }
    }
    
    private void OnTrainingOutputReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            UnityEngine.Debug.Log($"[ML-Agents] {e.Data}");
            
            // Update status based on key messages
            if (e.Data.Contains("Connected to Unity environment"))
            {
                UpdateStatus($"✅ Connected to Unity\\nTraining active...\\nRun ID: {currentRunId}");
            }
            else if (e.Data.Contains("Learning was interrupted"))
            {
                UpdateStatus($"⚠️ Training interrupted\\nRun ID: {currentRunId}");
            }
        }
    }
    
    private void OnTrainingErrorReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            UnityEngine.Debug.LogError($"[ML-Agents Error] {e.Data}");
        }
    }
    
    private void OnTrainingProcessExited(object sender, EventArgs e)
    {
        CancelInvoke(nameof(UpdateTrainingStatus));
        
        internalIsTraining = false;
        isTraining = false; // Update public property
        
        if (trainingProcess.ExitCode == 0)
        {
            UpdateStatus($"✅ Training completed successfully!\\nRun ID: {currentRunId}\\nCompleted at: {DateTime.Now:HH:mm:ss}");
            OnTrainingCompleted?.Invoke(currentRunId);
            
            // Parse results if enabled
            if (autoParseResults)
            {
                StartCoroutine(ParseTrainingResults());
            }
        }
        else
        {
            UpdateStatus($"❌ Training failed\\nRun ID: {currentRunId}\\nExit Code: {trainingProcess.ExitCode}\\nFailed at: {DateTime.Now:HH:mm:ss}");
            OnTrainingFailed?.Invoke($"Exit code: {trainingProcess.ExitCode}");
        }
        
        UnityEngine.Debug.Log($"[MLAgentsTrainingController] Training process exited with code: {trainingProcess.ExitCode}");
    }
    
    private IEnumerator ParseTrainingResults()
    {
        yield return new WaitForSeconds(2f); // Wait for files to be written
        
        UpdateStatus($"📊 Parsing training results...\\nRun ID: {currentRunId}");
        
        try
        {
            // Run results parser
            string arguments = $"\"{actualResultsParserPath}\" --run-id={currentRunId} --logs-dir=results --unity-levels=\"{Path.Combine(Application.dataPath, "Levels")}\"";
            
            ProcessStartInfo parseStartInfo = new ProcessStartInfo()
            {
                FileName = pythonExecutable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(actualResultsParserPath)
            };
            
            // Add protobuf compatibility environment variable
            parseStartInfo.EnvironmentVariables["PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION"] = "python";
            
            using (Process parseProcess = Process.Start(parseStartInfo))
            {
                string output = parseProcess.StandardOutput.ReadToEnd();
                string error = parseProcess.StandardError.ReadToEnd();
                
                parseProcess.WaitForExit();
                
                if (parseProcess.ExitCode == 0)
                {
                    UpdateStatus($"✅ Results parsed successfully!\\nRun ID: {currentRunId}\\nData integrated with level files");
                    UnityEngine.Debug.Log($"[MLAgentsTrainingController] Results parsing completed:\\n{output}");
                }
                else
                {
                    UpdateStatus($"⚠️ Results parsing failed\\nError: {error}");
                    UnityEngine.Debug.LogError($"[MLAgentsTrainingController] Results parsing failed: {error}");
                }
            }
        }
        catch (Exception e)
        {
            UpdateStatus($"❌ Error parsing results: {e.Message}");
            UnityEngine.Debug.LogError($"[MLAgentsTrainingController] Error parsing results: {e.Message}");
        }
    }
    
    private void UpdateStatus(string status)
    {
        trainingStatus = status;
        UnityEngine.Debug.Log($"[MLAgentsTrainingController] Status: {status.Replace("\\n", " | ")}");
    }
    
    #region Public API
    
    public bool IsTraining => isTraining;
    public string CurrentRunId => currentRunId;
    public string TrainingStatus => trainingStatus;
    public float ElapsedTrainingTime => isTraining ? Time.time - trainingStartTime : 0f;
    
    public void SetRunId(string newRunId)
    {
        if (!internalIsTraining)
        {
            currentRunId = newRunId;
            UpdateStatus($"Run ID changed to: {currentRunId}");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[MLAgentsTrainingController] Cannot change run ID during training");
        }
    }
    
    #endregion
    
    #region Context Menu Actions
    
    [ContextMenu("Start ML-Agents Training")]
    public void StartTrainingMenu()
    {
        StartTraining();
    }
    
    [ContextMenu("Stop ML-Agents Training")]
    public void StopTrainingMenu()
    {
        StopTraining();
    }
    
    [ContextMenu("Generate New Run ID")]
    public void GenerateNewRunId()
    {
        if (!internalIsTraining)
        {
            currentRunId = GenerateRunId();
            UpdateStatus($"Generated new run ID: {currentRunId}");
        }
    }
    
    [ContextMenu("Parse Latest Results")]
    public void ParseLatestResults()
    {
        if (!internalIsTraining)
        {
            StartCoroutine(ParseTrainingResults());
        }
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (LevelSequencer.Instance != null)
        {
            LevelSequencer.Instance.OnLevelSequenceChanged -= OnLevelSequenceChanged;
            LevelSequencer.Instance.OnAllLevelsCycled -= OnAllLevelsCycled;
        }
        
        StopTraining();
    }
    
    // Level sequence event handlers
    private void OnLevelSequenceChanged(int currentIndex, int totalLevels)
    {
        UpdateStatus($"🎯 Training Level: {currentIndex + 1}/{totalLevels}\\nRun ID: {currentRunId}\\nMulti-level curriculum active");
        Debug.Log($"[MLAgentsTrainingController] Level sequence: {currentIndex + 1}/{totalLevels}");
    }
    
    private void OnAllLevelsCycled(int cycleCount)
    {
        Debug.Log($"[MLAgentsTrainingController] Completed level cycle #{cycleCount} - curriculum learning continues");
        UpdateStatus($"🔄 Level Cycle #{cycleCount}\\nRun ID: {currentRunId}\\nCurriculum learning active");
    }
}