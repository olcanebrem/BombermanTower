# -*- coding: utf-8 -*-
# Houdini → Unity Level Exporter (INI v3.1 + JSON mirror)
# Integrated with GET_LEVEL_INFO parameters

import hou, os, json, datetime
from collections import defaultdict

# --------------------------
# 0) CONFIG / CONSTANTS
# --------------------------

# CELL_TYPES sözlüğü: tile_type (string) -> (ID, Symbol, Name, Passable, PrefabIndex, ExtraAttributes)
CELL_TYPES = {
    "empty":        (0, ".", "EMPTY",        True,  0, {}),
    "wall":         (1, "#", "WALL",         False, 1, {"health": "100"}),
    "floor":        (2, "o", "FLOOR",        True,  2, {}),
    "spawn":        (3, "S", "SPAWN",        True,  3, {"spawn_type": "player"}),
    "enemy_spawn":  (4, "E", "ENEMY_SPAWN",  True,  4, {"enemy_type": "goblin"}),
    "collectible":  (5, "C", "COLLECTIBLE",  True,  5, {"item_type": "coin", "value": "10"}),
    "exit":         (6, "X", "EXIT",         True,  6, {}),
    "trap":         (7, "T", "TRAP",         True,  7, {"damage": "25"}),
}

# Symbol fallback (tile_type tanınmazsa . koy)
FALLBACK_SYMBOL = "."

# --------------------------
# 1) HELPERS
# --------------------------

def parm_or_default(node, name, default):
    try:
        p = node.parm(name)
        return p.eval() if p is not None else default
    except:
        return default

def now_iso():
    return datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

def build_grid_ascii(geo):
    """
    Point grid'i integer X,Z üzerinden ASCII satırlarına çevirir.
    Beklenti:
      - @P.x, @P.z integer grid (veya çok yakın)
      - string point attrib: "tile_type"
    """
    pts = geo.points()
    if not pts:
        return [], 0, 0, (0,0), (0,0)

    # XZ int rounding
    xs, zs = [], []
    tiles = {}
    for pt in pts:
        P = pt.position()
        x = int(round(P[0]))
        z = int(round(P[2]))
        xs.append(x); zs.append(z)

    minx, maxx = min(xs), max(xs)
    minz, maxz = min(zs), max(zs)

    width  = maxx - minx + 1
    height = maxz - minz + 1

    # hızlı lookup: (x,z) -> symbol
    for pt in pts:
        P = pt.position()
        x = int(round(P[0])); z = int(round(P[2]))
        tt = pt.stringAttribValue("tile_type") if pt.attribValue("tile_type") is not None else "empty"
        cell_def = CELL_TYPES.get(tt, None)
        symbol = cell_def[1] if cell_def else FALLBACK_SYMBOL
        tiles[(x,z)] = symbol

    # Row order: üstten alta (z min → z max)
    lines = []
    for z in range(minz, maxz + 1):
        row_chars = []
        for x in range(minx, maxx + 1):
            row_chars.append( tiles.get((x,z), FALLBACK_SYMBOL) )
        lines.append("".join(row_chars))

    return lines, width, height, (minx, minz), (maxx, maxz)

def write_text(path, content):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)

def write_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, indent=2, ensure_ascii=False)

def kv(line_key, line_val):
    return f"{line_key}={line_val}\n"

def join_attrs(attrs_dict):
    # dict -> attr1:val1,attr2:val2
    if not attrs_dict:
        return ""
    parts = [f"{k}:{v}" for k, v in attrs_dict.items()]
    return "," + ",".join(parts)

# --------------------------
# 2) MAIN EXPORT
# --------------------------

def export_level():
    """
    Bu node'daki parametreleri kullanarak level export et
    """
    node = hou.pwd()
    geo = node.geometry()
    
    # Parametreleri oku
    level_name = parm_or_default(node, "level_name", "Tutorial Level")
    level_tag = parm_or_default(node, "level_tag", "LEVEL_001_Tutorial")
    version = parm_or_default(node, "level_version", "1.0.0")
    export_dir = parm_or_default(node, "export_dir", "$HIP/export")
    skybox = parm_or_default(node, "skybox", "forest")
    source_commit = parm_or_default(node, "source_commit", "unknown")
    generated_by = parm_or_default(node, "generated_by", "houdini_level_exporter_v1")
    include_eval_placeholders = parm_or_default(node, "include_eval_placeholders", True)
    include_training_placeholders = parm_or_default(node, "include_training_placeholders", True)
    
    # Export directory'yi expand et
    export_dir = hou.expandString(export_dir)
    
    grid_lines, width, height, (minx, minz), (maxx, maxz) = build_grid_ascii(geo)

    # Controller parametreleri (bu node'dan al)
ctrl = {
    "HOUDINI_SEED":      str(parm_or_default(node, "seed", 12345)),
    "NOISE_SCALE":       str(parm_or_default(node, "noise_scale", 0.25)),
    "ENEMY_DENSITY":     str(parm_or_default(node, "enemy_density", 0.3)),
    "TRAP_DENSITY":      str(parm_or_default(node, "trap_density", 0.0)),
    "BOSS_ENABLED":      str(bool(parm_or_default(node, "boss_enabled", False))).lower(),
    "BOSS_TYPE":         str(parm_or_default(node, "boss_type", "none")),
    "DIFFICULTY":        str(parm_or_default(node, "difficulty", 1)),
    "CURRICULUM_STAGE":  str(parm_or_default(node, "curriculum_stage", 0)),
    "TRAINING_TAGS":     str(parm_or_default(node, "training_tags", "")),
    "ENEMY_DENSITY_RANGE": str(parm_or_default(node, "enemy_density_range", "")),
    "TRAP_DENSITY_RANGE":  str(parm_or_default(node, "trap_density_range", "")),
}

# Geometry’den detail attributeları da ekle
for attrib in geo.globalAttribs():
    name = attrib.name()
    if name not in ctrl:  # aynı isim varsa parametreyi ezmesin
        val = geo.attribValue(name)
        ctrl[name.upper()] = str(val)


    # Visual
    visual = {
        "SKYBOX": skybox
    }

    # Evaluation placeholders (Unity dolduracak)
    eval_metrics = {
        "PLAY_COUNT": "0",
        "SUCCESS_RATE": "0.0",
        "AVG_COMPLETION_TIME": "0.0",
        "DEATH_RATE": "0.0",
        "AVG_COLLECTIBLES": "0.0",
        "AGENT_STRATEGY": ""
    } if include_eval_placeholders else {}

    # Training placeholders
    training = {
        "CURRICULUM_STAGE": ctrl.get("CURRICULUM_STAGE", "0"),
        "TRAINING_TAGS": ctrl.get("TRAINING_TAGS", "")
    } if include_training_placeholders else {}

    # ------------ INI BUILD (v3.1) ------------
    ini_lines = []
    ini_lines.append("# ========================================================\n")
    ini_lines.append("# Unity Level Data v3.1\n")
    ini_lines.append("# Generator: Houdini 20.0.547\n")
    ini_lines.append("# Export Date: %s\n" % now_iso())
    ini_lines.append("# ========================================================\n")
    ini_lines.append("# Format: CELL_TYPES, LEVEL_xxx, GRID_ASCII, CONTROLLER PARAMETERS, VISUAL SETTINGS\n")
    ini_lines.append("# ========================================================\n\n\n")

    # CELL_TYPES (global)
    ini_lines.append("[CELL_TYPES]\n")
    ini_lines.append("# ID=Symbol,Name,Passable,Prefab_Index,Attributes\n")
    sorted_cell_defs = sorted(CELL_TYPES.items(), key=lambda kv: kv[1][0])  # by ID
    for tname, (cid, sym, cname, passable, prefab, extra) in sorted_cell_defs:
        ini_lines.append(f"{cid}={sym},{cname},{str(passable).lower()},{prefab}{join_attrs(extra)}\n")
    ini_lines.append("\n\n")

    # LEVEL SECTION
    ini_lines.append("# ========================================================\n")
    ini_lines.append(f"# {level_tag} : {level_name}\n")
    ini_lines.append("# ========================================================\n")
    ini_lines.append(f"[{level_tag}]\n")
    ini_lines.append(kv("VERSION", version))
    ini_lines.append(kv("SOURCE_COMMIT", source_commit))
    ini_lines.append(kv("GENERATED_BY", generated_by))
    ini_lines.append(kv("LEVEL_NAME", level_name))
    ini_lines.append(kv("GRID_WIDTH", width))
    ini_lines.append(kv("GRID_HEIGHT", height))
    ini_lines.append(kv("CELL_SIZE", parm_or_default(node, "cell_size", 1.0)))

    # Controller params
    ini_lines.append("\n# === CONTROLLER PARAMETERS ===\n")
    for k, v in ctrl.items():
        ini_lines.append(kv(k, v if v is not None else ""))

    # Training
    if training:
        ini_lines.append("\n# === TRAINING HINTS ===\n")
        for k, v in training.items():
            ini_lines.append(kv(k, v))

    # Visual
    ini_lines.append("\n# === VISUAL SETTINGS ===\n")
    for k, v in visual.items():
        ini_lines.append(kv(k, v))

    # Evaluation
    if eval_metrics:
        ini_lines.append("\n# === EVALUATION METRICS (Unity fills) ===\n")
        for k, v in eval_metrics.items():
            ini_lines.append(kv(k, v))

    # Grid
    ini_lines.append("\n[GRID_ASCII]\n")
    for line in grid_lines:
        ini_lines.append(line + "\n")

    ini_text = "".join(ini_lines)

    # ------------ JSON MIRROR ------------
    json_obj = {
        "meta": {
            "format_version": "3.1",
            "export_date": now_iso(),
            "generator": "Houdini 20.0.547",
        },
        "cell_types": [
            {
                "id": cid,
                "symbol": sym,
                "name": cname,
                "passable": passable,
                "prefab_index": prefab,
                "attributes": extra
            }
            for _, (cid, sym, cname, passable, prefab, extra) in sorted_cell_defs
        ],
        "level": {
            "tag": level_tag,
            "version": version,
            "source_commit": source_commit,
            "generated_by": generated_by,
            "name": level_name,
            "grid_width": width,
            "grid_height": height,
            "cell_size": float(parm_or_default(node, "cell_size", 1.0)),
            "controller_parameters": ctrl,
            "training_hints": training,
            "visual_settings": visual,
            "evaluation_metrics": eval_metrics,
            "grid_ascii": grid_lines
        }
    }

    # ------------ WRITE FILES ------------
    safe_tag = level_tag.lower()
    ini_path  = os.path.join(export_dir, f"{safe_tag}.ini")
    json_path = os.path.join(export_dir, f"{safe_tag}.json")

    write_text(ini_path, ini_text)
    write_json(json_path, json_obj)

    hou.ui.displayMessage(f"Level exported successfully!\n\nINI:  {ini_path}\nJSON: {json_path}")
    
    return ini_path, json_path

# --------------------------
# 3) ENTRY POINT
# --------------------------

# Bu script Python SOP içinde çalıştığında geometry boş döndür
# Export sadece button'a basıldığında yapılır
node = hou.pwd()
geo = node.geometry()
geo.clear()
