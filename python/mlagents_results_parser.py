#!/usr/bin/env python3
"""
ML-Agents Training Results Parser
Parses ML-Agents TensorBoard logs and creates RLTrainingData for Unity level files
"""

import os
import sys
import json
import yaml
import argparse
from pathlib import Path
from datetime import datetime
import numpy as np
import pandas as pd
from typing import Dict, List, Optional

# TensorBoard log parsing
try:
    from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
    TENSORBOARD_AVAILABLE = True
except ImportError:
    print("WARNING: TensorBoard not available. Install with: pip install tensorboard")
    TENSORBOARD_AVAILABLE = False


class MLAgentsResultsParser:
    """Parse ML-Agents training results and convert to Unity RLTrainingData format."""
    
    def __init__(self, run_id: str, logs_dir: str = "results"):
        self.run_id = run_id
        self.logs_dir = Path(logs_dir)
        self.run_dir = self.logs_dir / run_id
        
        # Training data storage
        self.training_data = {}
        self.config_data = {}
        
        print(f"Parsing ML-Agents results for run: {run_id}")
        print(f"Log directory: {self.run_dir}")
    
    def parse_training_results(self) -> Dict:
        """Parse all training results and create RLTrainingData."""
        
        # 1. Parse configuration
        self._parse_config()
        
        # 2. Parse TensorBoard logs
        if TENSORBOARD_AVAILABLE:
            self._parse_tensorboard_logs()
        else:
            print("Skipping TensorBoard parsing - not available")
        
        # 3. Parse training summary
        self._parse_training_summary()
        
        # 4. Create RLTrainingData
        rl_data = self._create_rl_training_data()
        
        return rl_data
    
    def _parse_config(self):
        """Parse training configuration from YAML."""
        config_file = self.run_dir / "configuration.yaml"
        
        if config_file.exists():
            with open(config_file, 'r') as f:
                self.config_data = yaml.safe_load(f)
                print("[OK] Configuration parsed")
        else:
            print("[WARNING]  Configuration file not found")
    
    def _parse_tensorboard_logs(self):
        """Parse TensorBoard event files for training metrics."""
        tb_dir = self.run_dir / "PlayerAgent"  # Behavior name directory
        
        if not tb_dir.exists():
            print(f"[WARNING]  TensorBoard directory not found: {tb_dir}")
            return
        
        # Find event files
        event_files = list(tb_dir.glob("events.out.tfevents.*"))
        if not event_files:
            print("[WARNING]  No TensorBoard event files found")
            return
        
        # Parse latest event file
        latest_event_file = max(event_files, key=lambda x: x.stat().st_mtime)
        print(f"📊 Parsing TensorBoard logs: {latest_event_file.name}")
        
        try:
            event_acc = EventAccumulator(str(latest_event_file))
            event_acc.Reload()
            
            # Extract training metrics
            self._extract_training_metrics(event_acc)
            
        except Exception as e:
            print(f"[ERROR] Error parsing TensorBoard logs: {e}")
    
    def _extract_training_metrics(self, event_acc):
        """Extract key training metrics from TensorBoard events."""
        scalar_tags = event_acc.Tags()['scalars']
        
        metrics = {}
        
        # Key metrics to extract
        metric_mappings = {
            'Policy/Learning Rate': 'learning_rate',
            'Policy/Beta': 'entropy_coef', 
            'Policy/Epsilon': 'epsilon',
            'Environment/Cumulative Reward': 'cumulative_reward',
            'Environment/Episode Length': 'episode_length',
            'Losses/Policy Loss': 'policy_loss',
            'Losses/Value Loss': 'value_loss',
            'Policy/Entropy': 'entropy_loss',
            'Policy/KL Divergence': 'kl_divergence',
            'Policy/Learning Rate': 'learning_rate_final'
        }
        
        for tb_tag, metric_name in metric_mappings.items():
            if tb_tag in scalar_tags:
                try:
                    scalar_events = event_acc.Scalars(tb_tag)
                    if scalar_events:
                        # Get final value and some statistics
                        values = [event.value for event in scalar_events]
                        steps = [event.step for event in scalar_events]
                        
                        metrics[metric_name] = {
                            'final_value': values[-1] if values else 0.0,
                            'mean_value': np.mean(values) if values else 0.0,
                            'max_value': np.max(values) if values else 0.0,
                            'final_step': steps[-1] if steps else 0
                        }
                        
                except Exception as e:
                    print(f"[WARNING]  Error extracting {tb_tag}: {e}")
        
        self.training_data['tensorboard_metrics'] = metrics
        print(f"📈 Extracted {len(metrics)} metric types from TensorBoard")
    
    def _parse_training_summary(self):
        """Parse training summary and final statistics."""
        
        # Look for progress.csv (contains episode data)
        progress_file = self.run_dir / "PlayerAgent" / "PlayerAgent-0.csv"
        
        if progress_file.exists():
            try:
                df = pd.read_csv(progress_file)
                
                # Calculate summary statistics
                summary = {
                    'total_episodes': len(df),
                    'total_steps': df['Step'].iloc[-1] if not df.empty else 0,
                    'final_reward': df['Environment/Cumulative Reward'].iloc[-1] if 'Environment/Cumulative Reward' in df.columns else 0.0,
                    'mean_reward': df['Environment/Cumulative Reward'].mean() if 'Environment/Cumulative Reward' in df.columns else 0.0,
                    'max_reward': df['Environment/Cumulative Reward'].max() if 'Environment/Cumulative Reward' in df.columns else 0.0,
                    'mean_episode_length': df['Environment/Episode Length'].mean() if 'Environment/Episode Length' in df.columns else 0.0
                }
                
                # Calculate success rate (assuming reward > threshold indicates success)
                if 'Environment/Cumulative Reward' in df.columns:
                    success_threshold = 5.0  # Configurable
                    successful_episodes = (df['Environment/Cumulative Reward'] > success_threshold).sum()
                    summary['success_rate'] = (successful_episodes / len(df)) * 100.0
                else:
                    summary['success_rate'] = 0.0
                
                self.training_data['summary'] = summary
                print(f"📊 Training summary: {summary['total_episodes']} episodes, {summary['total_steps']} steps")
                
            except Exception as e:
                print(f"[ERROR] Error parsing progress CSV: {e}")
    
    def _create_rl_training_data(self) -> Dict:
        """Create RLTrainingData dictionary from parsed results."""
        
        # Get current timestamp
        training_date = datetime.now().isoformat() + "Z"
        
        # Extract config parameters
        config = self.config_data.get('behaviors', {}).get('PlayerAgent', {})
        hyperparams = config.get('hyperparameters', {})
        
        # Extract training results
        summary = self.training_data.get('summary', {})
        tb_metrics = self.training_data.get('tensorboard_metrics', {})
        
        # Create RLTrainingData structure
        rl_data = {
            'version': 1,  # Will be incremented by LevelTrainingManager
            'training_date': training_date,
            'training_note': f'ML-Agents training run: {self.run_id}',
            
            # Training parameters
            'seed': 42,  # ML-Agents doesn't expose this easily
            'learning_rate': hyperparams.get('learning_rate', 0.0003),
            'gamma': config.get('reward_signals', {}).get('extrinsic', {}).get('gamma', 0.99),
            'epsilon': hyperparams.get('epsilon', 0.2),
            'max_steps': config.get('max_steps', 0),
            
            # PPO hyperparameters
            'gae_lambda': hyperparams.get('lambd', 0.95),
            'entropy_coef': hyperparams.get('beta', 0.01),
            'vf_coef': 0.5,  # Not directly configurable in ML-Agents
            'batch_size': hyperparams.get('batch_size', 64),
            'n_steps': config.get('time_horizon', 2048),
            'n_epochs': hyperparams.get('num_epoch', 3),
            'max_grad_norm': 0.5,  # ML-Agents default
            'normalize_advantage': hyperparams.get('normalize_advantage', True),
            'clip_range_vf': -1.0,  # Not used in ML-Agents
            'target_kl': -1.0,  # Not used in ML-Agents
            
            # Training results
            'episodes': summary.get('total_episodes', 0),
            'avg_reward': summary.get('mean_reward', 0.0),
            'success_rate': summary.get('success_rate', 0.0),
            'deaths': 0,  # Would need custom tracking
            'collectibles_found': 0,  # Would need custom tracking  
            'total_collectibles': 0,  # Would need custom tracking
            'total_training_time': 0.0,  # Would need custom tracking
            
            # Advanced training metrics
            'final_loss': 0.0,
            'policy_loss': tb_metrics.get('policy_loss', {}).get('final_value', 0.0),
            'value_loss': tb_metrics.get('value_loss', {}).get('final_value', 0.0),
            'entropy_loss': tb_metrics.get('entropy_loss', {}).get('final_value', 0.0),
            'kl_divergence': tb_metrics.get('kl_divergence', {}).get('final_value', 0.0),
            'explained_variance': 0.0,  # Would need custom calculation
            'total_timesteps': summary.get('total_steps', 0),
            'fps': 0.0,  # Would need custom tracking
            'approx_kl': tb_metrics.get('kl_divergence', {}).get('mean_value', 0.0)
        }
        
        return rl_data
    
    def save_to_unity_format(self, rl_data: Dict, output_file: str):
        """Save RLTrainingData in Unity-compatible JSON format."""
        
        output_path = Path(output_file)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        # Create Unity-compatible export format
        export_data = {
            'levelName': 'TRAINING_RESULT',
            'exportDate': datetime.now().isoformat(),
            'trainingData': [rl_data]
        }
        
        with open(output_path, 'w') as f:
            json.dump(export_data, f, indent=2, default=str)
        
        print(f"💾 Training data saved to: {output_path}")
        return str(output_path)
    
    def integrate_with_unity_levels(self, rl_data: Dict, unity_levels_dir: str):
        """Integrate training results with Unity level files."""
        
        levels_dir = Path(unity_levels_dir)
        if not levels_dir.exists():
            print(f"[ERROR] Unity levels directory not found: {levels_dir}")
            return
        
        # Find level files
        level_files = list(levels_dir.glob("LEVEL_*.txt"))
        
        if not level_files:
            print("[WARNING]  No Unity level files found")
            return
        
        # For now, add to first level file found
        # In practice, you'd want to match to the specific level used in training
        target_level = level_files[0]
        
        print(f"🎯 Integrating results with level: {target_level.name}")
        
        try:
            # Read existing level file
            with open(target_level, 'r') as f:
                content = f.read()
            
            # Generate INI format training data
            ini_content = self._generate_ini_content(rl_data)
            
            # Append to level file
            with open(target_level, 'a') as f:
                f.write('\n' + ini_content)
            
            print(f"[OK] Training data added to {target_level.name}")
            
        except Exception as e:
            print(f"[ERROR] Error integrating with Unity level: {e}")
    
    def _generate_ini_content(self, rl_data: Dict) -> str:
        """Generate INI format content for Unity level file."""
        
        version = rl_data.get('version', 1)
        
        ini_lines = [
            f"# ===================================",
            f"# RL TRAINING DATA v{version}",
            f"# ===================================",
            f"",
            f"[training_params_v{version}]",
            f"version={version}",
            f"seed={rl_data.get('seed', 42)}",
            f"learning_rate={rl_data.get('learning_rate', 0.0003):.6f}",
            f"gamma={rl_data.get('gamma', 0.99):.3f}",
            f"epsilon={rl_data.get('epsilon', 0.2):.3f}",
            f"max_steps={rl_data.get('max_steps', 0)}",
            f"gae_lambda={rl_data.get('gae_lambda', 0.95):.3f}",
            f"entropy_coef={rl_data.get('entropy_coef', 0.01):.6f}",
            f"vf_coef={rl_data.get('vf_coef', 0.5):.3f}",
            f"batch_size={rl_data.get('batch_size', 64)}",
            f"n_steps={rl_data.get('n_steps', 2048)}",
            f"n_epochs={rl_data.get('n_epochs', 3)}",
            f"max_grad_norm={rl_data.get('max_grad_norm', 0.5):.3f}",
            f"normalize_advantage={str(rl_data.get('normalize_advantage', True)).lower()}",
            f"training_date={rl_data.get('training_date', '')}",
            f"training_note={rl_data.get('training_note', '')}",
            f"",
            f"[training_results_v{version}]",
            f"episodes={rl_data.get('episodes', 0)}",
            f"avg_reward={rl_data.get('avg_reward', 0.0):.6f}",
            f"success_rate={rl_data.get('success_rate', 0.0):.3f}",
            f"deaths={rl_data.get('deaths', 0)}",
            f"collectibles={rl_data.get('collectibles_found', 0)}/{rl_data.get('total_collectibles', 0)}",
            f"total_timesteps={rl_data.get('total_timesteps', 0)}",
            f"policy_loss={rl_data.get('policy_loss', 0.0):.6f}",
            f"value_loss={rl_data.get('value_loss', 0.0):.6f}",
            f"entropy_loss={rl_data.get('entropy_loss', 0.0):.6f}",
            f"kl_divergence={rl_data.get('kl_divergence', 0.0):.6f}",
            f"approx_kl={rl_data.get('approx_kl', 0.0):.6f}",
            f""
        ]
        
        return '\n'.join(ini_lines)


def main():
    parser = argparse.ArgumentParser(description="Parse ML-Agents training results")
    parser.add_argument("--run-id", required=True, help="ML-Agents run ID")
    parser.add_argument("--logs-dir", default="results", help="ML-Agents results directory")
    parser.add_argument("--output", help="Output JSON file path")
    parser.add_argument("--unity-levels", help="Unity levels directory for integration")
    
    args = parser.parse_args()
    
    # Create parser
    parser = MLAgentsResultsParser(args.run_id, args.logs_dir)
    
    # Parse results
    rl_data = parser.parse_training_results()
    
    # Save results
    if args.output:
        parser.save_to_unity_format(rl_data, args.output)
    
    # Integrate with Unity levels
    if args.unity_levels:
        parser.integrate_with_unity_levels(rl_data, args.unity_levels)
    
    print("🎉 Results parsing completed!")
    
    return rl_data


if __name__ == "__main__":
    main()