flowchart LR
  %% ============ HOUDINI ============ 
  subgraph H[Houdini (Prosedürel Üretim + Export)]
    direction LR
    A1[1_INITIALIZE_MAP.py]
    A2[2_CARVE_ROOMS.py]
    A3[2_5_CONNECT_ROOMS.py]
    A4[2_6_CLEANUP.py]
    A5[3_PLACE_PLAYER_AND_EXIT.py]
    A6[3_5_GUARANTEE_PATH.py]
    A7[4_VISUALIZE_MAP.py]
    A8[5_PLACE_ENEMIES.py]
    A9[6_CREATE_INTERACTABLES.py]

    A1 --> A2 --> A3 --> A4 --> A5 --> A6 --> A7 --> A8 --> A9

    B1[GET_LVL_INFO.py<br/>- ASCII GRID üret<br/>- CELL_TYPES ve meta yaz<br/>- Controller/Training/Visual parametreleri yaz]
    A9 --> B1
    B2[LEVEL_XXXX_vA.B.C_vX.Y.ini/.txt<br/>(Unity/Assets/Levels)]
    B1 --> B2

    %% Alternatif/deneysel exporter'lar
    X1 -.-> B1
    X2 -.-> B1
    X1[Houdini/scripts/my_exporter.py]
    X2[Houdini/scripts/level_exporter.py]
  end

  %% ============ UNITY ============ 
  subgraph U[Unity (Oyun + ML-Agents)]
    direction TB
    C1[LevelLoader<br/>ScanForLevelFiles()]
    C2[LevelLoader<br/>SelectLevel + LoadSelectedLevel()]
    C3[HoudiniLevelImporter<br/>ImportFromText(INI/ASCII)]
    C4[LevelLoader<br/>CreateMapVisual()<br/>- Tile prefabs<br/>- Player/Exit/Enemies/Collectibles]
    C5[LevelManager<br/>- Level state/reset/next/random]
    C6[EnvManager]
    C7[PlayerAgent]
    C8[RewardSystem]
    C9[MLAgentsTrainingController<br/>python -m mlagents-learn ...]

    C1 --> C2 --> C3 --> C4 --> C5
    %% ML-Agents runtime etkileşimleri
    C6 <--> C7
    C7 <--> C8
    C6 -. world/grid & sorgular .-> C1
    C6 -. reset & durum .-> C5
  end

  %% ============ PYTHON ============ 
  subgraph P[Python (Eğitim/Analiz)]
    direction TB
    P1[mlagents-learn<br/>(ML-Agents CLI)]
    P2[mlagents_results_parser.py<br/>(Eğitim çıktısı ayrıştırma)]
    P3[evaluate.py (opsiyonel)<br/>SB3 PPO değerlendirme/rapor]

    P1 --> P2
    P3
  end

  %% Akış bağlantıları
  B2 --> C1
  C9 --> P1
