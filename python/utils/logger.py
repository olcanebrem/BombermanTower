"""
Logging utilities for Bomberman training
"""

import os
import sys
from pathlib import Path
import logging
from datetime import datetime

def setup_tensorboard_logging(log_dir: Path):
    """Setup TensorBoard logging directory and configuration."""
    log_dir = Path(log_dir)
    log_dir.mkdir(parents=True, exist_ok=True)
    
    # Create subdirectories
    (log_dir / "tensorboard").mkdir(exist_ok=True)
    (log_dir / "models").mkdir(exist_ok=True)
    (log_dir / "plots").mkdir(exist_ok=True)
    
    print(f"📊 TensorBoard logs will be saved to: {log_dir / 'tensorboard'}")
    print(f"💾 Models will be saved to: {log_dir / 'models'}")
    print(f"📈 Plots will be saved to: {log_dir / 'plots'}")
    
    # Return the tensorboard log directory
    return str(log_dir / "tensorboard")


def setup_console_logging(level: str = "INFO"):
    """Setup console logging with appropriate formatting."""
    numeric_level = getattr(logging, level.upper(), None)
    if not isinstance(numeric_level, int):
        raise ValueError(f'Invalid log level: {level}')
    
    # Create formatter
    formatter = logging.Formatter(
        fmt='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    
    # Setup console handler
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setFormatter(formatter)
    console_handler.setLevel(numeric_level)
    
    # Setup root logger
    root_logger = logging.getLogger()
    root_logger.setLevel(numeric_level)
    root_logger.addHandler(console_handler)
    
    return root_logger


def setup_file_logging(log_dir: Path, level: str = "DEBUG", filename: str = None):
    """Setup file logging with rotation."""
    log_dir = Path(log_dir)
    log_dir.mkdir(parents=True, exist_ok=True)
    
    if filename is None:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = f"bomberman_training_{timestamp}.log"
    
    log_file = log_dir / filename
    
    numeric_level = getattr(logging, level.upper(), None)
    if not isinstance(numeric_level, int):
        raise ValueError(f'Invalid log level: {level}')
    
    # Create formatter
    formatter = logging.Formatter(
        fmt='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    
    # Setup file handler
    file_handler = logging.FileHandler(log_file)
    file_handler.setFormatter(formatter)
    file_handler.setLevel(numeric_level)
    
    # Add to root logger
    root_logger = logging.getLogger()
    root_logger.addHandler(file_handler)
    
    print(f"📄 Log file: {log_file}")
    return str(log_file)


class TrainingLogger:
    """Custom logger for training metrics and progress."""
    
    def __init__(self, log_dir: Path, verbose: bool = True):
        self.log_dir = Path(log_dir)
        self.verbose = verbose
        
        # Setup logging
        self.logger = logging.getLogger("BombermanTrainer")
        
        # Create log files
        self.metrics_file = self.log_dir / "training_metrics.log"
        self.progress_file = self.log_dir / "training_progress.log"
        
    def log_training_start(self, config: dict):
        """Log training start with configuration."""
        message = f"🚀 Training started with config: {config}"
        if self.verbose:
            print(message)
        self.logger.info(message)
        
    def log_episode_complete(self, episode: int, reward: float, length: int, success: bool):
        """Log episode completion."""
        status = "SUCCESS" if success else "FAILED"
        message = f"Episode {episode}: Reward={reward:.3f}, Length={length}, Status={status}"
        
        if self.verbose and episode % 100 == 0:  # Log every 100 episodes to console
            print(message)
            
        self.logger.info(message)
        
        # Write to metrics file
        with open(self.metrics_file, 'a') as f:
            f.write(f"{episode},{reward:.6f},{length},{int(success)}\n")
    
    def log_training_progress(self, step: int, fps: float, mean_reward: float, success_rate: float):
        """Log training progress metrics."""
        message = f"Step {step}: FPS={fps:.1f}, Mean Reward={mean_reward:.3f}, Success Rate={success_rate:.1%}"
        
        if self.verbose:
            print(message)
            
        self.logger.info(message)
        
        # Write to progress file
        with open(self.progress_file, 'a') as f:
            f.write(f"{step},{fps:.3f},{mean_reward:.6f},{success_rate:.4f}\n")
    
    def log_curriculum_advance(self, old_level: str, new_level: str, success_rate: float):
        """Log curriculum advancement."""
        message = f"🎯 Curriculum advanced: {old_level} → {new_level} (Success: {success_rate:.1%})"
        
        if self.verbose:
            print(message)
            
        self.logger.info(message)
    
    def log_training_complete(self, total_episodes: int, total_steps: int, final_reward: float):
        """Log training completion."""
        message = f"✅ Training completed! Episodes: {total_episodes}, Steps: {total_steps}, Final Reward: {final_reward:.3f}"
        
        if self.verbose:
            print(message)
            
        self.logger.info(message)
    
    def log_model_saved(self, model_path: str):
        """Log model save."""
        message = f"💾 Model saved to: {model_path}"
        
        if self.verbose:
            print(message)
            
        self.logger.info(message)