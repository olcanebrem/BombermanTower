import hou
import os
from datetime import datetime

def export_level_complete():
    """Complete level export function - run this anytime you want to export"""
    
    # Node'lardan verileri al
    my_exporter = hou.node("/obj/main/my_exporter")
    controller = hou.node('/obj/main/CONTROLLER')
    visualize_node = hou.node("/obj/main/4_VISUALIZE_MAP")
    
    if not all([my_exporter, controller, visualize_node]):
        print("❌ Error: Required nodes not found!")
        return False
    
    # Export parametreleri
    export_folder = my_exporter.parm("levels_folder").eval()
    format_ver = my_exporter.parm("format_version").eval() 
    level_ver = my_exporter.parm("level_version").eval()
    
    # Controller parametreleri
    controller_data = {
        "seed": controller.parm('seed').eval(),
        "room_count": controller.parm('room_count').eval(),
        "enemy_density": controller.parm('enemy_density').eval(),
        "loot_density": controller.parm('loot_density').eval(),
        "coin_density": controller.parm('coin_density').eval(),
        "health_density": controller.parm('health_density').eval(),
        "breakable_density": controller.parm('breakable_density').eval(),
        "sizeX": controller.parm('sizeX').eval(),
        "sizeY": controller.parm('sizeY').eval()
    }
    
    # 4_VISUALIZE_MAP'den tile verilerini çıkar
    geo = visualize_node.geometry()
    prims = geo.prims()
    
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
    
    # Grid boyutları
    min_x = min(grid_chars.keys())
    max_x = max(grid_chars.keys())
    min_z = min(min(row.keys()) for row in grid_chars.values())
    max_z = max(max(row.keys()) for row in grid_chars.values())
    grid_width = max_x - min_x + 1
    grid_height = max_z - min_z + 1
    
    # ASCII grid oluştur
    ascii_grid = []
    for z in range(max_z, min_z - 1, -1):
        row = ""
        for x in range(min_x, max_x + 1):
            if x in grid_chars and z in grid_chars[x]:
                char = grid_chars[x][z]
                row += char if char else "."
            else:
                row += "."
        ascii_grid.append(row)
    
    # Klasör oluştur
    os.makedirs(export_folder, exist_ok=True)
    
    # Şu anki tarih
    current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    # Dosya içeriği
    content = f"""# Unity Level Data {format_ver}
# CORRECT TILES from 4_VISUALIZE_MAP
# Export Date: {current_time}
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
[LEVEL_CORRECT_TILES]
VERSION={level_ver}
LEVEL_NAME=Correct Tiles Level
GRID_WIDTH={grid_width}
GRID_HEIGHT={grid_height}
HOUDINI_SEED={controller_data['seed']}
ROOM_COUNT={controller_data['room_count']}
ENEMY_DENSITY={controller_data['enemy_density']}
[GRID_ASCII]"""
    
    # ASCII grid'i ekle
    for row in ascii_grid:
        content += f"\n{row}"
    
    # Dosyayı yaz
    filename = f"LEVEL_0001_{level_ver}_{format_ver}.ini"
    filepath = os.path.join(export_folder, filename)
    
    try:
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        
        print("🎉 EXPORT BAŞARILI!")
        print(f"📁 Konum: {export_folder}")
        print(f"📄 Dosya: {filename}")
        print(f"📊 Grid: {grid_width}x{grid_height}")
        print(f"🎲 Seed: {controller_data['seed']}")
        print(f"📅 Zaman: {current_time}")
        
        return True
        
    except Exception as e:
        print(f"❌ Export hatası: {str(e)}")
        return False

# Export'u çalıştır
export_level_complete()