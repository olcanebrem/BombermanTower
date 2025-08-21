#!/usr/bin/env python3
"""
Bomberman PPO Model Evaluation Script
Evaluates trained PPO models and generates performance reports
"""

import os
import argparse
import numpy as np
import matplotlib.pyplot as plt
import seaborn as sns
from pathlib import Path
import json
from datetime import datetime
from typing import Dict, List, Tuple

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from gym_unity.envs import UnityToGymWrapper
from stable_baselines3 import PPO
from stable_baselines3.common.evaluation import evaluate_policy
from stable_baselines3.common.monitor import Monitor

from utils.wrappers import BombermanWrapper
import yaml

class BombermanEvaluator:
    """Evaluates trained Bomberman PPO models."""
    
    def __init__(self, config_path="configs/ppo_config.yaml"):
        """Initialize evaluator with configuration."""
        self.config = self.load_config(config_path)
        self.results_dir = Path("evaluation_results")
        self.results_dir.mkdir(exist_ok=True)
        
    def load_config(self, config_path):
        """Load configuration from YAML file."""
        with open(config_path, 'r') as file:
            config = yaml.safe_load(file)
        return config
    
    def create_env(self, no_graphics=False):
        """Create Unity environment for evaluation."""
        # Engine configuration
        engine_config = EngineConfigurationChannel()
        engine_config.set_configuration_parameters(
            time_scale=1.0,  # Normal speed for evaluation
            target_frame_rate=60,
            capture_frame_rate=60
        )
        
        # Unity environment
        unity_env = UnityEnvironment(
            file_name=self.config['unity']['env_path'],
            worker_id=0,
            no_graphics=no_graphics,
            side_channels=[engine_config]
        )
        
        # Wrap with Gym interface
        gym_env = UnityToGymWrapper(unity_env, uint8_visual=False, flatten_branched=True)
        
        # Apply custom wrapper
        wrapped_env = BombermanWrapper(gym_env, self.config)
        
        # Monitor for logging
        log_file = self.results_dir / "evaluation.monitor.csv"
        monitored_env = Monitor(wrapped_env, str(log_file))
        
        return monitored_env
    
    def evaluate_model(self, model_path: str, n_episodes: int = 100, 
                      render: bool = False) -> Dict:
        """Evaluate a trained model."""
        print(f"🧪 Evaluating model: {model_path}")
        print(f"📊 Running {n_episodes} episodes...")
        
        # Create environment
        env = self.create_env(no_graphics=not render)
        
        # Load model
        try:
            model = PPO.load(model_path)
            print("✅ Model loaded successfully")
        except Exception as e:
            print(f"❌ Error loading model: {e}")
            return {}
        
        # Standard evaluation
        mean_reward, std_reward = evaluate_policy(
            model, env, n_eval_episodes=n_episodes, 
            deterministic=True, return_episode_rewards=False
        )
        
        # Detailed evaluation
        detailed_results = self._detailed_evaluation(model, env, n_episodes, render)
        
        # Combine results
        results = {
            'model_path': model_path,
            'n_episodes': n_episodes,
            'mean_reward': mean_reward,
            'std_reward': std_reward,
            'evaluation_date': datetime.now().isoformat(),
            **detailed_results
        }
        
        env.close()
        return results
    
    def _detailed_evaluation(self, model, env, n_episodes: int, render: bool) -> Dict:
        """Perform detailed evaluation with episode-by-episode analysis."""
        episode_rewards = []
        episode_lengths = []
        success_episodes = []
        
        # Detailed statistics
        stats = {
            'enemies_killed': [],
            'collectibles_collected': [],
            'walls_destroyed': [],
            'bombs_placed': [],
            'damage_taken': [],
            'levels_completed': 0,
            'deaths': 0,
            'timeouts': 0
        }
        
        for episode in range(n_episodes):
            obs = env.reset()
            episode_reward = 0
            episode_length = 0
            done = False
            
            episode_stats = {
                'enemies_killed': 0,
                'collectibles_collected': 0,
                'walls_destroyed': 0,
                'bombs_placed': 0,
                'damage_taken': 0
            }
            
            while not done:
                action, _ = model.predict(obs, deterministic=True)
                obs, reward, done, info = env.step(action)
                episode_reward += reward
                episode_length += 1
                
                # Update episode statistics
                if 'episode' in info and done:
                    episode_stats = info['episode'].get('stats', {})
                
                if render and episode < 5:  # Only render first few episodes
                    env.render()
            
            # Record results
            episode_rewards.append(episode_reward)
            episode_lengths.append(episode_length)
            
            # Success criteria (customize based on your game)
            success = episode_stats.get('level_completed', False)
            success_episodes.append(success)
            
            # Update statistics
            for key in episode_stats:
                if key in stats:
                    stats[key].append(episode_stats[key])
            
            if episode_stats.get('level_completed', False):
                stats['levels_completed'] += 1
            elif info.get('timeout', False):
                stats['timeouts'] += 1
            else:
                stats['deaths'] += 1
            
            # Progress update
            if (episode + 1) % 20 == 0:
                print(f"Episode {episode + 1}/{n_episodes} completed")
        
        # Calculate summary statistics
        success_rate = np.mean(success_episodes)
        
        # Performance metrics
        performance_metrics = {
            'episode_rewards': episode_rewards,
            'episode_lengths': episode_lengths,
            'success_rate': success_rate,
            'mean_episode_length': np.mean(episode_lengths),
            'std_episode_length': np.std(episode_lengths),
            'completion_rate': stats['levels_completed'] / n_episodes,
            'death_rate': stats['deaths'] / n_episodes,
            'timeout_rate': stats['timeouts'] / n_episodes,
        }
        
        # Game-specific metrics
        game_metrics = {}
        for key in ['enemies_killed', 'collectibles_collected', 'walls_destroyed', 
                   'bombs_placed', 'damage_taken']:
            if stats[key]:
                game_metrics[f'mean_{key}'] = np.mean(stats[key])
                game_metrics[f'std_{key}'] = np.std(stats[key])
                game_metrics[f'total_{key}'] = np.sum(stats[key])
        
        return {
            'performance_metrics': performance_metrics,
            'game_metrics': game_metrics,
            'detailed_stats': stats
        }
    
    def compare_models(self, model_paths: List[str], n_episodes: int = 50) -> Dict:
        """Compare multiple models."""
        print(f"🔄 Comparing {len(model_paths)} models...")
        
        comparison_results = {}
        
        for i, model_path in enumerate(model_paths):
            print(f"\n📊 Evaluating model {i+1}/{len(model_paths)}: {Path(model_path).name}")
            results = self.evaluate_model(model_path, n_episodes, render=False)
            
            model_name = Path(model_path).stem
            comparison_results[model_name] = results
        
        # Create comparison summary
        summary = self._create_comparison_summary(comparison_results)
        
        return {
            'individual_results': comparison_results,
            'summary': summary
        }
    
    def _create_comparison_summary(self, results: Dict) -> Dict:
        """Create summary comparison of models."""
        summary = {
            'best_mean_reward': {'model': '', 'value': float('-inf')},
            'best_success_rate': {'model': '', 'value': 0},
            'most_stable': {'model': '', 'value': float('inf')},
            'fastest_completion': {'model': '', 'value': float('inf')}
        }
        
        for model_name, result in results.items():
            mean_reward = result.get('mean_reward', 0)
            success_rate = result.get('performance_metrics', {}).get('success_rate', 0)
            std_reward = result.get('std_reward', float('inf'))
            mean_length = result.get('performance_metrics', {}).get('mean_episode_length', float('inf'))
            
            # Best mean reward
            if mean_reward > summary['best_mean_reward']['value']:
                summary['best_mean_reward'] = {'model': model_name, 'value': mean_reward}
            
            # Best success rate
            if success_rate > summary['best_success_rate']['value']:
                summary['best_success_rate'] = {'model': model_name, 'value': success_rate}
            
            # Most stable (lowest std deviation)
            if std_reward < summary['most_stable']['value']:
                summary['most_stable'] = {'model': model_name, 'value': std_reward}
            
            # Fastest completion
            if mean_length < summary['fastest_completion']['value']:
                summary['fastest_completion'] = {'model': model_name, 'value': mean_length}
        
        return summary
    
    def generate_report(self, results: Dict, output_path: str = None):
        """Generate comprehensive evaluation report."""
        if output_path is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            output_path = self.results_dir / f"evaluation_report_{timestamp}.json"
        
        # Save JSON report
        with open(output_path, 'w') as f:
            json.dump(results, f, indent=2, default=str)
        
        print(f"📄 Report saved to: {output_path}")
        
        # Generate plots if matplotlib available
        try:
            self._generate_plots(results, output_path)
        except ImportError:
            print("⚠️ Matplotlib not available, skipping plot generation")
    
    def _generate_plots(self, results: Dict, base_path: str):
        """Generate evaluation plots."""
        plots_dir = Path(base_path).parent / "plots"
        plots_dir.mkdir(exist_ok=True)
        
        # Set style
        plt.style.use('seaborn-v0_8')
        sns.set_palette("husl")
        
        if 'individual_results' in results:
            # Multiple model comparison
            self._plot_model_comparison(results['individual_results'], plots_dir)
        else:
            # Single model analysis
            self._plot_single_model(results, plots_dir)
    
    def _plot_model_comparison(self, results: Dict, plots_dir: Path):
        """Generate comparison plots for multiple models."""
        model_names = list(results.keys())
        
        # Performance comparison
        fig, axes = plt.subplots(2, 2, figsize=(15, 10))
        fig.suptitle('Model Performance Comparison', fontsize=16)
        
        # Mean rewards
        mean_rewards = [results[name].get('mean_reward', 0) for name in model_names]
        std_rewards = [results[name].get('std_reward', 0) for name in model_names]
        
        axes[0, 0].bar(model_names, mean_rewards, yerr=std_rewards, capsize=5)
        axes[0, 0].set_title('Mean Episode Reward')
        axes[0, 0].set_ylabel('Reward')
        axes[0, 0].tick_params(axis='x', rotation=45)
        
        # Success rates
        success_rates = [results[name].get('performance_metrics', {}).get('success_rate', 0) 
                        for name in model_names]
        axes[0, 1].bar(model_names, success_rates)
        axes[0, 1].set_title('Success Rate')
        axes[0, 1].set_ylabel('Success Rate')
        axes[0, 1].set_ylim(0, 1)
        axes[0, 1].tick_params(axis='x', rotation=45)
        
        # Episode lengths
        mean_lengths = [results[name].get('performance_metrics', {}).get('mean_episode_length', 0) 
                       for name in model_names]
        axes[1, 0].bar(model_names, mean_lengths)
        axes[1, 0].set_title('Mean Episode Length')
        axes[1, 0].set_ylabel('Steps')
        axes[1, 0].tick_params(axis='x', rotation=45)
        
        # Game metrics (enemies killed)
        enemies_killed = [results[name].get('game_metrics', {}).get('mean_enemies_killed', 0) 
                         for name in model_names]
        axes[1, 1].bar(model_names, enemies_killed)
        axes[1, 1].set_title('Mean Enemies Killed per Episode')
        axes[1, 1].set_ylabel('Enemies')
        axes[1, 1].tick_params(axis='x', rotation=45)
        
        plt.tight_layout()
        plt.savefig(plots_dir / 'model_comparison.png', dpi=300, bbox_inches='tight')
        plt.close()
        
        print(f"📊 Comparison plots saved to: {plots_dir}")
    
    def _plot_single_model(self, results: Dict, plots_dir: Path):
        """Generate plots for single model evaluation."""
        performance_metrics = results.get('performance_metrics', {})
        episode_rewards = performance_metrics.get('episode_rewards', [])
        episode_lengths = performance_metrics.get('episode_lengths', [])
        
        if not episode_rewards:
            print("⚠️ No episode data available for plotting")
            return
        
        # Episode rewards over time
        fig, axes = plt.subplots(2, 2, figsize=(15, 10))
        fig.suptitle('Model Performance Analysis', fontsize=16)
        
        # Episode rewards
        axes[0, 0].plot(episode_rewards)
        axes[0, 0].set_title('Episode Rewards Over Time')
        axes[0, 0].set_xlabel('Episode')
        axes[0, 0].set_ylabel('Reward')
        axes[0, 0].grid(True, alpha=0.3)
        
        # Rolling average
        if len(episode_rewards) >= 20:
            rolling_mean = np.convolve(episode_rewards, np.ones(20)/20, mode='valid')
            axes[0, 0].plot(range(19, len(episode_rewards)), rolling_mean, 
                           color='red', label='20-episode average')
            axes[0, 0].legend()
        
        # Reward distribution
        axes[0, 1].hist(episode_rewards, bins=20, alpha=0.7, edgecolor='black')
        axes[0, 1].set_title('Reward Distribution')
        axes[0, 1].set_xlabel('Reward')
        axes[0, 1].set_ylabel('Frequency')
        axes[0, 1].axvline(np.mean(episode_rewards), color='red', linestyle='--', 
                          label=f'Mean: {np.mean(episode_rewards):.2f}')
        axes[0, 1].legend()
        
        # Episode lengths
        axes[1, 0].plot(episode_lengths)
        axes[1, 0].set_title('Episode Lengths Over Time')
        axes[1, 0].set_xlabel('Episode')
        axes[1, 0].set_ylabel('Steps')
        axes[1, 0].grid(True, alpha=0.3)
        
        # Success rate over time (moving window)
        if 'detailed_stats' in results:
            # This would need success/failure data per episode
            pass
        
        # Game statistics
        game_metrics = results.get('game_metrics', {})
        metric_names = []
        metric_values = []
        
        for key, value in game_metrics.items():
            if key.startswith('mean_') and not key.endswith('_std'):
                metric_names.append(key.replace('mean_', '').replace('_', ' ').title())
                metric_values.append(value)
        
        if metric_names:
            axes[1, 1].bar(metric_names, metric_values)
            axes[1, 1].set_title('Game Performance Metrics')
            axes[1, 1].set_ylabel('Average per Episode')
            axes[1, 1].tick_params(axis='x', rotation=45)
        
        plt.tight_layout()
        plt.savefig(plots_dir / 'single_model_analysis.png', dpi=300, bbox_inches='tight')
        plt.close()
        
        print(f"📊 Analysis plots saved to: {plots_dir}")
    
    def benchmark_model(self, model_path: str, difficulty_levels: List[str] = None):
        """Benchmark model across different difficulty levels."""
        if difficulty_levels is None:
            difficulty_levels = ['beginner', 'intermediate', 'advanced', 'expert']
        
        benchmark_results = {}
        
        for difficulty in difficulty_levels:
            print(f"🎯 Benchmarking on {difficulty} difficulty...")
            
            # Modify config for difficulty level
            original_config = self.config.copy()
            if difficulty in self.config.get('curriculum', {}).get('levels', {}):
                difficulty_config = self.config['curriculum']['levels'][difficulty]
                # Apply difficulty settings to environment
                # This would require Unity environment to support runtime difficulty changes
            
            # Evaluate on this difficulty
            results = self.evaluate_model(model_path, n_episodes=20, render=False)
            benchmark_results[difficulty] = results
            
            # Restore original config
            self.config = original_config
        
        return benchmark_results
    
    def interactive_evaluation(self, model_path: str):
        """Interactive evaluation with manual control override."""
        print("🎮 Starting interactive evaluation...")
        print("Controls: WASD for movement, SPACE for bomb, Q to quit, M to toggle AI")
        
        env = self.create_env(no_graphics=False)
        model = PPO.load(model_path)
        
        obs = env.reset()
        ai_mode = True
        
        try:
            while True:
                if ai_mode:
                    action, _ = model.predict(obs, deterministic=True)
                    print(f"AI Action: {action}")
                else:
                    # Manual control (would need keyboard input handling)
                    action = self._get_manual_action()
                
                obs, reward, done, info = env.step(action)
                env.render()
                
                print(f"Reward: {reward:.3f}, Total: {env.get_total_reward():.3f}")
                
                if done:
                    print("Episode ended!")
                    obs = env.reset()
                    
        except KeyboardInterrupt:
            print("\n👋 Interactive evaluation ended")
        finally:
            env.close()
    
    def _get_manual_action(self):
        """Get manual action from keyboard (placeholder)."""
        # This would require proper keyboard input handling
        # For now, return a random action
        return 0

def main():
    parser = argparse.ArgumentParser(description="Evaluate Bomberman PPO Models")
    parser.add_argument("--model", type=str, required=True,
                      help="Path to trained model")
    parser.add_argument("--config", type=str, default="configs/ppo_config.yaml",
                      help="Path to configuration file")
    parser.add_argument("--episodes", type=int, default=100,
                      help="Number of evaluation episodes")
    parser.add_argument("--render", action="store_true",
                      help="Render evaluation episodes")
    parser.add_argument("--compare", nargs="+", type=str,
                      help="Compare multiple models")
    parser.add_argument("--benchmark", action="store_true",
                      help="Run benchmark across difficulty levels")
    parser.add_argument("--interactive", action="store_true",
                      help="Interactive evaluation mode")
    parser.add_argument("--output", type=str,
                      help="Output path for results")
    
    args = parser.parse_args()
    
    # Create evaluator
    evaluator = BombermanEvaluator(args.config)
    
    if args.interactive:
        # Interactive mode
        evaluator.interactive_evaluation(args.model)
    elif args.compare:
        # Model comparison
        results = evaluator.compare_models(args.compare, args.episodes)
        evaluator.generate_report(results, args.output)
        
        # Print summary
        print("\n📊 Comparison Summary:")
        for metric, data in results['summary'].items():
            print(f"{metric.replace('_', ' ').title()}: {data['model']} ({data['value']:.3f})")
            
    elif args.benchmark:
        # Benchmarking
        results = evaluator.benchmark_model(args.model)
        evaluator.generate_report(results, args.output)
    else:
        # Single model evaluation
        results = evaluator.evaluate_model(args.model, args.episodes, args.render)
        evaluator.generate_report(results, args.output)
        
        # Print summary
        print(f"\n📊 Evaluation Summary:")
        print(f"Mean Reward: {results.get('mean_reward', 0):.3f} ± {results.get('std_reward', 0):.3f}")
        print(f"Success Rate: {results.get('performance_metrics', {}).get('success_rate', 0):.1%}")
        print(f"Mean Episode Length: {results.get('performance_metrics', {}).get('mean_episode_length', 0):.1f} steps")

if __name__ == "__main__":
    main()