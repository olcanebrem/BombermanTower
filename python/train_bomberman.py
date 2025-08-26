#!/usr/bin/env python3
"""
Bomberman Tower ML-Agents Training Script
Interactive Python script for training ML-Agents
"""

import os
import sys
import subprocess
import time
from datetime import datetime

def setup_environment():
    """Setup training environment"""
    # Set environment variable for protobuf compatibility
    os.environ['PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION'] = 'python'
    
    print("🚀 Bomberman Tower ML-Agents Training")
    print("="*50)
    print(f"📁 Current directory: {os.getcwd()}")
    print(f"🐍 Python version: {sys.version}")
    
    # Check ML-Agents installation
    try:
        import mlagents.trainers
        print("✅ ML-Agents found")
    except ImportError:
        print("❌ ML-Agents not found. Install with: pip install mlagents")
        return False
        
    return True

def get_training_config():
    """Get training configuration from user"""
    print("\n⚙️ Training Configuration")
    print("-" * 25)
    
    # Default values
    config_file = "config/bomberman_ppo_simple.yaml"
    run_id = f"bomberman_training_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    
    # Check if config file exists
    if os.path.exists(config_file):
        print(f"✅ Config file found: {config_file}")
    else:
        print(f"❌ Config file not found: {config_file}")
        return None, None
    
    print(f"🆔 Run ID: {run_id}")
    
    # Ask for custom settings
    print(f"\nPress Enter to use defaults, or type 'custom' for custom settings:")
    choice = input().strip().lower()
    
    if choice == 'custom':
        print(f"Current run ID: {run_id}")
        custom_run = input("Enter custom run ID (or press Enter to keep): ").strip()
        if custom_run:
            run_id = custom_run
    
    return config_file, run_id

def start_training(config_file, run_id):
    """Start ML-Agents training"""
    print(f"\n🚀 Starting Training")
    print("="*50)
    
    # Build command
    cmd = [
        "python", "-m", "mlagents.trainers.learn",
        config_file,
        f"--run-id={run_id}",
        "--force"
    ]
    
    print(f"📋 Training Details:")
    print(f"   Config: {config_file}")
    print(f"   Run ID: {run_id}")
    print(f"   Command: {' '.join(cmd)}")
    print(f"   Results: results/{run_id}/")
    print(f"")
    print(f"🎮 NOW GO TO UNITY AND PRESS PLAY!")
    print(f"   You should see: 'Connected to Unity environment'")
    print(f"")
    print(f"📊 Monitoring:")
    print(f"   - Training progress will show below")
    print(f"   - For graphs: tensorboard --logdir results")
    print(f"   - Browser: http://localhost:6006")
    print(f"")
    print(f"⛔ To stop: Press Ctrl+C")
    print("="*50)
    
    try:
        # Start training process
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            universal_newlines=True,
            bufsize=1
        )
        
        print(f"🟢 Training server started (PID: {process.pid})")
        print(f"⏳ Waiting for Unity connection...")
        print("-" * 50)
        
        # Stream output in real-time
        for line in process.stdout:
            print(line.rstrip())
            
        # Wait for process to complete
        return_code = process.wait()
        
        if return_code == 0:
            print(f"\n✅ Training completed successfully!")
            show_results(run_id)
        else:
            print(f"\n❌ Training failed with return code: {return_code}")
            
    except KeyboardInterrupt:
        print(f"\n⏹️ Training stopped by user")
        if process:
            process.terminate()
            print(f"🔄 Cleaning up...")
            process.wait()
    except Exception as e:
        print(f"\n❌ Error during training: {e}")

def show_results(run_id):
    """Show training results"""
    results_dir = f"results/{run_id}"
    
    if os.path.exists(results_dir):
        print(f"\n📊 Training Results")
        print(f"📁 Location: {results_dir}")
        
        # List key files
        files = []
        for root, dirs, filenames in os.walk(results_dir):
            for filename in filenames:
                if filename.endswith(('.onnx', '.pt', '.yaml')):
                    files.append(os.path.join(root, filename))
        
        if files:
            print(f"📄 Key files:")
            for file in files:
                print(f"   - {file}")
        else:
            print(f"⚠️ No model files found yet")
            
        print(f"\n💡 Next steps:")
        print(f"   - View graphs: tensorboard --logdir results")
        print(f"   - Test model: Load {run_id} in Unity ML-Agents")
    else:
        print(f"\n❌ No results directory found: {results_dir}")

def quick_commands():
    """Show quick training commands"""
    print(f"\n⚡ Quick Training Commands")
    print("-" * 30)
    timestamp = datetime.now().strftime('%H%M%S')
    
    commands = [
        ("Test (1K steps)", f"python train_bomberman.py --quick --steps=1000 --suffix=test_{timestamp}"),
        ("Short (5K steps)", f"python train_bomberman.py --quick --steps=5000 --suffix=short_{timestamp}"),
        ("Medium (10K steps)", f"python train_bomberman.py --quick --steps=10000 --suffix=medium_{timestamp}"),
        ("Custom config", f"python train_bomberman.py --config=your_config.yaml --run-id=your_run"),
    ]
    
    for desc, cmd in commands:
        print(f"{desc}:")
        print(f"   {cmd}")
    
    print(f"\n💾 Save this script as: train_bomberman.py")

def main():
    """Main function"""
    
    # Setup
    if not setup_environment():
        return
    
    # Handle command line arguments
    if len(sys.argv) > 1 and sys.argv[1] == "--help":
        quick_commands()
        return
    
    # Get configuration
    config_file, run_id = get_training_config()
    if not config_file:
        return
    
    # Confirm start
    print(f"\n🔄 Ready to start training!")
    input("Press Enter to continue (or Ctrl+C to cancel)...")
    
    # Start training
    start_training(config_file, run_id)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print(f"\n👋 Training cancelled by user")
    except Exception as e:
        print(f"\n💥 Unexpected error: {e}")
        sys.exit(1)