# Bomberman Tower

A 3D tower defense game with Bomberman-style mechanics, built with Unity and Houdini for procedural level generation.

## Project Structure

```
bombermanTower/
│
├── unity/                  # 🎮 Unity project
│   ├── Assets/              #   Game assets (models, textures, scripts, etc.)
│   ├── Packages/            #   Unity package dependencies
│   └── ProjectSettings/     #   Unity project settings
│
├── houdini/                 # 🧙‍♂️ Houdini Digital Assets (HDA)
│   ├── hda/                 # Houdini Digital Assets
│   │   └── level_generator.hda
│   ├── export/              # Exported assets from Houdini
│   │   └── geo_json/        # GeoJSON/FBX exports
│   └── scenes/              # Houdini scene files
│       └── test_level.hipnc
│
├── python/                  # 🐍 Python scripts
│   ├── train_model.py       # AI training scripts
│   ├── run_simulation.py    # Simulation scripts
│   └── utils/               # Utility scripts
│
└── docs/                    # 📚 Documentation
    ├── project_plan.md
    └── architecture.png
```

## Setup

1. **Unity Setup**
   - Open the project in Unity 2021.3 or later
   - Import required packages through Package Manager

2. **Houdini Setup**
   - Install Houdini 19.5 or later
   - Install Houdini Engine for Unity

3. **Python Dependencies**
   ```bash
   cd python
   pip install -r requirements.txt
   ```

## Development

- Unity scripts: `Assets/Scripts/`
- Houdini assets: `houdini/hda/`
- Python tools: `python/`

## License

MIT License - see LICENSE for details
