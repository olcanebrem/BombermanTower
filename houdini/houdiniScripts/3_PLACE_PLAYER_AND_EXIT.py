import hou
import random
from collections import deque, defaultdict

node = hou.pwd()
geo = node.geometry()

# --- Controller parametreleri ---
controller = hou.node("../CONTROLLER")
seed = controller.parm("seed").eval()
min_dist_param = controller.parm("min_player_exit_dist").eval()
random.seed(seed + 1)

# --- Empty tile noktalarını ve mapping ---
traversable_pts = [pt for pt in geo.points() if pt.stringAttribValue("tile_type") in ("empty","player","stairs")]
pt_num_to_idx = {pt.number(): idx for idx, pt in enumerate(traversable_pts)}
idx_to_pt = {idx: pt for idx, pt in enumerate(traversable_pts)}
num_pts = len(traversable_pts)

# --- Neighbor map (wall’ları atla) ---
neighbors_map = []
for pt in traversable_pts:
    neighbors_idx = []
    for nidx in pt.intListAttribValue("neighbours"):
        if nidx in pt_num_to_idx:
            neighbors_idx.append(pt_num_to_idx[nidx])
    neighbors_map.append(neighbors_idx)

# --- Distance map oluştur (BFS) ---
dist_map = [[float('inf')]*num_pts for _ in range(num_pts)]

for i in range(num_pts):
    queue = deque([i])
    dist_map[i][i] = 0
    while queue:
        current = queue.popleft()
        for n in neighbors_map[current]:
            if dist_map[i][n] == float('inf'):
                dist_map[i][n] = dist_map[i][current] + 1
                queue.append(n)

# --- Maksimum mesafeyi bul ---
max_dist = 0
best_pair = (0,0)
for i in range(num_pts):
    for j in range(i+1, num_pts):
        if dist_map[i][j] > max_dist and dist_map[i][j] < float('inf'):
            max_dist = dist_map[i][j]
            best_pair = (i,j)

# --- min_player_exit_dist parametresine göre uygun pair seç ---
target_dist = min(min_dist_param, max_dist)
candidates = []

for i in range(num_pts):
    for j in range(i+1, num_pts):
        if abs(dist_map[i][j] - target_dist) <= 1:  # tolerans 1 birim
            candidates.append( (i,j) )

if candidates:
    player_idx, exit_idx = random.choice(candidates)
else:
    player_idx, exit_idx = best_pair

player_pt = idx_to_pt[player_idx]
exit_pt = idx_to_pt[exit_idx]

# --- tile_type güncelle ---
player_pt.setAttribValue("tile_type","player")
exit_pt.setAttribValue("tile_type","stairs")

print(f"Player ve Exit noktaları yerleştirildi: mesafe={dist_map[player_idx][exit_idx]}")
