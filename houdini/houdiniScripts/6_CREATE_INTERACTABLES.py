# 6_CREATE_INTERACTABLES.py (NİHAİ ve DOĞRU Hali)
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
    print("UYARI: CONTROLLER veya gerekli parametreler bulunamadı.")
    loot_density = 0.0

# --- 2. GANİMET YERLEŞTİRME ---
if loot_density > 0:
    empty_points_for_loot = [pt for pt in geo.points() if pt.stringAttribValue("tile_type") == "empty"]
    
    for pt in empty_points_for_loot:
        if random.random() < (loot_density * 0.1):
            if random.random() < 0.75:
                pt.setAttribValue("tile_type", "coin")
            else:
                pt.setAttribValue("tile_type", "health")

# --- 3. KIRILABİLİR DUVARLARI YERLEŞTİRME (DÜZELTİLMİŞ KISIM) ---
breakable_ratio = loot_density * 0.5

if breakable_ratio > 0:
    candidate_walls = []
    for pt in geo.points():
        if pt.stringAttribValue("tile_type") == "wall":
            is_edge = False
            
            # HATA BURADAYDI: Şimdi doğru yöntemi kullanıyoruz.
            # 1. Bu noktaya bağlı olan TÜM yüzeyleri (primitive'leri) bul.
            for prim in pt.prims():
                # 2. Bu yüzeyi oluşturan TÜM noktaları bul.
                for neighbor_pt in prim.points():
                    # Noktanın kendisini komşu olarak sayma.
                    if neighbor_pt.number() == pt.number():
                        continue
                    
                    # 3. Eğer komşulardan BİR TANESİ bile 'wall' değilse,
                    #    bu bir kenar duvardır. Döngüleri durdurabiliriz.
                    if neighbor_pt.stringAttribValue("tile_type") != "wall":
                        is_edge = True
                        break
                
                if is_edge:
                    break
            
            if is_edge:
                candidate_walls.append(pt)

    # Orana göre kaç tanesini dönüştüreceğimizi hesapla.
    num_to_convert = int(len(candidate_walls) * breakable_ratio)

    if num_to_convert > 0 and len(candidate_walls) >= num_to_convert:
        walls_to_break = random.sample(candidate_walls, num_to_convert)
        for pt in walls_to_break:
            pt.setAttribValue("tile_type", "breakable")