# 6_CREATE_INTERACTABLES.py (Advanced Loot System with Flexible Ratios)
import hou
import random

node = hou.pwd()
geo = node.geometry()

# --- 1. KONTROL PANELİNİ BUL VE PARAMETRELERİ OKU ---
try:
    controller = hou.node("../CONTROLLER")
    seed = controller.parm("seed").evalAsInt()
    loot_density = controller.parm("loot_density").evalAsFloat()  # Ana loot density
    coin_density = controller.parm("coin_density").evalAsFloat()
    health_density = controller.parm("health_density").evalAsFloat()
    breakable_density = controller.parm("breakable_density").evalAsFloat()
    edge_wall_bias = controller.parm("edge_wall_bias").evalAsFloat()
    
    random.seed(seed + 3)

except (AttributeError, TypeError):
    print("UYARI: CONTROLLER veya gerekli parametreler bulunamadı. Varsayılan değerler kullanılıyor.")
    loot_density, coin_density, health_density, breakable_density, edge_wall_bias = 1.0, 0.5, 0.25, 0.3, 1.0

# --- 2. SOYUT LOOT SİSTEMİ (GELECEKTEKİ ELEMANLAR İÇİN HAZIR) ---
class LootSystem:
    """
    Gelecekte yeni loot türleri eklemek için soyut loot sistemi.
    Örnekler: powerup, trap, key, bomb_upgrade, speed_boost vb.
    """
    
    def __init__(self):
        self.loot_types = {}
        self.total_ratio = 0
    
    def add_loot_type(self, name, ratio, tile_type):
        """Yeni loot türü ekle
        name: loot türünün adı (coin, health, powerup vs.)
        ratio: nispi oran (2.0 = 2x daha sık)
        tile_type: geometrideki tile_type attribute'u
        """
        self.loot_types[name] = {
            'ratio': ratio,
            'tile_type': tile_type,
            'normalized_ratio': 0  # Hesaplanacak
        }
        self._update_ratios()
    
    def _update_ratios(self):
        """Oranları normalize et"""
        self.total_ratio = sum(loot['ratio'] for loot in self.loot_types.values())
        for loot in self.loot_types.values():
            loot['normalized_ratio'] = loot['ratio'] / self.total_ratio if self.total_ratio > 0 else 0
    
    def get_loot_counts(self, total_points, individual_densities, global_density):
        """Her loot türü için kaç tane yerleştirileceğini hesapla"""
        counts = {}
        
        for name, loot_data in self.loot_types.items():
            # Global density ve individual density'yi birleştir
            individual_density = individual_densities.get(name, 0.5)
            effective_density = individual_density * (global_density * 0.1)
            
            # Bu loot türü için count hesapla
            count = int(total_points * effective_density)
            counts[name] = {
                'count': count,
                'tile_type': loot_data['tile_type'],
                'ratio': loot_data['ratio']
            }
        
        return counts
    
    def place_loot_with_ratios(self, available_points, loot_counts):
        """Loot'ları ratio'larına göre yerleştir"""
        results = {}
        points_copy = available_points.copy()
        random.shuffle(points_copy)
        
        # Her loot türü için yerleştir
        for name, data in loot_counts.items():
            count = data['count']
            tile_type = data['tile_type']
            placed = 0
            
            # Mevcut noktalardan yerleştir
            for i in range(min(count, len(points_copy))):
                if len(points_copy) > 0:
                    pt = points_copy.pop()
                    pt.setAttribValue("tile_type", tile_type)
                    placed += 1
            
            results[name] = placed
        
        return results

# --- 3. LOOT SİSTEMİNİ BAŞLAT ---
loot_system = LootSystem()

# Mevcut loot türlerini ekle (2x coin ratio ile)
loot_system.add_loot_type("coin", 2.0, "coin")
loot_system.add_loot_type("health", 1.0, "health")

# Gelecek için hazır - yeni türler kolayca eklenebilir:
# loot_system.add_loot_type("powerup", 0.5, "powerup")
# loot_system.add_loot_type("trap", 0.3, "trap")
# loot_system.add_loot_type("key", 0.1, "key")

print("Loot System initialized with ratios:")
for name, data in loot_system.loot_types.items():
    print(f"  {name}: ratio={data['ratio']}, normalized={data['normalized_ratio']:.2f}")

# --- 4. LOOT YERLEŞTİRME ---
# Boş alanları bul
empty_points = []
for pt in geo.points():
    tile_type = pt.stringAttribValue("tile_type")
    if tile_type == "empty":
        empty_points.append(pt)

print(f"\nFound {len(empty_points)} empty points for loot placement")

if loot_density > 0 and len(empty_points) > 0:
    # Individual densities
    individual_densities = {
        "coin": coin_density,
        "health": health_density
    }
    
    # Her loot türü için count'ları hesapla
    loot_counts = loot_system.get_loot_counts(
        len(empty_points), 
        individual_densities, 
        loot_density
    )
    
    print(f"\nLoot placement plan (loot_density={loot_density}):")
    for name, data in loot_counts.items():
        print(f"  {name}: {data['count']} planned")
    
    # Loot'ları yerleştir
    results = loot_system.place_loot_with_ratios(empty_points, loot_counts)
    
    print(f"\nLoot placement results:")
    for name, placed in results.items():
        ratio = loot_system.loot_types[name]['ratio']
        print(f"  {name}: {placed} placed (ratio={ratio}x)")

# --- 5. KIRILABİLİR DUVARLARI YERLEŞTİRME (AYNI KALIYOR) ---
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
                thick_wall_candidates.append(pt)

    # Toplamda kaç tane kırılabilir duvar oluşturulacağını hesapla.
    total_candidates = len(edge_wall_candidates) + len(thick_wall_candidates)
    total_to_convert = int(total_candidates * (breakable_density * 0.2))

    # Her listeden kaç tane seçeceğimizi hesapla.
    num_from_edge = int(total_to_convert * edge_wall_bias)
    num_from_thick = total_to_convert - num_from_edge

    # Güvenlik kontrolü
    num_from_edge = min(num_from_edge, len(edge_wall_candidates))
    num_from_thick = min(num_from_thick, len(thick_wall_candidates))

    # Rastgele seçim yap.
    edge_picks = []
    if num_from_edge > 0:
        edge_picks = random.sample(edge_wall_candidates, num_from_edge)
        
    thick_picks = []
    if num_from_thick > 0:
        thick_picks = random.sample(thick_wall_candidates, num_from_thick)

    # Seçimleri birleştir ve yerleştir.
    walls_to_break = edge_picks + thick_picks
    
    breakables_placed = 0
    for pt in walls_to_break:
        pt.setAttribValue("tile_type", "breakable")
        breakables_placed += 1
    
    print(f"\nPlaced {breakables_placed} breakable walls")

print("\n✓ 6_CREATE_INTERACTABLES completed with advanced loot system!")
