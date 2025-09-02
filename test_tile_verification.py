#!/usr/bin/env python3
"""
Test script to verify the tile counting logic is working correctly
by analyzing level files and predicting expected tile counts.
"""
import re
from collections import Counter

def analyze_level_file(file_path):
    """Analyze a level file and count expected tiles"""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract grid data
    grid_lines = []
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
            
        if current_section == "GRID_ASCII":
            grid_lines.append(line)
    
    # Count characters in grid
    if not grid_lines:
        return None
        
    char_counts = Counter()
    total_cells = 0
    
    for line in grid_lines:
        for char in line:
            char_counts[char] += 1
            total_cells += 1
    
    # Map characters to tile types based on TileSymbols.DataSymbolToType
    tile_mapping = {
        '#': 'Wall',      # or '|' for new format
        '|': 'Wall', 
        '.': 'Empty',
        '-': 'Empty',
        '/': 'Breakable',
        'B': 'Breakable', # Legacy
        'P': 'Player',
        'E': 'Enemy',
        'S': 'EnemyShooter',
        'C': 'Coin', 
        'H': 'Health',
        'X': 'Explosion',  # or Exit
        'G': 'Gate',
        'O': 'Bomb',
        '*': 'Projectile'
    }
    
    tile_counts = Counter()
    for char, count in char_counts.items():
        tile_type = tile_mapping.get(char, f'Unknown({char})')
        tile_counts[tile_type] += count
    
    return {
        'total_cells': total_cells,
        'grid_size': (len(grid_lines[0]) if grid_lines else 0, len(grid_lines)),
        'char_counts': dict(char_counts),
        'tile_counts': dict(tile_counts)
    }

def main():
    print("=== TILE COUNT VERIFICATION TEST ===")
    
    level_files = [
        "Unity/Assets/Resources/Levels/LEVEL_0001_v1.0.0_v4.4.txt",
        "Unity/Assets/Resources/Levels/LEVEL_0002_v1.0.0_v4.4.txt"
    ]
    
    for file_path in level_files:
        try:
            analysis = analyze_level_file(file_path)
            if analysis:
                print(f"\n=== {file_path} ===")
                print(f"Grid Size: {analysis['grid_size']}")
                print(f"Total Cells: {analysis['total_cells']}")
                print("\nCharacter Counts:")
                for char, count in sorted(analysis['char_counts'].items()):
                    print(f"  '{char}': {count}")
                print("\nExpected Tile Counts:")
                for tile_type, count in sorted(analysis['tile_counts'].items()):
                    if count > 0:  # Only show non-zero counts
                        print(f"  {tile_type}: {count}")
            else:
                print(f"No grid data found in {file_path}")
        except Exception as e:
            print(f"Error analyzing {file_path}: {e}")
    
    print("\n=== VERIFICATION NOTES ===")
    print("These counts should match what Unity reports after level loading.")
    print("If Unity reports different counts, there's a loading issue.")
    print("The verification system will stop the game if counts don't match.")

if __name__ == "__main__":
    main()