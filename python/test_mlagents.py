#!/usr/bin/env python3
"""
Test script to verify ML-Agents training pipeline functionality
"""

import os
import sys
from pathlib import Path

def test_mlagents_import():
    """Test if ML-Agents can be imported correctly with protobuf fix."""
    
    # Set protobuf environment variable
    os.environ["PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION"] = "python"
    
    try:
        print("Testing ML-Agents imports...")
        
        # Test basic imports
        import mlagents
        try:
            version = mlagents.__version__
        except AttributeError:
            # Fallback for different ML-Agents versions
            version = "installed"
        print(f"ML-Agents version: {version}")
        
        # Test trainers import
        from mlagents.trainers.learn import main as mlagents_main
        print("ML-Agents trainers imported successfully")
        
        # Test environment import
        from mlagents_envs.environment import UnityEnvironment
        print("Unity environment imported successfully")
        
        print("\nAll ML-Agents imports successful!")
        return True
        
    except Exception as e:
        print(f"ML-Agents import failed: {e}")
        return False

def test_config_validation():
    """Test if the training configuration is valid."""
    
    try:
        import yaml
        
        config_path = Path("config/bomberman_ppo_config.yaml")
        if not config_path.exists():
            print(f"Config file not found: {config_path}")
            return False
        
        with open(config_path, 'r') as f:
            config = yaml.safe_load(f)
        
        # Validate required sections
        if 'behaviors' not in config:
            print("Config missing 'behaviors' section")
            return False
            
        if 'PlayerAgent' not in config['behaviors']:
            print("Config missing 'PlayerAgent' behavior")
            return False
            
        player_config = config['behaviors']['PlayerAgent']
        if player_config.get('trainer_type') != 'ppo':
            print("PlayerAgent trainer_type is not 'ppo'")
            return False
        
        print("Training configuration is valid")
        return True
        
    except Exception as e:
        print(f"Config validation failed: {e}")
        return False

def test_results_parser():
    """Test if the results parser script is functional."""
    
    try:
        # Import the parser
        from mlagents_results_parser import MLAgentsResultsParser
        
        print("Results parser imported successfully")
        
        # Test parser instantiation
        parser = MLAgentsResultsParser("test_run", "results")
        print("Results parser instantiated successfully")
        
        return True
        
    except Exception as e:
        print(f"Results parser test failed: {e}")
        return False

def main():
    print("Testing ML-Agents Training Pipeline")
    print("=" * 50)
    
    tests = [
        ("ML-Agents Import", test_mlagents_import),
        ("Config Validation", test_config_validation), 
        ("Results Parser", test_results_parser)
    ]
    
    results = []
    for test_name, test_func in tests:
        print(f"\nRunning: {test_name}")
        print("-" * 30)
        result = test_func()
        results.append((test_name, result))
    
    # Summary
    print("\n" + "=" * 50)
    print("TEST SUMMARY")
    print("=" * 50)
    
    passed = 0
    for test_name, result in results:
        status = "PASS" if result else "FAIL" 
        print(f"{status}: {test_name}")
        if result:
            passed += 1
    
    print(f"\nTests passed: {passed}/{len(tests)}")
    
    if passed == len(tests):
        print("\nAll tests passed! ML-Agents pipeline is ready.")
        print("\nTo start training:")
        print("1. Open Unity and ensure PlayerAgent behavior name is 'PlayerAgent'")
        print("2. Add MLAgentsTrainingController to a GameObject")
        print("3. Run: mlagents-learn config/bomberman_ppo_config.yaml --run-id=test")
        print("   Or use Unity GUI training controller")
    else:
        print(f"\n{len(tests) - passed} tests failed. Please fix issues before training.")
        
    return passed == len(tests)

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)