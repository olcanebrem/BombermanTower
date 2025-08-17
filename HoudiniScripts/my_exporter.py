"""
Houdini Level Exporter Module
Bomberman Tower Unity Level Export System
Updated with better formatting and sections
"""

import hou
import os
from datetime import datetime


def get_controller_data():
    """CONTROLLER node'undan parametreleri al"""
    controller = hou.node('/obj/main/CONTROLLER')
    
    if not controller:
        raise Exception("CONTROLLER node not found!")
    
    return {
        "seed": controller.parm('seed').eval(),
        "room_count": controller.parm('room_count').eval(),
        "enemy_density": controller.parm('enemy_density').eval(),
        "loot_density": controller.parm('loot_density').eval() if controller.parm('loot_density') else 1.0,
        "coin_density": controller.parm('coin_density').eval() if controller.parm('coin_density') else 0.6,
        "health_density": controller.parm('health_density').eval() if controller.parm('health_density') else 0.5,
        "breakable_density": controller.parm('breakable_density').eval() if controller.parm('breakable_density') else 0.8,
        "sizeX": controller.parm('sizeX').eval(),
        "sizeY": controller.parm('sizeY').eval(),
        "edge_wall_bias": controller.parm('edge_wall_bias').eval() if controller.parm('edge_wall_bias') else 0.0,
        "noise_scale": controller.parm('noise_scale').eval() if controller.parm('noise_scale') else 0.8,
        "noise_threshold": controller.parm('noise_threshold').eval() if controller.parm('noise_threshold') else 0.5,
        "min_room_size": controller.parm('min_room_size').eval() if controller.parm('min_room_size') else 4,
        "max_room_size": controller.parm('max_room_size').eval() if controller.parm('max_room_size') else 8,
        "min_player_exit_dist": controller.parm('min_player_exit_dist').eval() if controller.parm('min_player_exit_dist') else 5
    }


def get_tile_data():
    """4_VISUALIZE_MAP node'undan tile verilerini çıkar"""
    visualize_node = hou.node("/obj/main/4_VISUALIZE_MAP")
    
    if not visualize_node:
        raise Exception("4_VISUALIZE_MAP node not found!")
    
    geo = visualize_node.geometry()
    if not geo:
        raise Exception("No geometry found in 4_VISUALIZE_MAP!")
    
    prims = geo.prims()
    if not prims:
        raise Exception("No primitives found in 4_VISUALIZE_MAP!")
    
    # Tile verilerini topla
    grid_chars = {}
    for prim in prims:
        bbox = prim.boundingBox()
        center = bbox.center()
        x = int(round(center[0]))
        z = int(round(center[2]))
        tile_char = prim.attribValue("tile_char")
        
        if x not in grid_chars:
            grid_chars[x] = {}
        grid_chars[x][z] = tile_char
    
    if not grid_chars:
        raise Exception("No tile data found!")
    
    return grid_chars


def create_ascii_grid(grid_chars):
    """Grid karakterlerinden ASCII grid oluştur"""
    # Grid boyutları
    min_x = min(grid_chars.keys())
    max_x = max(grid_chars.keys())
    min_z = min(min(row.keys()) for row in grid_chars.values())
    max_z = max(max(row.keys()) for row in grid_chars.values())
    
    grid_width = max_x - min_x + 1
    grid_height = max_z - min_z + 1
    
    # ASCII grid oluştur
    ascii_grid = []
    for z in range(max_z, min_z - 1, -1):  # Yukarıdan aşağıya
        row = ""
        for x in range(min_x, max_x + 1):  # Soldan sağa
            if x in grid_chars and z in grid_chars[x]:
                char = grid_chars[x][z]
                row += char if char else "."
            else:
                row += "."
        ascii_grid.append(row)
    
    return ascii_grid, grid_width, grid_height


def get_export_parameters(source_node=None):
    """Export parametrelerini al"""
    # FORMAT_PARAMS node'unu dene
    format_params = hou.node("/obj/main/FORMAT_PARAMS")
    if format_params:
        return {
            "export_folder": format_params.parm("levels_folder").eval() if format_params.parm("levels_folder") else "E:/UNITY/BombermanTower/unity/Assets/Levels",
            "format_version": format_params.parm("format_version").eval() if format_params.parm("format_version") else "v3.8",
            "level_version": format_params.parm("level_version").eval() if format_params.parm("level_version") else "v1.0.0"
        }
    
    # Default değerler
    return {
        "export_folder": "E:/UNITY/BombermanTower/unity/Assets/Levels",
        "format_version": "v3.8",
        "level_version": "v1.0.0"
    }


def get_houdini_version():
    """Houdini version bilgisini al"""
    try:
        return hou.applicationVersionString()
    except:
        return "Unknown"


def create_unity_level_content(controller_data, ascii_grid, grid_width, grid_height, export_params):
    """Unity level dosyası içeriğini oluştur - geliştirilmiş format"""
    current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    houdini_version = get_houdini_version()
    
    # Ana header
    content = f"""# === LEVEL DATASET {export_params['format_version']} ===
# Generator: Houdini {houdini_version}
# Export Date: {current_time}
# Encoding: UTF-8
# Levels: 1
# Format: Incremental IDs with Suffix
# ===================================

# ===================================
# LEVEL 0001 : Generated Level
# ===================================

[CELL_TYPES]
# ID=Symbol,Name,Passable,Prefab_Index
0=.,EMPTY,true,0
1=#,WALL,false,1
2=o,FLOOR,true,2
3=P,PLAYER,true,3
4=E,ENEMY,true,4
5=S,ENEMY_SHOOTER,true,5
6=C,COIN,true,6
7=H,HEALTH,true,7
8=B,BREAKABLE,false,8
9=X,STAIRS,true,9

# ===================================
# LEVEL CONFIGURATION
# ===================================

[LEVEL_CONFIG]
VERSION={export_params['level_version']}
FORMAT_VERSION={export_params['format_version']}
LEVEL_NAME=Generated Level
LEVEL_ID=0001
GRID_WIDTH={grid_width}
GRID_HEIGHT={grid_height}

# ===================================
# GENERATION PARAMETERS
# ===================================

[GENERATION_PARAMS]
HOUDINI_SEED={controller_data['seed']}
ROOM_COUNT={controller_data['room_count']}
ENEMY_DENSITY={controller_data['enemy_density']}
LOOT_DENSITY={controller_data['loot_density']}
COIN_DENSITY={controller_data['coin_density']}
HEALTH_DENSITY={controller_data['health_density']}
BREAKABLE_DENSITY={controller_data['breakable_density']}
EDGE_WALL_BIAS={controller_data['edge_wall_bias']}
NOISE_SCALE={controller_data['noise_scale']}
NOISE_THRESHOLD={controller_data['noise_threshold']}
MIN_ROOM_SIZE={controller_data['min_room_size']}
MAX_ROOM_SIZE={controller_data['max_room_size']}
MIN_PLAYER_EXIT_DIST={controller_data['min_player_exit_dist']}

# ===================================
# GRID DATA
# ===================================

[GRID_ASCII]"""
    
    # ASCII grid'i ekle
    for row in ascii_grid:
        content += f"\n{row}"
    
    # Footer
    content += f"""

# ===================================
# END OF LEVEL 0001
# ===================================
"""
    
    return content


def export_level_complete(source_node=None, show_ui_message=True):
    """Ana export fonksiyonu - güncellenmiş format ile"""
    try:
        print("🚀 Level export başlatılıyor...")
        
        # 1. Export parametrelerini al
        export_params = get_export_parameters(source_node)
        print(f"📁 Export folder: {export_params['export_folder']}")
        print(f"📄 Format version: {export_params['format_version']}")
        print(f"📄 Level version: {export_params['level_version']}")
        
        # 2. CONTROLLER verilerini al
        controller_data = get_controller_data()
        print(f"🎲 Seed: {controller_data['seed']}")
        print(f"🏠 Rooms: {controller_data['room_count']}")
        
        # 3. Tile verilerini al
        grid_chars = get_tile_data()
        print(f"🗺️ Tile data collected: {len(grid_chars)} columns")
        
        # 4. ASCII grid oluştur
        ascii_grid, grid_width, grid_height = create_ascii_grid(grid_chars)
        print(f"📊 Grid size: {grid_width}x{grid_height}")
        
        # 5. Unity level içeriğini oluştur (yeni format)
        content = create_unity_level_content(controller_data, ascii_grid, grid_width, grid_height, export_params)
        
        # 6. Export klasörünü oluştur
        os.makedirs(export_params['export_folder'], exist_ok=True)
        
        # 7. Dosyayı yaz
        filename = f"LEVEL_0001_{export_params['level_version']}_{export_params['format_version']}.ini"
        filepath = os.path.join(export_params['export_folder'], filename)
        
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        
        # 8. Başarı mesajı
        success_msg = f"🎉 Export Successful!\n📁 Folder: {export_params['export_folder']}\n📄 File: {filename}\n📊 Grid: {grid_width}x{grid_height}\n🎲 Seed: {controller_data['seed']}\n📄 Format: {export_params['format_version']}"
        print(success_msg.replace('\n', '\n'))
        
        if show_ui_message:
            try:
                hou.ui.displayMessage(success_msg, severity=hou.severityType.Message, title="Export Successful")
            except:
                pass
        
        return True
        
    except Exception as e:
        error_msg = f"❌ Export Error: {str(e)}"
        print(error_msg)
        
        if show_ui_message:
            try:
                hou.ui.displayMessage(error_msg, severity=hou.severityType.Error, title="Export Error")
            except:
                pass
        
        return False


def quick_export():
    """Hızlı export - UI mesajı olmadan"""
    return export_level_complete(show_ui_message=False)


def test_module():
    """Module test fonksiyonu"""
    print("🧪 my_exporter module test:")
    print("✅ Module loaded successfully!")
    
    try:
        controller_data = get_controller_data()
        print(f"✅ Controller data: {controller_data['seed']}")
        
        grid_chars = get_tile_data()
        print(f"✅ Tile data: {len(grid_chars)} columns")
        
        export_params = get_export_parameters()
        print(f"✅ Export params: {export_params['format_version']}")
        
        print("🎯 Module is ready for export!")
        return True
        
    except Exception as e:
        print(f"❌ Module test failed: {str(e)}")
        return False


if __name__ == "__main__":
    print("my_exporter module loaded!")
