using UnityEngine;

/// <summary>
/// Makes the RL_TRAINING_PARAMETERS GameObject persistent across scene loads
/// to prevent ML-Agent components (RewardSystem, EnvManager) from being destroyed
/// </summary>
public class PersistentMLTraining : MonoBehaviour
{
    void Awake()
    {
        // Make this GameObject persistent across scene loads
        DontDestroyOnLoad(this.gameObject);
        
        // Prevent duplicates if multiple instances exist
        PersistentMLTraining[] existingInstances = FindObjectsOfType<PersistentMLTraining>();
        if (existingInstances.Length > 1)
        {
            Debug.Log("[PersistentMLTraining] Duplicate instance found - destroying this one");
            Destroy(this.gameObject);
            return;
        }
        
        Debug.Log("[PersistentMLTraining] ML Training components made persistent across scene loads");
    }
    
    void OnDestroy()
    {
        Debug.Log("[PersistentMLTraining] ML Training GameObject destroyed");
    }
}