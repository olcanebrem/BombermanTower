using UnityEngine;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

/// <summary>
/// Manages multi-level sequence for ML-Agent training
/// Handles level progression, cycling, and curriculum learning
/// Separated from LevelLoader to follow Single Responsibility Principle
/// </summary>
public class LevelSequencer : MonoBehaviour
{
    public static LevelSequencer Instance { get; private set; }
    
    [Header("Multi-Level Sequence Settings")]
    [Tooltip("Enable multi-level curriculum learning")]
    public bool useMultiLevelSequence = true;
    
    [Tooltip("Available levels for sequence training")]
    [SerializeField] private List<LevelFileEntry> availableLevels = new List<LevelFileEntry>();
    
    [Tooltip("Start level index (0-based)")]
    [SerializeField] private int startLevelIndex = 0;
    
    [Header("Sequence State")]
    [SerializeField, Tooltip("Current level in sequence")]
    private int currentLevelIndex = 0;
    
    [SerializeField, Tooltip("Total cycles completed")]
    private int cyclesCompleted = 0;
    
    [Header("Components")]
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private HoudiniLevelImporter levelImporter;
    
    // Events for level sequence management
    public event System.Action<int, int> OnLevelSequenceChanged; // (currentIndex, totalLevels)
    public event System.Action<string> OnLevelLoaded; // levelName
    public event System.Action<int> OnAllLevelsCycled; // cycleCount
    public event System.Action<LevelFileEntry> OnLevelStarted; // current level info
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // Auto-find components if not assigned
        if (levelLoader == null)
            levelLoader = LevelLoader.instance;
        if (levelImporter == null)
            levelImporter = FindObjectOfType<HoudiniLevelImporter>();
            
        ValidateSetup();
    }
    
    private void ValidateSetup()
    {
        if (levelLoader == null)
        {
            Debug.LogError("[LevelSequencer] LevelLoader not found! Please assign or ensure LevelLoader exists in scene.");
        }
        
        if (levelImporter == null)
        {
            Debug.LogError("[LevelSequencer] HoudiniLevelImporter not found! Please assign or ensure it exists in scene.");
        }
        
        if (availableLevels.Count == 0)
        {
            Debug.LogWarning("[LevelSequencer] No levels assigned to sequence. Multi-level training disabled.");
            useMultiLevelSequence = false;
        }
    }
    
    //=========================================================================
    // LEVEL SEQUENCE MANAGEMENT
    //=========================================================================
    
    /// <summary>
    /// Initialize level sequence (typically called at training start)
    /// </summary>
    public void InitializeSequence(int startIndex = -1)
    {
        if (!useMultiLevelSequence || availableLevels.Count == 0)
        {
            Debug.LogWarning("[LevelSequencer] Multi-level sequence disabled or no levels available");
            return;
        }
        
        // Use provided start index or default
        if (startIndex >= 0)
        {
            currentLevelIndex = Mathf.Clamp(startIndex, 0, availableLevels.Count - 1);
        }
        else
        {
            currentLevelIndex = Mathf.Clamp(startLevelIndex, 0, availableLevels.Count - 1);
        }
        
        cyclesCompleted = 0;
        
        LoadCurrentLevel();
        
        Debug.Log($"[LevelSequencer] Initialized sequence starting at index {currentLevelIndex}");
    }

    /// <summary>
    /// Load next level in sequence (cycles back to start when reaching end)
    /// Called when player dies or level is completed unsuccessfully
    /// </summary>
    public void LoadNextLevel()
    {
        if (!useMultiLevelSequence || availableLevels.Count == 0)
        {
            Debug.LogWarning("[LevelSequencer] Multi-level sequence disabled or no levels available");
            return;
        }

        // Move to next level
        currentLevelIndex = (currentLevelIndex + 1) % availableLevels.Count;

        // Check if we completed a full cycle
        if (currentLevelIndex == 0)
        {
            cyclesCompleted++;
            Debug.Log($"[LevelSequencer] Completed level cycle #{cyclesCompleted} - starting over");
            OnAllLevelsCycled?.Invoke(cyclesCompleted);
        }

        LoadCurrentLevel();

        // Notify listeners
        OnLevelSequenceChanged?.Invoke(currentLevelIndex, availableLevels.Count);

        Debug.Log($"[LevelSequencer] Loaded next level: {currentLevelIndex + 1}/{availableLevels.Count}");
    }

    /// <summary>
    /// Load level by specific index in sequence
    /// </summary>
    public void LoadLevelByIndex(int index)
    {
        if (!useMultiLevelSequence || availableLevels.Count == 0)
        {
            Debug.LogWarning("[LevelSequencer] Multi-level sequence disabled or no levels available");
            return;
        }

        if (index < 0 || index >= availableLevels.Count)
        {
            Debug.LogError($"[LevelSequencer] Invalid level index: {index}");
            return;
        }

        currentLevelIndex = index;
        LoadCurrentLevel();

        OnLevelSequenceChanged?.Invoke(currentLevelIndex, availableLevels.Count);
        Debug.Log($"[LevelSequencer] Loaded level by index: {currentLevelIndex + 1}/{availableLevels.Count}");
    }

    /// <summary>
    /// Restart current level (useful for manual gameplay or debugging)
    /// </summary>
    public void RestartCurrentLevel()
    {
        if (!useMultiLevelSequence || availableLevels.Count == 0)
        {
            Debug.LogWarning("[LevelSequencer] Multi-level sequence disabled or no levels available");
            return;
        }

        LoadCurrentLevel();
        Debug.Log($"[LevelSequencer] Restarted current level: {currentLevelIndex + 1}/{availableLevels.Count}");
    }

    /// <summary>
    /// Load the current level using LevelLoader
    /// </summary>
    private void LoadCurrentLevel()
    {
        if (currentLevelIndex >= availableLevels.Count)
        {
            Debug.LogError($"[LevelSequencer] Current level index {currentLevelIndex} out of range");
            return;
        }

        var levelEntry = availableLevels[currentLevelIndex];

        if (levelEntry.textAsset == null)
        {
            Debug.LogError($"[LevelSequencer] Level {currentLevelIndex} has no TextAsset assigned");
            return;
        }

        // Load level data using HoudiniLevelImporter
        var levelData = levelImporter.LoadLevelData(levelEntry.textAsset);
        if (levelData != null)
        {
            // Use LevelLoader to actually load the level
            levelLoader.LoadLevelFromHoudiniData(levelData);

            // Fire events
            OnLevelLoaded?.Invoke(levelEntry.fileName);
            OnLevelStarted?.Invoke(levelEntry);

            Debug.Log($"[LevelSequencer] Successfully loaded level: {levelEntry.fileName}");
        }
        else
        {
            Debug.LogError($"[LevelSequencer] Failed to load level data from {levelEntry.fileName}");
        }
    }

    //=========================================================================
    // PUBLIC API FOR INTEGRATION
    //=========================================================================

    /// <summary>
    /// Get total number of levels in sequence
    /// </summary>
    public int GetTotalLevelsCount()
    {
        return availableLevels.Count;
    }

    /// <summary>
    /// Get current level index (0-based)
    /// </summary>
    public int GetCurrentLevelIndex()
    {
        return currentLevelIndex;
    }

    /// <summary>
    /// Get current level info for UI/debugging
    /// </summary>
    public string GetCurrentLevelInfo()
    {
        if (currentLevelIndex < availableLevels.Count)
        {
            var current = availableLevels[currentLevelIndex];
            return $"{current.fileName} ({currentLevelIndex + 1}/{availableLevels.Count})";
        }
        return "No level loaded";
    }

    /// <summary>
    /// Get current level entry
    /// </summary>
    public LevelFileEntry GetCurrentLevelEntry()
    {
        if (currentLevelIndex >= 0 && currentLevelIndex < availableLevels.Count)
        {
            return availableLevels[currentLevelIndex];
        }
        return default;
    }

    /// <summary>
    /// Get number of completed cycles
    /// </summary>
    public int GetCompletedCycles()
    {
        return cyclesCompleted;
    }

    /// <summary>
    /// Check if sequence is active
    /// </summary>
    public bool IsSequenceActive()
    {
        return useMultiLevelSequence && availableLevels.Count > 0;
    }

    //=========================================================================
    // LEVEL MANAGEMENT UTILITIES
    //=========================================================================

    /// <summary>
    /// Add level to sequence (runtime)
    /// </summary>
    public void AddLevelToSequence(LevelFileEntry levelEntry)
    {
        if (levelEntry.textAsset != null)
        {
            availableLevels.Add(levelEntry);
            Debug.Log($"[LevelSequencer] Added level to sequence: {levelEntry.fileName}");
        }
    }

    /// <summary>
    /// Remove level from sequence (runtime)
    /// </summary>
    public void RemoveLevelFromSequence(int index)
    {
        if (index >= 0 && index < availableLevels.Count)
        {
            var removed = availableLevels[index];
            availableLevels.RemoveAt(index);

            // Adjust current index if necessary
            if (currentLevelIndex >= index)
            {
                currentLevelIndex = Mathf.Max(0, currentLevelIndex - 1);
            }

            Debug.Log($"[LevelSequencer] Removed level from sequence: {removed.fileName}");
        }
    }

    /// <summary>
    /// Shuffle level sequence (for varied training)
    /// </summary>
    public void ShuffleLevelSequence()
    {
        if (availableLevels.Count > 1)
        {
            for (int i = 0; i < availableLevels.Count; i++)
            {
                var temp = availableLevels[i];
                int randomIndex = Random.Range(i, availableLevels.Count);
                availableLevels[i] = availableLevels[randomIndex];
                availableLevels[randomIndex] = temp;
            }

            currentLevelIndex = 0; // Reset to start
            Debug.Log("[LevelSequencer] Shuffled level sequence");
        }
    }

#if UNITY_EDITOR
    [Header("Editor Tools")]
    [SerializeField] private bool showDebugInfo = true;

    private void OnDrawGizmosSelected()
    {
        if (!showDebugInfo) return;

        // Draw sequence info in Scene view
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"Level Sequence: {currentLevelIndex + 1}/{availableLevels.Count}\\nCycles: {cyclesCompleted}"
        );
    }
#endif
}