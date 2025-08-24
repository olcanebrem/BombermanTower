using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class RLTrainingData
{
    [Header("Training Metadata")]
    public int version = 1;
    public DateTime trainingDate = DateTime.Now;
    public string trainingNote = "";
    
    [Header("Training Parameters")]
    public int seed = 12345;
    public float learningRate = 0.0003f;
    public float gamma = 0.99f;
    public float epsilon = 0.2f;              // PPO clip_range
    public int maxSteps = 50000;
    
    [Header("PPO Hyperparameters")]
    public float gaeLambda = 0.95f;           // GAE lambda parameter
    public float entropyCoef = 0.01f;         // Entropy coefficient (ent_coef)
    public float vfCoef = 0.5f;               // Value function coefficient
    public int batchSize = 64;                // Batch size for training
    public int nSteps = 2048;                 // Steps per environment per update
    public int nEpochs = 10;                  // Epochs per update
    public float maxGradNorm = 0.5f;          // Maximum gradient norm
    public bool normalizeAdvantage = true;    // Normalize advantages
    public float clipRangeVf = -1f;           // VF clipping (-1 = same as epsilon)
    public float targetKl = -1f;              // Target KL divergence (-1 = disabled)
    
    [Header("Training Results")]
    public int episodes;
    public float avgReward;
    public float successRate;
    public int deaths;
    public int collectiblesFound;
    public int totalCollectibles;
    public float totalTrainingTime;
    
    [Header("Advanced Training Metrics")]
    public float finalLoss = 0f;              // Final training loss
    public float policyLoss = 0f;             // Policy loss
    public float valueLoss = 0f;              // Value function loss
    public float entropyLoss = 0f;            // Entropy loss
    public float klDivergence = 0f;           // KL divergence
    public float explainedVariance = 0f;      // Explained variance
    public int totalTimesteps = 0;            // Total training timesteps
    public float fps = 0f;                    // Training FPS
    public float approxKl = 0f;               // Approximate KL divergence
    
    public string CollectiblesRatio => $"{collectiblesFound}/{totalCollectibles}";
    public string VersionTag => $"v{version}";
    public float CollectiblesPercentage => totalCollectibles > 0 ? (float)collectiblesFound / totalCollectibles : 0f;
    
    public RLTrainingData()
    {
        trainingDate = DateTime.Now;
    }
    
    public RLTrainingData(int versionNumber)
    {
        version = versionNumber;
        trainingDate = DateTime.Now;
    }
    
    public override string ToString()
    {
        return $"RL Training {VersionTag} - Reward: {avgReward:F3}, Success: {successRate:F2}%, Episodes: {episodes}";
    }
}

public enum TrainingMetric
{
    AvgReward,
    SuccessRate,
    Episodes,
    CollectiblesRatio,
    Deaths,
    TrainingTime
}

[System.Serializable]
public class TrainingComparison
{
    public string levelName;
    public RLTrainingData bestReward;
    public RLTrainingData bestSuccessRate;
    public RLTrainingData fastestTraining;
    public RLTrainingData latestTraining;
    public List<RLTrainingData> allVersions;
    public int totalVersions;
    
    public TrainingComparison(string levelName)
    {
        this.levelName = levelName;
        allVersions = new List<RLTrainingData>();
    }
    
    public void UpdateComparison()
    {
        if (allVersions == null || allVersions.Count == 0) return;
        
        totalVersions = allVersions.Count;
        
        bestReward = allVersions[0];
        bestSuccessRate = allVersions[0];
        fastestTraining = allVersions[0];
        latestTraining = allVersions[0];
        
        foreach (var training in allVersions)
        {
            if (training.avgReward > bestReward.avgReward)
                bestReward = training;
                
            if (training.successRate > bestSuccessRate.successRate)
                bestSuccessRate = training;
                
            if (training.totalTrainingTime < fastestTraining.totalTrainingTime && training.totalTrainingTime > 0)
                fastestTraining = training;
                
            if (training.trainingDate > latestTraining.trainingDate)
                latestTraining = training;
        }
    }
}

[System.Serializable]
public class LevelDifficulty
{
    public string levelName;
    public float averageSuccessRate;
    public float averageDeaths;
    public float averageEpisodesToComplete;
    public float averageTrainingTime;
    public DifficultyRating rating;
    public int totalTrainingSessions;
    
    public LevelDifficulty(string levelName)
    {
        this.levelName = levelName;
    }
    
    public void CalculateDifficulty(List<RLTrainingData> trainingData)
    {
        if (trainingData == null || trainingData.Count == 0) return;
        
        totalTrainingSessions = trainingData.Count;
        averageSuccessRate = 0;
        averageDeaths = 0;
        averageEpisodesToComplete = 0;
        averageTrainingTime = 0;
        
        foreach (var training in trainingData)
        {
            averageSuccessRate += training.successRate;
            averageDeaths += training.deaths;
            averageEpisodesToComplete += training.episodes;
            averageTrainingTime += training.totalTrainingTime;
        }
        
        averageSuccessRate /= totalTrainingSessions;
        averageDeaths /= totalTrainingSessions;
        averageEpisodesToComplete /= totalTrainingSessions;
        averageTrainingTime /= totalTrainingSessions;
        
        rating = CalculateRating();
    }
    
    private DifficultyRating CalculateRating()
    {
        float difficultyScore = 0;
        
        difficultyScore += (100 - averageSuccessRate) / 25f;
        difficultyScore += averageDeaths / 50f;
        difficultyScore += averageEpisodesToComplete / 200f;
        
        difficultyScore /= 3f;
        
        if (difficultyScore < 1f) return DifficultyRating.Easy;
        if (difficultyScore < 2f) return DifficultyRating.Medium;
        if (difficultyScore < 3f) return DifficultyRating.Hard;
        return DifficultyRating.Expert;
    }
}

public enum DifficultyRating 
{ 
    Easy, 
    Medium, 
    Hard, 
    Expert 
}

[System.Serializable]
public class TrainingExportData
{
    public string levelName;
    public System.DateTime exportDate;
    public List<RLTrainingData> trainingData;
}

[System.Serializable]
public class AllLevelsExportData
{
    public System.DateTime exportDate;
    public int totalLevels;
    public Dictionary<string, List<RLTrainingData>> allLevelsData;
}