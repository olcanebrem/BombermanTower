using UnityEngine;
using Unity.MLAgents;

public class EpsilonGreedyController : MonoBehaviour
{
    [Header("Epsilon-Greedy Settings")]
    [SerializeField] private float initialEpsilon = 1.0f;
    [SerializeField] private float finalEpsilon = 0.1f;
    [SerializeField] private int decaySteps = 10000;
    [SerializeField] private bool enableEpsilonDecay = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugEpsilon = true;
    
    private static EpsilonGreedyController _instance;
    public static EpsilonGreedyController Instance => _instance;
    
    private int totalSteps = 0;
    private float currentEpsilon;
    
    public float CurrentEpsilon => currentEpsilon;
    public int TotalSteps => totalSteps;
    
    private void Awake()
    {
        // Always keep the newest instance - this allows for level reloads
        if (_instance != null && _instance != this)
        {
            Debug.Log($"[EpsilonGreedy] Replacing old instance with new one on {gameObject.name}");
            Destroy(_instance.gameObject);
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeEpsilon();
    }
    
    private void InitializeEpsilon()
    {
        currentEpsilon = initialEpsilon;
        totalSteps = 0;
        
        if (debugEpsilon)
        {
            Debug.Log($"[EpsilonGreedy] Initialized - Initial: {initialEpsilon}, Final: {finalEpsilon}, Decay Steps: {decaySteps}");
        }
    }
    
    public void IncrementStep()
    {
        totalSteps++;
        
        if (enableEpsilonDecay && totalSteps <= decaySteps)
        {
            float decayProgress = (float)totalSteps / decaySteps;
            currentEpsilon = Mathf.Lerp(initialEpsilon, finalEpsilon, decayProgress);
            
            if (debugEpsilon && totalSteps % 1000 == 0)
            {
                Debug.Log($"[EpsilonGreedy] Step {totalSteps}: Epsilon = {currentEpsilon:F3} ({(1f - currentEpsilon) * 100f:F1}% Python)");
            }
        }
        else if (totalSteps > decaySteps)
        {
            currentEpsilon = finalEpsilon;
        }
    }
    
    public bool ShouldUseHeuristic()
    {
        if (!enableEpsilonDecay) return false;
        
        bool useHeuristic = Random.Range(0f, 1f) < currentEpsilon;
        
        if (debugEpsilon && totalSteps % 100 == 0)
        {
            string actionType = useHeuristic ? "🎲 Heuristic" : "🐍 Python";
            Debug.Log($"[EpsilonGreedy] Step {totalSteps}: {actionType} (ε={currentEpsilon:F3})");
        }
        
        return useHeuristic;
    }
    
    public void ResetEpsilon()
    {
        InitializeEpsilon();
        if (debugEpsilon)
        {
            Debug.Log("[EpsilonGreedy] Reset to initial values");
        }
    }
    
    [ContextMenu("Force Decay Test")]
    private void TestDecay()
    {
        for (int i = 0; i < 10; i++)
        {
            IncrementStep();
        }
    }
    
    private void OnValidate()
    {
        initialEpsilon = Mathf.Clamp01(initialEpsilon);
        finalEpsilon = Mathf.Clamp01(finalEpsilon);
        decaySteps = Mathf.Max(1, decaySteps);
        
        if (finalEpsilon > initialEpsilon)
        {
            finalEpsilon = initialEpsilon;
        }
    }
}