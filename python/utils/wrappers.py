"""
Environment wrappers for Bomberman training
"""

import gym
import numpy as np
from typing import Dict, Any

class BombermanWrapper(gym.Wrapper):
    """
    Custom wrapper for Bomberman environment with reward shaping and episode tracking.
    """
    
    def __init__(self, env, config: Dict[str, Any]):
        super().__init__(env)
        self.config = config
        self.reward_config = config.get('rewards', {})
        self.max_episode_steps = config.get('wrapper', {}).get('max_episode_steps', 3000)
        
        # Episode tracking
        self.episode_step = 0
        self.episode_stats = {}
        
        # Performance tracking
        self.enemies_killed = 0
        self.collectibles_collected = 0
        self.level_completed = False
        
        print(f"[BombermanWrapper] Initialized with max_episode_steps={self.max_episode_steps}")
    
    def reset(self, **kwargs):
        """Reset environment and episode tracking."""
        obs = self.env.reset(**kwargs)
        
        # Reset episode tracking
        self.episode_step = 0
        self.episode_stats = {
            'enemies_killed': 0,
            'collectibles_collected': 0,
            'level_completed': False,
            'deaths': 0
        }
        
        return obs
    
    def step(self, action):
        """Step environment with reward shaping."""
        obs, reward, done, info = self.env.step(action)
        
        self.episode_step += 1
        
        # Apply reward shaping
        shaped_reward = self._shape_reward(reward, info)
        
        # Update episode statistics
        self._update_episode_stats(info)
        
        # Check timeout
        if self.episode_step >= self.max_episode_steps:
            done = True
            shaped_reward += self.reward_config.get('timeout', -2.0)
        
        # Add episode statistics to info
        if done:
            info['episode'] = {
                'reward': shaped_reward,
                'length': self.episode_step,
                'stats': self.episode_stats.copy()
            }
        
        return obs, shaped_reward, done, info
    
    def _shape_reward(self, base_reward: float, info: Dict) -> float:
        """Apply reward shaping based on game events."""
        shaped_reward = base_reward
        
        # Extract game events from info
        events = info.get('events', {})
        
        # Reward shaping based on configuration
        if events.get('enemy_killed', False):
            shaped_reward += self.reward_config.get('enemy_kill', 2.0)
            self.enemies_killed += 1
        
        if events.get('collectible_collected', False):
            shaped_reward += self.reward_config.get('collectible_base', 1.0)
            self.collectibles_collected += 1
        
        if events.get('health_collected', False):
            shaped_reward += self.reward_config.get('health_collectible', 1.5)
        
        if events.get('level_completed', False):
            shaped_reward += self.reward_config.get('level_complete', 10.0)
            self.level_completed = True
        
        if events.get('player_died', False):
            shaped_reward += self.reward_config.get('death', -5.0)
        
        if events.get('wall_destroyed', False):
            shaped_reward += self.reward_config.get('wall_destroyed', 0.2)
        
        if events.get('bomb_placed', False):
            shaped_reward += self.reward_config.get('bomb_placed', 0.1)
        
        # Step penalty for encouraging faster completion
        shaped_reward += self.reward_config.get('step_penalty', -0.001)
        
        return shaped_reward
    
    def _update_episode_stats(self, info: Dict):
        """Update episode statistics tracking."""
        events = info.get('events', {})
        
        if events.get('enemy_killed', False):
            self.episode_stats['enemies_killed'] += 1
        
        if events.get('collectible_collected', False):
            self.episode_stats['collectibles_collected'] += 1
        
        if events.get('level_completed', False):
            self.episode_stats['level_completed'] = True
        
        if events.get('player_died', False):
            self.episode_stats['deaths'] += 1


class NormalizeObservationWrapper(gym.ObservationWrapper):
    """Normalize observations to [0, 1] range."""
    
    def __init__(self, env):
        super().__init__(env)
        
    def observation(self, observation):
        """Normalize observation."""
        # Assuming observations are already in a reasonable range
        # This can be customized based on your observation space
        return np.clip(observation, -1.0, 1.0)


class FrameStackWrapper(gym.Wrapper):
    """Stack multiple frames for temporal information."""
    
    def __init__(self, env, n_frames=4):
        super().__init__(env)
        self.n_frames = n_frames
        self.frames = []
        
        # Update observation space
        obs_space = env.observation_space
        if isinstance(obs_space, gym.spaces.Box):
            new_shape = (obs_space.shape[0] * n_frames,) + obs_space.shape[1:]
            self.observation_space = gym.spaces.Box(
                low=obs_space.low.min(),
                high=obs_space.high.max(),
                shape=new_shape,
                dtype=obs_space.dtype
            )
    
    def reset(self, **kwargs):
        obs = self.env.reset(**kwargs)
        self.frames = [obs] * self.n_frames
        return self._get_observation()
    
    def step(self, action):
        obs, reward, done, info = self.env.step(action)
        self.frames.append(obs)
        if len(self.frames) > self.n_frames:
            self.frames.pop(0)
        return self._get_observation(), reward, done, info
    
    def _get_observation(self):
        """Stack frames along first dimension."""
        return np.concatenate(self.frames, axis=0)