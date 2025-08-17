import hou
import os

def get_export_params():
    """FORMAT_PARAMS node'undan parametreleri al"""
    source_node = hou.node("/obj/main/FORMAT_PARAMS")
    
    if not source_node:
        print("❌ FORMAT_PARAMS node bulunamadı!")
        return None
    
    return {
        "export_folder": source_node.parm("levels_folder").eval(),
        "format_version": source_node.parm("format_version").eval(),
        "level_version": source_node.parm("level_version").eval(),
        "map_name": source_node.parm("map_name").eval(),
        "map_width": source_node.parm("map_width").eval(),
        "map_height": source_node.parm("map_height").eval(),
        "wall_symbol": source_node.parm("wall_symbol").eval(),
        "floor_symbol": source_node.parm("floor_symbol").eval(),
        "player_symbol": source_node.parm("player_symbol").eval(),
        "exit_symbol": source_node.parm("exit_symbol").eval(),
        "enemy_symbol": source_node.parm("enemy_symbol").eval(),
        "powerup_symbol": source_node.parm("powerup_symbol").eval()
    }

def export_level():
    """LEVEL_0001_v1.0.0_v4.0.ini formatında export"""
    print("🔧 Level export başlatılıyor...")

    # Parametreleri al
    params = get_export_params()
    if not params:
        print("❌ Parametreler alınamadı!")
        return False

    print(f"✅ Parametreler alındı:")
    for key, value in params.items():
        print(f"   {key}: {value}")

    # Export klasörünü oluştur
    export_dir = params['export_folder']
    if not os.path.exists(export_dir):
        os.makedirs(export_dir)
        print(f"📁 Export klasörü oluşturuldu: {export_dir}")

    # DOSYA ADI: LEVEL_0001_v1.0.0_v4.0.ini
    filename = f"{params['map_name']}_{params['level_version']}_{params['format_version']}.ini"
    export_file = os.path.join(export_dir, filename)

    # INI FORMAT İÇERİĞİ
    ini_content = f"""[LEVEL_INFO]
FORMAT_VERSION={params['format_version']}
LEVEL_VERSION={params['level_version']}
MAP_NAME={params['map_name']}
MAP_WIDTH={params['map_width']}
MAP_HEIGHT={params['map_height']}

[SYMBOLS]
WALL={params['wall_symbol']}
FLOOR={params['floor_symbol']}
PLAYER={params['player_symbol']}
EXIT={params['exit_symbol']}
ENEMY={params['enemy_symbol']}
POWERUP={params['powerup_symbol']}

[MAP_DATA]
# Harita verisi burada olacak
"""

    print(f"📝 INI dosyası hazırlandı")
    print(f"💾 Export dosyası: {filename}")

    # Dosyayı yaz
    try:
        with open(export_file, 'w', encoding='utf-8') as f:
            f.write(ini_content)
        print(f"✅ Export başarılı: {filename}")
        print(f"📁 Konum: {export_file}")
        return True
    except Exception as e:
        print(f"❌ Export hatası: {str(e)}")
        return False

# Direkt çalıştırma
if __name__ == "__main__":
    export_level()
