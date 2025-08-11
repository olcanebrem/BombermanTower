# 6_CREATE_INTERACTABLES.py (Sadece 'path' Nokta Grubunu Kullanan Versiyon)
import hou
import random

node = hou.pwd()
geo = node.geometry()

# --- 1. KONTROL PANELİNİ BUL VE PARAMETRELERİ OKU ---
try:
    controller = hou.node("../CONTROLLER")
    seed = controller.parm("seed").evalAsInt()
    loot_density = controller.parm("loot_density").evalAsFloat()
    
    random.seed(seed + 3)

except AttributeError:
    print("UYARI: CONTROLLER veya gerekli parametreler bulunamadı. Varsayılan değerler kullanılıyor.")
    loot_density = 0.0

# --- 2. GANİMET YERLEŞTİRME (ANA YOLU KORUYARAK) ---
if loot_density > 0:
    # Ganimet yerleştirmek için uygun, YOL ÜZERİNDE OLMAYAN boş noktaları bul.
    suitable_loot_spots = []
    
    # 'path' adında bir nokta grubu olup olmadığını bir kez kontrol et.
    path_group = geo.findPointGroup("path")
    
    for pt in geo.points():
        # Önce noktanın 'empty' olup olmadığını kontrol et.
        if pt.stringAttribValue("tile_type") == "empty":
            
            # --- YENİ VE BASİTLEŞTİRİLMİŞ KONTROL ---
            is_on_path = False
            if path_group: # Eğer 'path' grubu varsa...
                # Bu noktanın o grubun içinde olup olmadığını kontrol et.
                is_on_path = path_group.contains(pt)
            
            # Eğer nokta ana yol üzerinde DEĞİLSE, ganimet için uygun bir yerdir.
            if not is_on_path:
                suitable_loot_spots.append(pt)

    # Şimdi sadece güvenli noktalardan oluşan listemiz üzerinde çalışıyoruz.
    for pt in suitable_loot_spots:
        if random.random() < (loot_density * 0.1):
            if random.random() < 0.75:
                pt.setAttribValue("tile_type", "coin")
            else:
                pt.setAttribValue("tile_type", "health")

# --- 3. KIRILABİLİR DUVARLARI YERLEŞTİRME ---
# Bu mantık değişmeden kalabilir.
breakable_ratio = loot_density * 0.5 

if breakable_ratio > 0:
    candidate_walls = []
    for pt in geo.points():
        if pt.stringAttribValue("tile_type") == "wall":
            is_edge_wall = False
            for prim in pt.prims():
                for neighbor_pt in prim.points():
                    if neighbor_pt.number() != pt.number() and neighbor_pt.stringAttribValue("tile_type") != "wall":
                        is_edge_wall = True
                        break
                if is_edge_wall:
                    break
            if is_edge_wall:
                candidate_walls.append(pt)

    num_to_convert = int(len(candidate_walls) * breakable_ratio)

    if num_to_convert > 0 and len(candidate_walls) >= num_to_convert:
        walls_to_break = random.sample(candidate_walls, num_to_convert)
        for pt in walls_to_break:
            pt.setAttribValue("tile_type", "breakable")