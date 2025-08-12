# 6_CREATE_INTERACTABLES.py (Kenar/İç Duvar Oranı Kontrolü)
import hou
import random

node = hou.pwd()
geo = node.geometry()

# --- 1. KONTROL PANELİNİ BUL VE YENİ PARAMETRELERİ OKU ---
try:
    controller = hou.node("../CONTROLLER") # VEYA DOĞRU MUTLAK YOL
    seed = controller.parm("seed").evalAsInt()
    coin_density = controller.parm("coin_density").evalAsFloat()
    health_density = controller.parm("health_density").evalAsFloat()
    breakable_density = controller.parm("breakable_density").evalAsFloat()
    edge_wall_bias = controller.parm("edge_wall_bias").evalAsFloat() # YENİ PARAMETRE
    
    random.seed(seed + 3)

except (AttributeError, TypeError):
    print("UYARI: CONTROLLER veya gerekli parametreler bulunamadı. Varsayılan değerler kullanılıyor.")
    coin_density, health_density, breakable_density, edge_wall_bias = 0.0, 0.0, 0.0, 1.0

# ... (Ganimet yerleştirme kodu aynı kalıyor) ...

# --- 3. KIRILABİLİR DUVARLARI YERLEŞTİRME (GELİŞMİŞ ORAN MANTIĞI) ---
if breakable_density > 0:
    # Aday duvarları iki ayrı listeye ayır.
    edge_wall_candidates = []
    thick_wall_candidates = []
    
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
                edge_wall_candidates.append(pt)
            else:
                # Eğer bir kenar duvarı değilse, o zaman bir "iç/kalın" duvardır.
                thick_wall_candidates.append(pt)

    # Toplamda kaç tane kırılabilir duvar oluşturulacağını hesapla.
    total_candidates = len(edge_wall_candidates) + len(thick_wall_candidates)
    total_to_convert = int(total_candidates * (breakable_density * 0.2))

    # Her listeden kaç tane seçeceğimizi, yeni parametremize göre hesapla.
    num_from_edge = int(total_to_convert * edge_wall_bias)
    num_from_thick = total_to_convert - num_from_edge

    # Güvenlik kontrolü: Listede yeterli aday yoksa, seçebileceğimiz maksimum sayıyla sınırla.
    num_from_edge = min(num_from_edge, len(edge_wall_candidates))
    num_from_thick = min(num_from_thick, len(thick_wall_candidates))

    # Her listeden rastgele seçim yap.
    edge_picks = []
    if num_from_edge > 0:
        edge_picks = random.sample(edge_wall_candidates, num_from_edge)
        
    thick_picks = []
    if num_from_thick > 0:
        thick_picks = random.sample(thick_wall_candidates, num_from_thick)

    # İki seçim listesini birleştir.
    walls_to_break = edge_picks + thick_picks
    
    for pt in walls_to_break:
        pt.setAttribValue("tile_type", "breakable")