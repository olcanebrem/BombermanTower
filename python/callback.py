"""
Custom callbacks for Bomberman PPO training
"""

import os
import numpy as np
from pathlib import Path
from typing import Dict, Any
import json
from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.logger import Figure
import matplotlib.pyplot as plt
import seaborn as sns

class BombermanCallback(BaseCallback):
    """
    Custom callback for monitoring Bomberman-specific metrics during training.
    """
    
    def __init__(self, log_dir: Path, config: Dict[str, Any], verbose: int = 1):
        super().__init__(verbose)
        self.log_dir = Path(log_dir)
        self.config = config
        
        # Episode tracking
        self.episode_rewards = []
        self.episode_lengths = []
        self.episode_stats = []
        
        # Performance tracking
        self.success_rates = []
        self.enemy_kill_rates = []
        self.collectible_rates = []
        
        # Training metrics
        self.training_step = 0
        self.last_log_step = 0
        self.log_frequency = config.get('logging', {}).get('log_frequency', 10000)
        
        # Create directories
        (self.log_dir / "plots").mkdir(parents=True, exist_ok=True)
        (self.log_dir / "metrics").mkdir(parents=True, exist_ok=True)
    
    def _on_training_start(self) -> None:
        """Called before the first rollout starts."""
        self.logger.record("train/initial_learning_rate", self.model.learning_rate)
        if self.verbose > 0:
            print("🚀 Bomberman PPO training started!")
    
    def _on_rollout_start(self) -> None:
        """Called at the start of a rollout."""
        pass
    
    def _on_step(self) -> bool:
        """Called at each step of the training."""
        self.training_step += 1
        
        # Log periodic updates
        if self.training_step - self.last_log_step >= self.log_frequency:
            self._log_training_metrics()
            self.last_log_step = self.training_step
        
        return True
    
    def _on_rollout_end(self) -> None:
        """Called at the end of a rollout."""
        # Extract episode information from rollout buffer
        if hasattr(self.locals, 'infos'):
            self._process_episode_info(self.locals['infos'])
        
        # Log rollout metrics
        self._log_rollout_metrics()
    
    def _process_episode_info(self, infos):
        """Process episode information from environment."""
        for info in infos:
            if 'episode' in info:
                episode_data = info['episode']
                
                # Basic episode metrics
                self.episode_rewards.append(episode_data.get('reward', 0))
                self.episode_lengths.append(episode_data.get('length', 0))
                
                # Game-specific statistics
                stats = episode_data.get('stats', {})
                self.episode_stats.append(stats)
                
                # Calculate derived metrics
                success = stats.get('level_completed', False)
                enemies_killed = stats.get('enemies_killed', 0)
                collectibles_collected = stats.get('collectibles_collected', 0)
                
                self.success_rates.append(1.0 if success else 0.0)
                self.enemy_kill_rates.append(enemies_killed)
                self.collectible_rates.append(collectibles_collected)
    
    def _log_training_metrics(self):
        """Log training-specific metrics."""
        if len(self.episode_rewards) > 0:
            # Recent performance (last 100 episodes)
            recent_rewards = self.episode_rewards[-100:]
            recent_success = self.success_rates[-100:]
            recent_enemies = self.enemy_kill_rates[-100:]
            recent_collectibles = self.collectible_rates[-100:]
            
            # Log to tensorboard
            self.logger.record("episode/mean_reward_100", np.mean(recent_rewards))
            self.logger.record("episode/std_reward_100", np.std(recent_rewards))
            self.logger.record("episode/success_rate_100", np.mean(recent_success))
            self.logger.record("episode/mean_enemies_killed_100", np.mean(recent_enemies))
            self.logger.record("episode/mean_collectibles_100", np.mean(recent_collectibles))
            
            # Overall performance
            self.logger.record("episode/mean_reward_total", np.mean(self.episode_rewards))
            self.logger.record("episode/success_rate_total", np.mean(self.success_rates))
            
            # Training progress
            self.logger.record("train/episodes_completed", len(self.episode_rewards))
            
            if self.verbose > 0:
                print(f"Step {self.training_step}: "
                      f"Mean Reward: {np.mean(recent_rewards):.2f}, "
                      f"Success Rate: {np.mean(recent_success):.1%}")
    
    def _log_rollout_metrics(self):
        """Log rollout-specific metrics."""
        if hasattr(self.model, 'ep_info_buffer') and len(self.model.ep_info_buffer) > 0:
            # Extract episode info from SB3's buffer
            ep_info = self.model.ep_info_buffer
            
            if len(ep_info) > 0:
                ep_rewards = [ep['r'] for ep in ep_info]
                ep_lengths = [ep['l'] for ep in ep_info]
                
                self.logger.record("rollout/ep_rew_mean", np.mean(ep_rewards))
                self.logger.record("rollout/ep_len_mean", np.mean(ep_lengths))
    
    def _on_training_end(self) -> None:
        """Called at the end of training."""
        if self.verbose > 0:
            print("✅ Bomberman PPO training completed!")
        
        # Generate final report
        self._generate_training_report()
        
        # Save training metrics
        self._save_training_data()
    
    def _generate_training_report(self):
        """Generate comprehensive training report."""
        if len(self.episode_rewards) == 0:
            return
        
        # Create training plots
        try:
            self._create_training_plots()
        except Exception as e:
            print(f"⚠️ Could not generate plots: {e}")
        
        # Create text report
        report = {
            'training_summary': {
                'total_episodes': len(self.episode_rewards),
                'total_steps': self.training_step,
                'final_success_rate': np.mean(self.success_rates[-100:]) if len(self.success_rates) >= 100 else np.mean(self.success_rates),
                'final_mean_reward': np.mean(self.episode_rewards[-100:]) if len(self.episode_rewards) >= 100 else np.mean(self.episode_rewards),
                'best_episode_reward': np.max(self.episode_rewards),
                'mean_episode_length': np.mean(self.episode_lengths)
            },
            'game_statistics': {
                'mean_enemies_killed': np.mean(self.enemy_kill_rates),
                'mean_collectibles_collected': np.mean(self.collectible_rates),
                'total_successful_episodes': np.sum(self.success_rates)
            }
        }
        
        # Save report
        report_path = self.log_dir / "metrics" / "training_report.json"
        with open(report_path, 'w') as f:
            json.dump(report, f, indent=2, default=str)
        
        print(f"📄 Training report saved to: {report_path}")
    
    def _create_training_plots(self):
        """Create training visualization plots."""
        plt.style.use('seaborn-v0_8')
        
        # Training progress plot
        fig, axes = plt.subplots(2, 2, figsize=(15, 10))
        fig.suptitle('Bomberman PPO Training Progress', fontsize=16)
        
        # Episode rewards
        axes[0, 0].plot(self.episode_rewards, alpha=0.6, label='Episode Reward')
        if len(self.episode_rewards) >= 20:
            rolling_mean = np.convolve(self.episode_rewards, np.ones(20)/20, mode='valid')
            axes[0, 0].plot(range(19, len(self.episode_rewards)), rolling_mean, 
                           color='red', linewidth=2, label='20-episode average')
        axes[0, 0].set_title('Episode Rewards Over Time')
        axes[0, 0].set_xlabel('Episode')
        axes[0, 0].set_ylabel('Reward')
        axes[0, 0].legend()
        axes[0, 0].grid(True, alpha=0.3)
        
        # Success rate
        if len(self.success_rates) >= 50:
            success_rolling = np.convolve(self.success_rates, np.ones(50)/50, mode='valid')
            axes[0, 1].plot(range(49, len(self.success_rates)), success_rolling, 
                           color='green', linewidth=2)
        axes[0, 1].set_title('Success Rate (50-episode average)')
        axes[0, 1].set_xlabel('Episode')
        axes[0, 1].set_ylabel('Success Rate')
        axes[0, 1].set_ylim(0, 1)
        axes[0, 1].grid(True, alpha=0.3)
        
        # Episode lengths
        axes[1, 0].plot(self.episode_lengths, alpha=0.6, color='orange')
        if len(self.episode_lengths) >= 20:
            length_rolling = np.convolve(self.episode_lengths, np.ones(20)/20, mode='valid')
            axes[1, 0].plot(range(19, len(self.episode_lengths)), length_rolling, 
                           color='darkorange', linewidth=2)
        axes[1, 0].set_title('Episode Lengths Over Time')
        axes[1, 0].set_xlabel('Episode')
        axes[1, 0].set_ylabel('Steps')
        axes[1, 0].grid(True, alpha=0.3)
        
        # Game performance metrics
        if len(self.enemy_kill_rates) >= 20:
            enemy_rolling = np.convolve(self.enemy_kill_rates, np.ones(20)/20, mode='valid')
            axes[1, 1].plot(range(19, len(self.enemy_kill_rates)), enemy_rolling, 
                           color='red', linewidth=2, label='Enemies Killed')
        
        if len(self.collectible_rates) >= 20:
            collectible_rolling = np.convolve(self.collectible_rates, np.ones(20)/20, mode='valid')
            axes[1, 1].plot(range(19, len(self.collectible_rates)), collectible_rolling, 
                           color='blue', linewidth=2, label='Collectibles')
        
        axes[1, 1].set_title('Game Performance (20-episode average)')
        axes[1, 1].set_xlabel('Episode')
        axes[1, 1].set_ylabel('Count per Episode')
        axes[1, 1].legend()
        axes[1, 1].grid(True, alpha=0.3)
        
        plt.tight_layout()
        plot_path = self.log_dir / "plots" / "training_progress.png"
        plt.savefig(plot_path, dpi=300, bbox_inches='tight')
        plt.close()
        
        print(f"📊 Training plots saved to: {plot_path}")
    
    def _save_training_data(self):
        """Save training data for later analysis."""
        training_data = {
            'episode_rewards': self.episode_rewards,
            'episode_lengths': self.episode_lengths,
            'episode_stats': self.episode_stats,
            'success_rates': self.success_rates,
            'enemy_kill_rates': self.enemy_kill_rates,
            'collectible_rates': self.collectible_rates
        }
        
        data_path = self.log_dir / "metrics" / "training_data.json"
        with open(data_path, 'w') as f:
            json.dump(training_data, f, indent=2, default=str)


class CurriculumCallback(BaseCallback):
    """
    Callback for curriculum learning - adjusts difficulty based on performance.
    """
    
    def __init__(self, config: Dict[str, Any], verbose: int = 1):
        super().__init__(verbose)
        self.curriculum_config = config
        
        # Current curriculum level
        self.current_level = 'beginner'
        self.levels = list(config['levels'].keys())
        
        # Performance tracking
        self.episode_results = []
        self.evaluation_window = config.get('evaluation_window', 100)
        self.update_frequency = config.get('update_frequency', 25000)
        self.last_update_step = 0
        
        if self.verbose > 0:
            print(f"📚 Curriculum learning enabled. Starting at '{self.current_level}' level")
    
    def _on_step(self) -> bool:
        """Check curriculum progression at specified intervals."""
        if self.num_timesteps - self.last_update_step >= self.update_frequency:
            self._check_curriculum_progression()
            self.last_update_step = self.num_timesteps
        
        return True
    
    def _on_rollout_end(self) -> None:
        """Track episode performance for curriculum evaluation."""
        if hasattr(self.locals, 'infos'):
            for info in self.locals['infos']:
                if 'episode' in info:
                    episode_data = info['episode']
                    stats = episode_data.get('stats', {})
                    success = stats.get('level_completed', False)
                    self.episode_results.append(success)
    
    def _check_curriculum_progression(self):
        """Check if agent should progress to next difficulty level."""
        if len(self.episode_results) < self.evaluation_window:
            return
        
        # Calculate recent success rate
        recent_results = self.episode_results[-self.evaluation_window:]
        success_rate = np.mean(recent_results)
        
        # Check progression criteria
        current_level_config = self.curriculum_config['levels'][self.current_level]
        min_success_rate = current_level_config['min_success_rate']
        
        # Try to advance to next level
        current_index = self.levels.index(self.current_level)
        if current_index < len(self.levels) - 1:  # Not at highest level
            next_level = self.levels[current_index + 1]
            next_level_config = self.curriculum_config['levels'][next_level]
            next_min_success = next_level_config['min_success_rate']
            
            if success_rate >= next_min_success:
                self._advance_curriculum_level(next_level, success_rate)
        
        # Log current performance
        self.logger.record("curriculum/current_level", current_index)
        self.logger.record("curriculum/success_rate", success_rate)
        self.logger.record("curriculum/required_success_rate", min_success_rate)
        
        if self.verbose > 0:
            print(f"📊 Curriculum check - Level: {self.current_level}, "
                  f"Success Rate: {success_rate:.1%} (Required: {min_success_rate:.1%})")
    
    def _advance_curriculum_level(self, new_level: str, success_rate: float):
        """Advance to the next curriculum level."""
        old_level = self.current_level
        self.current_level = new_level
        
        # Clear episode results for fresh evaluation
        self.episode_results = []
        
        # Log advancement
        self.logger.record("curriculum/level_advancement", 1)
        
        if self.verbose > 0:
            print(f"🎯 Curriculum advanced! {old_level} → {new_level} "
                  f"(Success rate: {success_rate:.1%})")
        
        # Apply new level settings to environment
        self._apply_curriculum_settings(new_level)
    
    def _apply_curriculum_settings(self, level: str):
        """Apply curriculum level settings to the environment."""
        level_config = self.curriculum_config['levels'][level]
        
        # This would require environment support for runtime difficulty changes
        # For now, we log the intended changes
        if self.verbose > 0:
            print(f"📝 Applying curriculum level: {level}")