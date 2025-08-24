#!/usr/bin/env python3
"""
Legacy Bomberman Training Script - DEPRECATED
Use ML-Agents native training instead:
    mlagents-learn config/bomberman_ppo_config.yaml --run-id=bomberman_training

This file is kept for reference only.
"""

import sys
import argparse

def main():
    print("=" * 80)
    print("WARNING: DEPRECATED TRAINING SCRIPT - STABLE-BASELINES3")  
    print("=" * 80)
    print("This training script has been replaced with ML-Agents native training.")
    print()
    print("Use ML-Agents native training instead:")
    print("   mlagents-learn config/bomberman_ppo_config.yaml --run-id=bomberman_training")
    print()
    print("Or use Unity MLAgentsTrainingController for GUI control:")
    print("   - Add MLAgentsTrainingController to a GameObject in your scene")
    print("   - Configure paths and settings in Inspector")
    print("   - Click 'Start ML-Agents Training' in context menu")
    print()
    print("Training results will be parsed automatically and integrated")
    print("   with level files using mlagents_results_parser.py")
    print("=" * 80)
    
    # Exit to prevent accidental usage
    sys.exit(1)

# Legacy Stable-Baselines3 training code removed
# Use ML-Agents native training instead

if __name__ == "__main__":
    main()