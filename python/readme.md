# Bomberman PPO RL Projesi

Unity ile Bomberman tarzı oyun geliştirip, oyuncuyu **PPO (Proximal Policy Optimization)** ile eğiten makine öğrenmesi projesi.

## 🎯 Proje Özeti

- **Unity Environment**: 8 yönlü hareket, bomba yerleştirme, düşmanlar, toplanabilir öğeler
- **PPO Agent**: Oyuncu karakterinin davranışlarını öğrenir
- **Procedural Generation**: Rastgele level üretimi
- **Curriculum Learning**: Zorluk seviyesi kademeli artırım
- **Comprehensive Monitoring**: Detaylı performans takibi

## 📁 Proje Yapısı

```
bomberman-ppo/
├── Unity/                          # Unity projesi
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── PlayerAgent.cs      # ML-Agent player kontrolü
│   │   │   ├── EnvManager.cs       # Çevre yönetimi
│   │   │   ├── RewardSystem.cs     # Ödül sistemi
│   │   │   ├── BombController.cs   # Bomba mantığı
│   │   │   └── ...
│   │   ├── Prefabs/               # Oyun objeleri
│   │   └── Scenes/                # Unity sahneleri
│   └── ProjectSettings/
├── Python/                         # Python eğitim kodu
│   ├── train.py                   # Ana eğitim scripti
│   ├── evaluate.py                # Model değerlendirme
│   ├── configs/
│   │   └── ppo_config.yaml        # Hyperparameter ayarları
│   ├── utils/
│   │   ├── wrappers.py            # Environment wrapper'ları
│   │   ├── callbacks.py           # Eğitim callback'leri
│   │   └── logger.py              # Logging utilities
│   └── requirements.txt           # Python bağımlılıkları
├── models/                        # Kaydedilen modeller
├── logs/                          # TensorBoard logları
├── checkpoints/                   # Eğitim checkpoint'leri
└── README.md
```

## 🚀 Kurulum

### Unity Kurulumu

1. **Unity Hub ve Unity Editor yükleyin** (2022.3 LTS önerilen)

2. **ML-Agents paketini ekleyin**:
   ```
   Window > Package Manager > Add package by name:
   com.unity.ml-agents
   ```

3. **Proje dosyalarını Unity'ye ekleyin**:
   - Scriptleri `Assets/Scripts/` klasörüne kopyalayın
   - Prefab'ları oluşturun ve ayarlayın

### Python Kurulumu

1. **Python 3.8+ kurulumunu doğrulayın**:
   ```bash
   python --version
   ```

2. **Gerekli paketleri yükleyin**:
   ```bash
   pip install -r requirements.txt
   ```

3. **Requirements.txt içeriği**:
   ```
   mlagents==0.30.0
   stable-baselines3==2.0.0
   gym-unity==0.28.0
   torch>=1.8.0
   tensorboard>=2.8.0
   matplotlib>=3.5.0
   seaborn>=0.11.0
   pyyaml>=6.0
   numpy>=1.21.0
   opencv-python>=4.5.0
   ```

## 🎮 Unity Environment Kurulumu

### 1. Scene Hazırlığı

```csharp
// Temel GameObject'leri oluşturun:
- GameManager (EnvManager script ekleyin)
- Player (PlayerAgent + RewardSystem scriptleri)
- Prefabs klasörüne: Wall, BreakableWall, Enemy, Collectible, Bomb, Exit
```

### 2. LayerMask Ayarları

```
Layers:
- Default (0): Genel objeler
- Wall (8): Duvarlar (hareket engeli)
- Enemy (9): Düşmanlar
- Player (10): Oyuncu
- Collectible (11): Toplanabilir öğeler
- Bomb (12): Bombalar
```

### 3. ML-Agents Behavior Parameters

```yaml
# PlayerAgent için Behavior Parameters:
Behavior Name: BombermanPlayer
Vector Observation: 200+ (grid observations + distances + status)
Actions:
  - Discrete Branch 1: Movement (9 actions: 8 directions + no movement)
  - Discrete Branch 2: Bomb (2 actions: place bomb or not)
Model: (eğitim sonrası atanacak)
```

## 🧠 Eğitim Süreci

### 1. Konfigürasyon Ayarlama

`configs/ppo_config.yaml` dosyasını ihtiyaçlarınıza göre düzenleyin:

```yaml
# Temel ayarlar
training:
  total_timesteps: 2000000
  n_envs: 8
  device: "auto"

# PPO hiperparametreleri
ppo:
  learning_rate: 3.0e-4
  n_steps: 2048
  batch_size: 64
  gamma: 0.99
```

### 2. Eğitimi Başlatma

```bash
# Temel eğitim
python train.py --config configs/ppo_config.yaml

# Varolan modelden devam etme
python train.py --config configs/ppo_config.yaml --resume models/bomberman_ppo_checkpoint_1000000

# Özel ayarlarla eğitim
python train.py --config configs/ppo_config.yaml --total-timesteps 5000000
```

### 3. Eğitimi İzleme

```bash
# TensorBoard'u başlatın
tensorboard --logdir logs/

# Browser'da açın: http://localhost:6006
```

## 📊 Model Değerlendirme

### Temel Değerlendirme

```bash
# Eğitilmiş modeli test etme
python evaluate.py --model models/bomberman_ppo_final.zip --episodes 100

# Görselleştirme ile test
python evaluate.py --model models/bomberman_ppo_final.zip --episodes 10 --render
```

### Model Karşılaştırma

```bash
# Birden fazla modeli karşılaştırma
python evaluate.py --compare models/model1.zip models/model2.zip models/model3.zip --episodes 50
```

### Benchmark Testi

```bash
# Farklı zorluk seviyelerinde test
python evaluate.py --model models/bomberman_ppo_final.zip --benchmark --episodes 20
```

## 🎯 Ödül Sistemi

### Pozitif Ödüller
- **Level Tamamlama**: +10.0
- **Düşman Öldürme**: +2.0
- **Toplanabilir Öğe**: +1.0 - +2.0
- **Duvar Kırma**: +0.2
- **Bomba Yerleştirme**: +0.1

### Negatif Cezalar
- **Ölüm**: -5.0
- **Hasar Alma**: -1.0
- **Duvara Çarpma**: -0.1
- **Hareketsizlik**: -0.02
- **Zaman Aşımı**: -2.0

### Mesafe Tabanlı Ödüller
- Düşmana yaklaşma: +0.01
- Çıkışa yaklaşma: +0.02
- Toplanabilir öğeye yaklaşma: +0.01

## 🎚️ Curriculum Learning

Eğitim zorluk seviyeleri kademeli olarak artar:

1. **Beginner**: 1 düşman, küçük harita (11x11)
2. **Intermediate**: 2 düşman, orta harita (13x13)
3. **Advanced**: 3 düşman, büyük harita (15x15)
4. **Expert**: 4 düşman, maksimum zorluk

## 🔧 Troubleshooting

### Yaygın Sorunlar

1. **Unity ML-Agents bağlantı hatası**:
   ```
   Çözüm: Unity'de Play modunda olduğunuzdan emin olun
   Port çakışması varsa worker_id değiştirin
   ```

2. **Python import hatası**:
   ```bash
   pip install --upgrade mlagents
   pip install --upgrade stable-baselines3
   ```

3. **CUDA/GPU sorunları**:
   ```yaml
   # config dosyasında:
   training:
     device: "cpu"  # GPU yerine CPU kullan
   ```

4. **Memory hatası**:
   ```yaml
   # Batch size'ı küçültün:
   ppo:
     batch_size: 32  # 64 yerine
     n_steps: 1024   # 2048 yerine
   ```

### Debug Modunda Çalıştırma

```bash
# Verbose logging ile
python train.py --config configs/ppo_config.yaml --verbose

# Tek environment ile test
python train.py --config configs/ppo_config.yaml --n-envs 1 --no-graphics false
```

## 📈 Performans Optimizasyonu

### Unity Optimizasyonu
- Graphics Quality: Fast/Fastest
- VSync: Disabled
- Target Frame Rate: 60
- No Graphics mode: Training için aktif

### Python Optimizasyonu
- Multi-environment: 4-8 parallel env
- Buffer size: GPU memory'ye göre ayarlayın
- Checkpoint frequency: Disk I/O'yu dengeyin

## 🚀 Gelişmiş Özellikler

### Multi-Agent Training
```yaml
# Gelecek sürümde:
multi_agent:
  enabled: true
  n_agents: 2
  competitive: true
```

### Self-Play
```yaml
experimental:
  self_play: true
  opponent_pool_size: 5
```

### Hyperparameter Tuning
```bash
# Optuna ile otomatik hyperparameter tuning
python tune_hyperparams.py --config configs/tuning_config.yaml
```

## 📚 Kaynaklar

- [Unity ML-Agents Documentation](https://github.com/Unity-Technologies/ml-agents)
- [Stable-Baselines3 Documentation](https://stable-baselines3.readthedocs.io/)
- [PPO Paper](https://arxiv.org/abs/1707.06347)
- [Curriculum Learning](https://arxiv.org/abs/1904.03733)

## 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Commit yapın (`git commit -m 'Add amazing feature'`)
4. Push yapın (`git push origin feature/amazing-feature`)
5. Pull Request açın

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır. Detaylar için `LICENSE` dosyasına bakın.

## ⚡ Hızlı Başlangıç

```bash
# 1. Repository'yi klonlayın
git clone <repo-url>
cd bomberman-ppo

# 2. Python bağımlılıklarını yükleyin
pip install -r requirements.txt

# 3. Unity'de projeyi açın ve Play yapın

# 4. Eğitimi başlatın
python train.py

# 5. TensorBoard ile izleyin
tensorboard --logdir logs/
```

Başarılı eğitimler! 🎮🤖