#!/usr/bin/env python3
"""
Test script to verify that the level files are properly parsed and different.
This helps verify the level sequencing fix outside of Unity.
"""
import re

def parse_level_file(file_path):
    """Parse a level file and extract key information"""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract level info
    level_name = None
    level_id = None
    grid_lines = []
    player_spawn = None
    
    current_section = None
    for line in content.split('\n'):
        line = line.strip().replace('\r', '')
        
        # Skip comments and empty lines outside grid
        if current_section != "GRID_ASCII" and (not line or line.startswith('#')):
            continue
            
        # Section headers
        if line.startswith('[') and line.endswith(']'):
            current_section = line[1:-1]
            continue
            
        # Parse based on section
        if current_section == "LEVEL_CONFIG":
            if '=' in line:
                key, value = line.split('=', 1)
                if key.strip() == "LEVEL_NAME":
                    level_name = value.strip()
                elif key.strip() == "LEVEL_ID":
                    level_id = value.strip()
        
        elif current_section == "GRID_ASCII":
            grid_lines.append(line)
    
    # Find player spawn in grid
    for y, line in enumerate(grid_lines):
        for x, char in enumerate(line):
            if char == 'P':
                player_spawn = (x, y)
                break
        if player_spawn:
            break
    
    return {
        'level_name': level_name,
        'level_id': level_id,
        'player_spawn': player_spawn,
        'grid_size': (len(grid_lines[0]) if grid_lines else 0, len(grid_lines)),
        'first_row_sample': grid_lines[0][:20] if grid_lines else ""
    }

def main():
    print("=== LEVEL PARSING TEST ===")
    
    level_files = [
        "Unity/Assets/Resources/Levels/LEVEL_0001_v1.0.0_v4.4.txt",
        "Unity/Assets/Resources/Levels/LEVEL_0002_v1.0.0_v4.4.txt"
    ]
    
    parsed_levels = []
    for file_path in level_files:
        try:
            level_data = parse_level_file(file_path)
            parsed_levels.append(level_data)
            print(f"\nLevel File: {file_path}")
            print(f"  Name: {level_data['level_name']}")
            print(f"  ID: {level_data['level_id']}")
            print(f"  Player Spawn: {level_data['player_spawn']}")
            print(f"  Grid Size: {level_data['grid_size']}")
            print(f"  First Row Sample: '{level_data['first_row_sample']}'")
        except Exception as e:
            print(f"Error parsing {file_path}: {e}")
    
    # Compare levels
    if len(parsed_levels) >= 2:
        print(f"\n=== COMPARISON ===")
        level1, level2 = parsed_levels[0], parsed_levels[1]
        print(f"Player spawns are {'SAME' if level1['player_spawn'] == level2['player_spawn'] else 'DIFFERENT'}")
        print(f"Level 1 spawn: {level1['player_spawn']}")
        print(f"Level 2 spawn: {level2['player_spawn']}")
        print(f"First rows are {'SAME' if level1['first_row_sample'] == level2['first_row_sample'] else 'DIFFERENT'}")
        
        if level1['player_spawn'] != level2['player_spawn']:
            print("✅ LEVELS ARE PROPERLY DIFFERENT - Level sequencing should work!")
        else:
            print("❌ LEVELS ARE IDENTICAL - This explains the sequencing issue!")

if __name__ == "__main__":
    main()