# Bomberman Tower

Unity tabanlı AI eğitim ortamı. Turn-based Bomberman mekaniği ile procedurel level generation ve ML-Agents entegrasyonu sunar.

## 🎯 Proje Amacı

Bu proje, ML-Agents kullanarak AI'ların turn-based Bomberman oyununda strateji geliştirmelerini sağlayan bir eğitim ortamıdır. Houdini ile procedurel level generation ve gelişmiş reward sistemi içerir.

## 📁 Proje Yapısı

```
BombermanTower/
│
├── Unity/                   # 🎮 Unity Ana Projesi
│   ├── Assets/Scripts/      #   Oyun kodları
│   │   ├── ML-Agent/        #   ML-Agents PlayerAgent, EnvManager, RewardSystem
│   │   ├── Managers/        #   GameManager, LevelManager, TurnManager
│   │   ├── Tiles/           #   Tile sistem (BombTile, EnemyTile, vb.)
│   │   └── Interfaces/      #   ITurnBased, IGameAction interfaceleri
│   ├── Assets/Levels/       #   .ini formatında level dosyaları
│   └── Assets/Prefabs/      #   Oyun objeleri
│
├── Houdini/                 # 🧙‍♂️ Procedurel Level Generation
│   ├── main_leveldesign.hip # Ana Houdini sahne dosyası
│   ├── houdiniScripts/      # Python level generation scriptleri
│   │   ├── 1_INITIALIZE_MAP.py
│   │   ├── 2_CARVE_ROOMS.py
│   │   └── 3_PLACE_ENEMIES.py
│   └── export/              # Export edilen level verileri
│
└── Python/                  # 🐍 ML Training Pipeline
    ├── train.py             # PPO eğitim scripti
    ├── evaluate.py          # Model değerlendirme
    ├── ppo.yml             # Hyperparameter konfigürasyonu
    └── requirements.txt     # Python dependencies
```

## 🚀 Özellikler

### ML-Agents Entegrasyonu
- **PlayerAgent**: Turn-based sistem ile entegre ML-Agent
- **EnvManager**: Episod yönetimi ve reset işlemleri
- **RewardSystem**: Gelişmiş ödül sistemi (exploration, combat, objective completion)
- **PPO Algorithm**: Proximal Policy Optimization ile eğitim

### Turn-Based Sistem
- **ITurnBased Interface**: Tüm oyun objeleri için standart turn sistemi
- **IGameAction Pattern**: Move, Attack, PlaceBomb, Shoot aksiyonları
- **TurnManager**: Sıra tabanlı oyun döngüsü yönetimi

### Procedurel Level Generation
- **Houdini Pipeline**: Python scriptleri ile otomatik level üretimi
- **Grid-Based System**: 15x15 grid üzerinde tile-based level tasarımı
- **Dynamic Difficulty**: Düşman yerleşimi ve collectable dağılımı

## 🛠️ Setup

### 1. Unity Setup
```bash
Unity 2022.3.x veya üstü
ML-Agents Package (2.0.x)
Houdini Engine for Unity
```

### 2. Python Dependencies
```bash
cd Python
pip install -r requirements.txt
```

### 3. Houdini Setup
```bash
Houdini 19.5+ 
Python 3.9+ environment
main_leveldesign.hip dosyasını aç
```

## 🎮 Nasıl Çalışır

### Training Pipeline
1. **Houdini** ile procedurel levellar üret
2. **Unity** ortamında ML-Agent'i başlat
3. **Python** script ile PPO eğitimini çalıştır
4. **TensorBoard** ile sonuçları izle

### Oyun Mekaniği
- Turn-based hareket sistemi
- Bomb placement ve explosion mekaniği
- Enemy AI ile combat
- Collectible sistemli scoring
- Multi-level progression

## 📊 Model Performance

- **Observation Space**: 127 dimensional vector
  - Grid observations (5x5 radius)
  - Distance calculations
  - Health, bomb count, enemy positions

- **Action Space**: Discrete(5)
  - 0: No action
  - 1: Move Up/Down/Left/Right
  - 2: Place Bomb
  - 3: Attack (melee)
  - 4: Shoot (ranged)

## 🔄 Recent Updates

- **v4.3**: Enhanced ML-Agent integration with cached observations
- **v4.2**: Optimized reward system for better exploration
- **v4.1**: Turn-based system refactoring
- **v4.0**: Complete ML-Agents integration
- **v3.8**: Initial Houdini pipeline implementation

## 🎯 Development Goals

- [ ] Multi-agent training environment
- [ ] Advanced difficulty scaling
- [ ] Real-time visualization tools
- [ ] Performance optimization
- [ ] Level variety expansion

## 📚 System Requirements

| Kategori | Gereksinim | Versiyon | Açıklama |
|----------|------------|----------|----------|
| **Unity** | Unity Editor | 2022.3.x LTS+ | Ana geliştirme ortamı |
| | ML-Agents Package | 2.0.x | Reinforcement Learning |
| | Houdini Engine for Unity | 19.5+ | Procedurel content entegrasyonu |
| **Houdini** | Houdini FX/Indie | 19.5+ | Level generation pipeline |
| | Python Environment | 3.9+ | Houdini script desteği |
| **Python** | Python | 3.9+ | ML training environment |
| | mlagents | 0.30.0 | Unity ML-Agents toolkit |
| | stable-baselines3 | 2.0.0 | RL algorithms |
| | PyTorch | 1.8.0+ | Deep learning framework |
| | TensorFlow | 2.6.0+ | ML backend support |
| | NumPy | 1.21.0+ | Numerical computing |
| | Matplotlib | 3.5.0+ | Visualization |
| **Hardware** | RAM | 8GB+ | Minimum sistem gereksinimi |
| | GPU | CUDA Compatible | Training acceleration (önerilen) |
| | Disk | 10GB+ | Proje + dependencies için alan |

## 📄 License

MIT License - Detaylar için LICENSE dosyasına bakın.