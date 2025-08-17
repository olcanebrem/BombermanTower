"""
Houdini Level Exporter Module
Bomberman Tower Unity Level Export System
Updated with better formatting and sections
FIX: FORMAT_PARAMS support added
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
    
    print(f"🔍 Tile Data Debug:")
    print(f"   Total primitives: {len(prims)}")
    
    # Tile verilerini topla
    grid_chars = {}
    empty_chars = 0
    valid_chars = 0
    
    # DEEP DEBUG: Koordinat dağılımını incele
    x_coords = []
    z_coords = []
    
    for i, prim in enumerate(prims):
        bbox = prim.boundingBox()
        center = bbox.center()
        
        # FIX: Koordinat konversiyonu - Houdini world space'ten grid space'e
        # Houdini'de 0.5, 1.5, 2.5... -> Grid'de 0, 1, 2...
        x = int(round(center[0] - 0.5))  # FIX: 0.5 offset çıkar
        z = int(round(center[2] - 0.5))  # FIX: 0.5 offset çıkar
        
        x_coords.append(x)
        z_coords.append(z)
        
        # tile_char attribute'unu al
        try:
            tile_char = prim.attribValue("tile_char")
        except:
            tile_char = None
        
        # Debug: İlk birkaç tile'ı göster
        if i < 10:
            print(f"   Prim {i}: pos({center[0]:.3f}, {center[2]:.3f}) -> grid({x}, {z}) -> char: '{tile_char}'")
        
        if x not in grid_chars:
            grid_chars[x] = {}
        grid_chars[x][z] = tile_char
        
        # İstatistik
        if tile_char is None or tile_char == "" or tile_char == ".":
            empty_chars += 1
        else:
            valid_chars += 1
    
    # Koordinat analizi
    x_min, x_max = min(x_coords), max(x_coords)
    z_min, z_max = min(z_coords), max(z_coords)
    expected_total = (x_max - x_min + 1) * (z_max - z_min + 1)
    actual_total = len(prims)
    
    print(f"🔍 Koordinat Analizi (FIXED):")
    print(f"   X coords: {x_min} to {x_max} (range: {x_max - x_min + 1})")
    print(f"   Z coords: {z_min} to {z_max} (range: {z_max - z_min + 1})")
    print(f"   Expected total tiles: {expected_total}")
    print(f"   Actual primitives: {actual_total}")
    print(f"   Missing tiles: {expected_total - actual_total}")
    print(f"   Valid chars: {valid_chars}, Empty chars: {empty_chars}")
    
    # X ve Z koordinat dağılımını kontrol et
    x_unique = sorted(set(x_coords))
    z_unique = sorted(set(z_coords))
    print(f"   Unique X coords: {len(x_unique)} -> {x_unique[:15]}...")
    print(f"   Unique Z coords: {len(z_unique)} -> {z_unique[:15]}...")
    
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
    
    print(f"🔍 Grid Debug:")
    print(f"   X range: {min_x} to {max_x} (width: {grid_width})")
    print(f"   Z range: {min_z} to {max_z} (height: {grid_height})")
    print(f"   Total tiles: {sum(len(row) for row in grid_chars.values())}")
    
    # DEEP DEBUG: Hangi koordinatlarda tile var?
    all_coords = []
    for x in grid_chars:
        for z in grid_chars[x]:
            all_coords.append((x, z))
    
    print(f"   İlk 10 koordinat: {all_coords[:10]}")
    print(f"   Son 10 koordinat: {all_coords[-10:]}")
    
    # ASCII grid oluştur - FIX: Doğru yön
    ascii_grid = []
    question_count = 0
    valid_count = 0
    
    for z in range(min_z, max_z + 1):  # FIX: Aşağıdan yukarıya (normal order)
        row = ""
        row_questions = 0
        row_valids = 0
        
        for x in range(min_x, max_x + 1):  # Soldan sağa
            if x in grid_chars and z in grid_chars[x]:
                char = grid_chars[x][z]
                # FIX: Boş karakter kontrolü
                if char is None or char == "":
                    row += "."
                    row_valids += 1
                    valid_count += 1
                else:
                    row += str(char)
                    row_valids += 1
                    valid_count += 1
            else:
                row += "?"
                row_questions += 1
                question_count += 1
        
        ascii_grid.append(row)
        
        # Debug: İlk birkaç satırı göster
        if len(ascii_grid) <= 5:
            print(f"   Row {z}: {row[:30]}{'...' if len(row) > 30 else ''}")
            print(f"     -> Valids: {row_valids}, Questions: {row_questions}")
    
    print(f"🚨 SUMMARY: Valid tiles: {valid_count}, Missing tiles (?): {question_count}")
    
    # DEEP DEBUG: grid_chars yapısını kontrol et
    print(f"🔍 Grid_chars yapısı:")
    x_keys = sorted(grid_chars.keys())[:5]  # İlk 5 X koordinatı
    for x in x_keys:
        z_keys = sorted(grid_chars[x].keys())
        print(f"   X={x}: Z values = {z_keys[:10]}...")  # İlk 10 Z değeri
    
    return ascii_grid, grid_width, grid_height


def find_format_params_node():
    """FORMAT_PARAMS node'unu bul"""
    # Olası konumları kontrol et
    possible_locations = [
        "/obj/main/FORMAT_PARAMS",
        "/obj/FORMAT_PARAMS", 
        "/obj/main/format_params",
        "/obj/format_params"
    ]
    
    for location in possible_locations:
        node = hou.node(location)
        if node:
            print(f"✅ FORMAT_PARAMS node bulundu: {location}")
            return node
    
    print("⚠️ FORMAT_PARAMS node bulunamadı, tüm node'ları arıyorum...")
    
    # Tüm obj node'larını tara
    obj_node = hou.node("/obj")
    if obj_node:
        for child in obj_node.allSubChildren():
            if "format" in child.name().lower() or "FORMAT" in child.name():
                print(f"🔍 Potansiyel FORMAT node bulundu: {child.path()}")
                return child
    
    return None


def get_export_parameters(source_node=None):
    """Export parametrelerini al - FORMAT_PARAMS desteği ile"""
    
    # 1. Önce FORMAT_PARAMS node'unu ara
    format_node = find_format_params_node()
    
    if format_node:
        print(f"📋 FORMAT_PARAMS node'undan parametreler alınıyor: {format_node.path()}")
        try:
            return {
                "export_folder": format_node.parm("levels_folder").eval() if format_node.parm("levels_folder") else "E:/UNITY/BombermanTower/unity/Assets/Levels",
                "format_version": format_node.parm("format_version").eval() if format_node.parm("format_version") else "v3.8",
                "level_version": format_node.parm("level_version").eval() if format_node.parm("level_version") else "v1.0.0",
                "level_count": format_node.parm("level_count").eval() if format_node.parm("level_count") else 1  # NEW: Level sayısı
            }
        except Exception as e:
            print(f"⚠️ FORMAT_PARAMS node'undan parametre alınırken hata: {e}")
    
    # 2. Source node kontrolü
    if source_node is None:
        # Mevcut node'u dene (HDA içinden çağrılıyorsa)
        try:
            source_node = hou.pwd()
            print(f"📋 Current node kullanılıyor: {source_node.path()}")
        except:
            # Diğer olası node'ları dene
            possible_nodes = [
                "/obj/main/my_exporter",
                "/obj/main/levelexporter_hda",
                "/obj/my_exporter",
                "/obj/levelexporter_hda"
            ]
            
            for node_path in possible_nodes:
                test_node = hou.node(node_path)
                if test_node:
                    source_node = test_node
                    print(f"📋 Export node bulundu: {node_path}")
                    break
    
    if source_node:
        print(f"📋 Source node'dan parametreler alınıyor: {source_node.path()}")
        try:
            return {
                "export_folder": source_node.parm("levels_folder").eval() if source_node.parm("levels_folder") else "E:/UNITY/BombermanTower/unity/Assets/Levels",
                "format_version": source_node.parm("format_version").eval() if source_node.parm("format_version") else "v3.8",
                "level_version": source_node.parm("level_version").eval() if source_node.parm("level_version") else "v1.0.0",
                "level_count": source_node.parm("level_count").eval() if source_node.parm("level_count") else 1  # NEW: Level sayısı
            }
        except Exception as e:
            print(f"⚠️ Source node'dan parametre alınırken hata: {e}")
    
    # 3. Default değerler
    print("📋 Default parametreler kullanılıyor")
    return {
        "export_folder": "E:/UNITY/BombermanTower/unity/Assets/Levels",
        "format_version": "v3.8",
        "level_version": "v1.0.0",
        "level_count": 1  # NEW: Default 1 level
    }


def get_houdini_version():
    """Houdini version bilgisini al"""
    try:
        return hou.applicationVersionString()
    except:
        return "Unknown"


def cook_pipeline_with_seed(seed_value):
    """Pipeline'ı belirli seed ile cook et"""
    try:
        # CONTROLLER node'unu al
        controller = hou.node('/obj/main/CONTROLLER')
        if not controller:
            raise Exception("CONTROLLER node not found!")
        
        # Seed'i ayarla
        controller.parm('seed').set(seed_value)
        print(f"   🎲 Seed set to: {seed_value}")
        
        # Pipeline'ı cook et (4_VISUALIZE_MAP node'unu cook et)
        visualize_node = hou.node("/obj/main/4_VISUALIZE_MAP")
        if not visualize_node:
            raise Exception("4_VISUALIZE_MAP node not found!")
        
        # Node'u cook et
        visualize_node.cook(force=True)
        print(f"   🔄 Pipeline cooked with seed {seed_value}")
        
        return True
        
    except Exception as e:
        print(f"   ❌ Pipeline cook failed for seed {seed_value}: {str(e)}")
        return False


def export_single_level(level_id, seed_value, export_params):
    """Tek bir level export et"""
    try:
        print(f"📄 Level {level_id:04d} export başlatılıyor (seed: {seed_value})...")
        
        # 1. Pipeline'ı bu seed ile cook et
        if not cook_pipeline_with_seed(seed_value):
            return False
        
        # 2. CONTROLLER verilerini al (güncel seed ile)
        controller_data = get_controller_data()
        
        # 3. Tile verilerini al
        grid_chars = get_tile_data()
        
        # 4. ASCII grid oluştur
        ascii_grid, grid_width, grid_height = create_ascii_grid(grid_chars)
        
        # 5. Unity level içeriğini oluştur
        content = create_unity_level_content_multi(level_id, controller_data, ascii_grid, grid_width, grid_height, export_params)
        
        # 6. Dosyayı yaz
        filename = f"LEVEL_{level_id:04d}_{export_params['level_version']}_{export_params['format_version']}.ini"
        filepath = os.path.join(export_params['export_folder'], filename)
        
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        
        print(f"   ✅ Level {level_id:04d} exported: {filename}")
        return True
        
    except Exception as e:
        print(f"   ❌ Level {level_id:04d} export failed: {str(e)}")
        return False


def create_unity_level_content_multi(level_id, controller_data, ascii_grid, grid_width, grid_height, export_params):
    """Unity level dosyası içeriğini oluştur - multi level için"""
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
# LEVEL {level_id:04d} : Generated Level
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
LEVEL_NAME=Generated Level {level_id:04d}
LEVEL_ID={level_id:04d}
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
# END OF LEVEL {level_id:04d}
# ===================================
"""
    
    return content


def export_level_complete(source_node=None, show_ui_message=True):
    """
    Ana export fonksiyonu - multi-level desteği ile
    
    Args:
        source_node: Export parametrelerini alacağı node (None ise otomatik tespit)
        show_ui_message: UI mesajı gösterilsin mi
    
    Returns:
        bool: Export başarılı ise True
    """
    try:
        print("🚀 Multi-Level export başlatılıyor...")
        
        # 1. Export parametrelerini al (level_count dahil)
        export_params = get_export_parameters(source_node)
        level_count = export_params['level_count']
        
        print(f"📁 Export folder: {export_params['export_folder']}")
        print(f"📄 Format version: {export_params['format_version']}")
        print(f"📄 Level version: {export_params['level_version']}")
        print(f"🎯 Level count: {level_count}")
        
        # 2. Export klasörünü oluştur
        os.makedirs(export_params['export_folder'], exist_ok=True)
        
        # 3. İlk seed'i al
        controller = hou.node('/obj/main/CONTROLLER')
        if not controller:
            raise Exception("CONTROLLER node not found!")
        
        base_seed = controller.parm('seed').eval()
        print(f"🎲 Base seed: {base_seed}")
        
        # 4. Her level için export
        successful_exports = 0
        failed_exports = 0
        exported_files = []
        
        for level_num in range(1, level_count + 1):
            # Her level için seed'i artır
            current_seed = base_seed + (level_num - 1)
            
            print(f"\n📦 === LEVEL {level_num}/{level_count} ===")
            
            if export_single_level(level_num, current_seed, export_params):
                successful_exports += 1
                filename = f"LEVEL_{level_num:04d}_{export_params['level_version']}_{export_params['format_version']}.ini"
                exported_files.append(filename)
            else:
                failed_exports += 1
        
        # 5. Özet rapor
        success_msg = f"""🎉 Multi-Level Export Complete!

📊 SUMMARY:
   ✅ Successful: {successful_exports}/{level_count}
   ❌ Failed: {failed_exports}/{level_count}
   📁 Folder: {export_params['export_folder']}
   📄 Format: {export_params['format_version']}
   🎲 Base Seed: {base_seed}

📄 FILES:"""
        
        for i, filename in enumerate(exported_files[:10]):  # İlk 10 dosyayı göster
            success_msg += f"\n   {i+1:2d}. {filename}"
        
        if len(exported_files) > 10:
            success_msg += f"\n   ... ve {len(exported_files) - 10} dosya daha"
        
        print(success_msg.replace('\n', '\n'))
        
        if show_ui_message:
            try:
                hou.ui.displayMessage(success_msg, severity=hou.severityType.Message)
            except:
                pass
        
        return successful_exports > 0
        
    except Exception as e:
        error_msg = f"❌ Multi-Level Export Error: {str(e)}"
        print(error_msg)
        
        if show_ui_message:
            try:
                hou.ui.displayMessage(error_msg, severity=hou.severityType.Error)
            except:
                pass
        
        return False


def export_single_level_legacy():
    """Eski single level export fonksiyonu - backward compatibility"""
    try:
        print("🚀 Single Level export başlatılıyor...")
        
        # 1. Export parametrelerini al
        export_params = get_export_parameters()
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
        
        # 5. Unity level içeriğini oluştur
        content = create_unity_level_content_multi(1, controller_data, ascii_grid, grid_width, grid_height, export_params)
        
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
        
        return True
        
    except Exception as e:
        error_msg = f"❌ Export Error: {str(e)}"
        print(error_msg)
        return False


def quick_export():
    """Hızlı export - UI mesajı olmadan"""
    export_params = get_export_parameters()
    level_count = export_params.get('level_count', 1)
    
    if level_count == 1:
        print("⚡ Hızlı single export başlatılıyor...")
        return export_single_level_legacy()
    else:
        print(f"⚡ Hızlı multi export başlatılıyor... ({level_count} levels)")
        return export_level_complete(show_ui_message=False)


def debug_nodes():
    """Mevcut node'ları debug et"""
    print("🔍 Node Debug Başlatılıyor...")
    
    # FORMAT_PARAMS arama
    format_node = find_format_params_node()
    if format_node:
        print(f"✅ FORMAT_PARAMS node: {format_node.path()}")
        
        # Parametreleri listele
        try:
            parms = format_node.parms()
            print("📋 Mevcut parametreler:")
            for parm in parms:
                print(f"   - {parm.name()}: {parm.eval()}")
        except Exception as e:
            print(f"⚠️ Parametre listesi alınamadı: {e}")
    else:
        print("❌ FORMAT_PARAMS node bulunamadı")
    
    # CONTROLLER kontrolü
    controller = hou.node('/obj/main/CONTROLLER')
    if controller:
        print(f"✅ CONTROLLER node: {controller.path()}")
    else:
        print("❌ CONTROLLER node bulunamadı")
    
    # VISUALIZE kontrolü  
    visualize = hou.node("/obj/main/4_VISUALIZE_MAP")
    if visualize:
        print(f"✅ 4_VISUALIZE_MAP node: {visualize.path()}")
    else:
        print("❌ 4_VISUALIZE_MAP node bulunamadı")


# Test fonksiyonu
def test_module():
    """Module test fonksiyonu"""
    print("🧪 my_exporter module test:")
    print("✅ Module loaded successfully!")
    
    try:
        # Debug node'ları
        debug_nodes()
        
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