import hou
import random
from collections import deque

node = hou.pwd()
geo = node.geometry()

# --- 1. CONTROLLER parametreleri ---
try:
    controller = hou.node("../CONTROLLER")
    seed = controller.parm("seed").eval()
    target_param = controller.parm("min_player_exit_dist").eval()  # 0..1 arası normalize parametre
    random.seed(seed + 1)
except AttributeError:
    print("UYARI: CONTROLLER veya parametreler bulunamadı. Varsayılanlar kullanılıyor.")
    target_param = 0.5  # default olarak orta mesafe

# --- 2. Empty tile noktalarını topla ---
empty_pts = [pt for pt in geo.points() if pt.stringAttribValue("tile_type") == "empty"]
if len(empty_pts) < 2:
    raise hou.NodeError("Boş alan yetersiz")

# --- 3. Komşuları belirle (8 yönlü grid) ---
def get_neighbors_dict(points):
    pos_dict = {pt.number(): pt.position() for pt in points}
    neighbors = {}
    for pt in points:
        pnum = pt.number()
        x, y, z = pt.position()
        neighbor_ids = []
        offsets = [(1,0,0), (-1,0,0), (0,0,1), (0,0,-1),
                   (1,0,1), (-1,0,1), (1,0,-1), (-1,0,-1)]
        for dx, dy, dz in offsets:
            nx, ny, nz = x+dx, y+dy, z+dz
            for other_num, other_pos in pos_dict.items():
                if other_num == pnum:
                    continue
                if abs(other_pos[0]-nx)<0.1 and abs(other_pos[2]-nz)<0.1 and abs(other_pos[1]-ny)<0.1:
                    neighbor_ids.append(other_num)
                    break
        neighbors[pnum] = neighbor_ids
    return neighbors

neighbors_dict = get_neighbors_dict(empty_pts)

# --- 4. BFS path bulma ---
def find_path_bfs(start_id, end_id, neighbors):
    queue = deque([start_id])
    came_from = {start_id: None}
    while queue:
        current = queue.popleft()
        if current == end_id:
            break
        for n in neighbors.get(current, []):
            if n not in came_from:
                came_from[n] = current
                queue.append(n)
    if end_id not in came_from:
        return []
    path = []
    cur = end_id
    while cur is not None:
        path.append(cur)
        cur = came_from[cur]
    path.reverse()
    return path

# --- 5. Maksimum path uzunluğunu bul ---
max_length = 0
max_pair = None
for i in range(len(empty_pts)):
    for j in range(i+1, len(empty_pts)):
        path_ids = find_path_bfs(empty_pts[i].number(), empty_pts[j].number(), neighbors_dict)
        if len(path_ids) > max_length:
            max_length = len(path_ids)
            max_pair = (empty_pts[i], empty_pts[j])

if not max_pair:
    raise hou.NodeError("Max path bulunamadı!")

# --- 6. Target mesafeye uygun path uzunluğu ---
# min_player_exit_dist parametresi controller’dan
min_player_exit_dist = controller.parm("min_player_exit_dist").eval()

# max_length bulundu
target_steps = max(2, min(int(min_player_exit_dist), max_length))

found_pair = False
attempt_limit = 500

for _ in range(attempt_limit):
    p1, p2 = random.sample(empty_pts, 2)
    path_ids = find_path_bfs(p1.number(), p2.number(), neighbors_dict)
    if len(path_ids)-1 == target_steps:
        p1.setAttribValue("tile_type", "player")
        p2.setAttribValue("tile_type", "stairs")
        found_pair = True
        print(f"Player–Stairs path steps: {len(path_ids)-1}")
        break

# Eğer tam hedef bulunamazsa, en yakın path uzunluğunu kullan
if not found_pair:
    closest_diff = 1e6
    best_pair = None
    for i in range(len(empty_pts)):
        for j in range(i+1, len(empty_pts)):
            path_ids = find_path_bfs(empty_pts[i].number(), empty_pts[j].number(), neighbors_dict)
            diff = abs((len(path_ids)-1) - target_steps)
            if diff < closest_diff:
                closest_diff = diff
                best_pair = (empty_pts[i], empty_pts[j])
    if best_pair:
        best_pair[0].setAttribValue("tile_type", "player")
        best_pair[1].setAttribValue("tile_type", "stairs")
        print(f"Closest Player–Stairs path steps: {len(find_path_bfs(best_pair[0].number(), best_pair[1].number(), neighbors_dict))-1}")
