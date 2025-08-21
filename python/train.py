#!/usr/bin/env python3
"""
Bomberman PPO Training Script
Trains a PPO agent to play Bomberman using Unity ML-Agents and Stable-Baselines3
"""

import os
import sys
import argparse
import numpy as np
import yaml
from datetime import datetime
from pathlib import Path

# ML-Agents and Stable-Baselines3 imports
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from gym_unity.envs import UnityToGymWrapper
from stable_baselines3 import PPO
from stable_baselines3.common.vec_env import DummyVecEnv, SubprocVecEnv
from stable_baselines3.common.callbacks import EvalCallback, CheckpointCallback
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.utils import set_random_seed

# Custom imports
from utils.callbacks import BombermanCallback, CurriculumCallback
from utils.wrappers import BombermanWrapper
from utils.logger import setup_tensorboard_logging

class BombermanTrainer:
    def __init__(self, config_path="configs/ppo_config.yaml"):
        """Initialize the Bomberman trainer with configuration."""
        self.config = self.load_config(config_path)
        self.setup_directories()
        
    def load_config(self, config_path):
        """Load training configuration from YAML file."""
        with open(config_path, 'r') as file:
            config = yaml.safe_load(file)
        return config
    
    def setup_directories(self):
        """Create necessary directories for training."""
        self.model_dir = Path(self.config['paths']['model_dir'])
        self.log_dir = Path(self.config['paths']['log_dir'])
        self.checkpoint_dir = Path(self.config['paths']['checkpoint_dir'])
        
        for directory in [self.model_dir, self.log_dir, self.checkpoint_dir]:
            directory.mkdir(parents=True, exist_ok=True)
    
    def create_unity_env(self, env_id=0, no_graphics=True):
        """Create Unity environment instance."""
        # Engine configuration for performance
        engine_config = EngineConfigurationChannel()
        engine_config.set_configuration_parameters(
            time_scale=self.config['unity']['time_scale'],
            target_frame_rate=60,
            capture_frame_rate=60
        )
        
        # Unity environment setup
        unity_env = UnityEnvironment(
            file_name=self.config['unity']['env_path'],
            worker_id=env_id,
            no_graphics=no_graphics,
            side_channels=[engine_config]
        )
        
        return unity_env
    
    def create_env(self, env_id=0, rank=0):
        """Create wrapped environment for training."""
        def _init():
            # Set random seed for reproducibility
            set_random_seed(rank)
            
            # Create Unity environment
            unity_env = self.create_unity_env(env_id=env_id, 
                                            no_graphics=self.config['training']['no_graphics'])
            
            # Wrap with Gym interface
            gym_env = UnityToGymWrapper(unity_env, 
                                      uint8_visual=False,
                                      flatten_branched=True)
            
            # Apply custom wrapper
            wrapped_env = BombermanWrapper(gym_env, self.config)
            
            # Monitor for logging
            log_file = self.log_dir / f"env_{env_id}.monitor.csv"
            monitored_env = Monitor(wrapped_env, str(log_file))
            
            return monitored_env
        
        return _init
    
    def create_vectorized_env(self, n_envs=1):
        """Create vectorized environment for parallel training."""
        if n_envs == 1:
            env = DummyVecEnv([self.create_env(env_id=0, rank=0)])
        else:
            env = SubprocVecEnv([self.create_env(env_id=i, rank=i) 
                               for i in range(n_envs)])
        return env
    
    def create_model(self, env):
        """Create PPO model with specified configuration."""
        ppo_config = self.config['ppo']
        
        # PPO hyperparameters
        model = PPO(
            policy="MlpPolicy",
            env=env,
            learning_rate=ppo_config['learning_rate'],
            n_steps=ppo_config['n_steps'],
            batch_size=ppo_config['batch_size'],
            n_epochs=ppo_config['n_epochs'],
            gamma=ppo_config['gamma'],
            gae_lambda=ppo_config['gae_lambda'],
            clip_range=ppo_config['clip_range'],
            clip_range_vf=ppo_config['clip_range_vf'],
            normalize_advantage=ppo_config['normalize_advantage'],
            ent_coef=ppo_config['ent_coef'],
            vf_coef=ppo_config['vf_coef'],
            max_grad_norm=ppo_config['max_grad_norm'],
            use_sde=ppo_config['use_sde'],
            sde_sample_freq=ppo_config['sde_sample_freq'],
            target_kl=ppo_config['target_kl'],
            tensorboard_log=str(self.log_dir),
            verbose=1,
            device=self.config['training']['device']
        )
        
        return model
    
    def setup_callbacks(self, env):
        """Setup training callbacks."""
        callbacks = []
        
        # Evaluation callback
        if self.config['evaluation']['enabled']:
            eval_env = self.create_vectorized_env(n_envs=1)
            eval_callback = EvalCallback(
                eval_env,
                best_model_save_path=str(self.model_dir),
                log_path=str(self.log_dir),
                eval_freq=self.config['evaluation']['eval_freq'],
                n_eval_episodes=self.config['evaluation']['n_eval_episodes'],
                deterministic=True,
                render=False
            )
            callbacks.append(eval_callback)
        
        # Checkpoint callback
        if self.config['checkpoints']['enabled']:
            checkpoint_callback = CheckpointCallback(
                save_freq=self.config['checkpoints']['save_freq'],
                save_path=str(self.checkpoint_dir),
                name_prefix="bomberman_ppo"
            )
            callbacks.append(checkpoint_callback)
        
        # Custom Bomberman callback for monitoring
        bomberman_callback = BombermanCallback(
            log_dir=self.log_dir,
            config=self.config
        )
        callbacks.append(bomberman_callback)
        
        # Curriculum learning callback
        if self.config['curriculum']['enabled']:
            curriculum_callback = CurriculumCallback(
                config=self.config['curriculum']
            )
            callbacks.append(curriculum_callback)
        
        return callbacks
    
    def train(self, resume_from=None):
        """Main training loop."""
        print("🎮 Starting Bomberman PPO Training...")
        print(f"📊 Logs will be saved to: {self.log_dir}")
        print(f"💾 Models will be saved to: {self.model_dir}")
        
        # Create environment
        n_envs = self.config['training']['n_envs']
        env = self.create_vectorized_env(n_envs=n_envs)
        
        # Create or load model
        if resume_from:
            print(f"📁 Loading model from: {resume_from}")
            model = PPO.load(resume_from, env=env)
        else:
            print("🆕 Creating new PPO model...")
            model = self.create_model(env)
        
        # Setup callbacks
        callbacks = self.setup_callbacks(env)
        
        # Setup tensorboard logging
        setup_tensorboard_logging(self.log_dir)
        
        try:
            # Start training
            total_timesteps = self.config['training']['total_timesteps']
            print(f"🚀 Training for {total_timesteps:,} timesteps...")
            
            model.learn(
                total_timesteps=total_timesteps,
                callback=callbacks,
                tb_log_name="bomberman_ppo",
                reset_num_timesteps=resume_from is None
            )
            
            # Save final model
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            final_model_path = self.model_dir / f"bomberman_ppo_final_{timestamp}"
            model.save(str(final_model_path))
            print(f"💾 Final model saved to: {final_model_path}")
            
        except KeyboardInterrupt:
            print("\n⚠️ Training interrupted by user")
            # Save interrupted model
            interrupted_model_path = self.model_dir / f"bomberman_ppo_interrupted_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
            model.save(str(interrupted_model_path))
            print(f"💾 Interrupted model saved to: {interrupted_model_path}")
        
        finally:
            env.close()
            print("✅ Training completed!")
    
    def evaluate(self, model_path, n_episodes=10, render=True):
        """Evaluate trained model."""
        print(f"🧪 Evaluating model: {model_path}")
        
        # Create environment for evaluation
        env = self.create_vectorized_env(n_envs=1)
        
        # Load model
        model = PPO.load(model_path)
        
        # Evaluation loop
        episode_rewards = []
        episode_lengths = []
        
        for episode in range(n_episodes):
            obs = env.reset()
            episode_reward = 0
            episode_length = 0
            done = False
            
            while not done:
                action, _ = model.predict(obs, deterministic=True)
                obs, reward, done, info = env.step(action)
                episode_reward += reward[0]
                episode_length += 1
                
                if render:
                    env.render()
            
            episode_rewards.append(episode_reward)
            episode_lengths.append(episode_length)
            
            print(f"Episode {episode + 1}: Reward = {episode_reward:.2f}, Length = {episode_length}")
        
        # Calculate statistics
        mean_reward = np.mean(episode_rewards)
        std_reward = np.std(episode_rewards)
        mean_length = np.mean(episode_lengths)
        
        print(f"\n📈 Evaluation Results ({n_episodes} episodes):")
        print(f"Mean Reward: {mean_reward:.2f} ± {std_reward:.2f}")
        print(f"Mean Episode Length: {mean_length:.2f}")
        
        env.close()
        return mean_reward, std_reward, mean_length

def main():
    parser = argparse.ArgumentParser(description="Train Bomberman PPO Agent")
    parser.add_argument("--config", type=str, default="configs/ppo_config.yaml",
                      help="Path to configuration file")
    parser.add_argument("--resume", type=str, default=None,
                      help="Path to model to resume training from")
    parser.add_argument("--evaluate", type=str, default=None,
                      help="Path to model to evaluate")
    parser.add_argument("--episodes", type=int, default=10,
                      help="Number of episodes for evaluation")
    
    args = parser.parse_args()
    
    # Create trainer
    trainer = BombermanTrainer(args.config)
    
    if args.evaluate:
        # Evaluation mode
        trainer.evaluate(args.evaluate, n_episodes=args.episodes)
    else:
        # Training mode
        trainer.train(resume_from=args.resume)

if __name__ == "__main__":
    main()