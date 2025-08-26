#!/usr/bin/env python3
"""
Bomberman ML-Agents Training - Simple Version
Bu script ML-Agents import sorunlarını bypass eder ve direct subprocess kullanır
"""

import os
import sys
import subprocess
import time
from datetime import datetime

def check_mlagents():
    """ML-Agents kurulum kontrolü"""
    print("🔍 ML-Agents kontrolü...")
    
    try:
        # Direct command test
        result = subprocess.run(
            ["python", "-m", "mlagents.trainers", "--help"],
            capture_output=True,
            text=True,
            timeout=10
        )
        
        if result.returncode == 0:
            print("✅ ML-Agents command line çalışıyor")
            return True
        else:
            print("❌ ML-Agents command line hatası")
            print(f"Error: {result.stderr}")
            return False
            
    except subprocess.TimeoutExpired:
        print("⏱️ ML-Agents timeout (yavaş ama çalışıyor olabilir)")
        return True
    except FileNotFoundError:
        print("❌ Python veya ML-Agents bulunamadı")
        return False
    except Exception as e:
        print(f"⚠️ Test hatası: {e}")
        return True  # Yine de deneyelim

def simple_train():
    """Basit training başlatma"""
    
    # Setup
    os.environ['PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION'] = 'python'
    
    print("🚀 Bomberman Tower Training")
    print("="*40)
    print(f"📁 Directory: {os.getcwd()}")
    
    # Config kontrol
    config_file = "config/bomberman_ppo_simple.yaml"
    if not os.path.exists(config_file):
        print(f"❌ Config dosyası bulunamadı: {config_file}")
        return False
    
    print(f"✅ Config dosyası: {config_file}")
    
    # Run ID
    run_id = f"simple_train_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    print(f"🆔 Run ID: {run_id}")
    
    # ML-Agents kontrol
    if not check_mlagents():
        print("\n💡 ML-Agents sorun varsa şu komutları dene:")
        print("   pip uninstall mlagents")
        print("   pip install mlagents==0.30.0")
        print("   # veya")
        print("   pip install torch==1.13.1")
        choice = input("Devam etmek ister misin? (y/n): ")
        if choice.lower() != 'y':
            return False
    
    # Training komutu
    cmd = [
        "python", "-m", "mlagents.trainers.learn",
        config_file,
        f"--run-id={run_id}",
        "--force"
    ]
    
    print(f"\n🚀 Training Başlatılıyor")
    print("-" * 30)
    print(f"Komut: {' '.join(cmd)}")
    print(f"")
    print(f"🎮 ŞİMDİ UNITY'DE PLAY'E BAS!")
    print(f"🔗 'Connected to Unity environment' mesajını bekle")
    print(f"⛔ Durdurmak için: Ctrl+C")
    print("-" * 30)
    
    input("Hazır olduğunda Enter'a bas...")
    
    try:
        # Training başlat
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            universal_newlines=True,
            bufsize=1
        )
        
        print(f"🟢 Training başladı (PID: {process.pid})")
        
        # Output'u göster
        for line in process.stdout:
            line = line.rstrip()
            if line:  # Boş satırları skip et
                # Önemli mesajları highlight et
                if "Connected to Unity" in line:
                    print(f"✅ {line}")
                elif "error" in line.lower() or "exception" in line.lower():
                    print(f"❌ {line}")
                elif "Step:" in line:
                    print(f"📊 {line}")
                else:
                    print(f"   {line}")
        
        # Sonuç
        return_code = process.wait()
        if return_code == 0:
            print(f"\n✅ Training tamamlandı!")
            print(f"📁 Sonuçlar: results/{run_id}/")
        else:
            print(f"\n❌ Training hata ile bitti (kod: {return_code})")
        
        return True
        
    except KeyboardInterrupt:
        print(f"\n⏹️ Training durduruldu")
        process.terminate()
        return True
    except Exception as e:
        print(f"\n💥 Hata: {e}")
        return False

def quick_fix_suggestions():
    """Hızlı çözüm önerileri"""
    print("\n🔧 ML-Agents Sorun Çözümleri")
    print("="*35)
    
    fixes = [
        "1. Version downgrade:",
        "   pip uninstall mlagents torch",
        "   pip install mlagents==0.30.0",
        "",
        "2. PyTorch fix:",
        "   pip install torch==1.13.1 torchvision==0.14.1",
        "",
        "3. Clean reinstall:",
        "   pip uninstall mlagents torch",
        "   pip install mlagents",
        "",
        "4. Alternative (conda):",
        "   conda install pytorch torchvision torchaudio -c pytorch",
        "   pip install mlagents",
    ]
    
    for fix in fixes:
        print(fix)

def main():
    """Ana fonksiyon"""
    
    if len(sys.argv) > 1 and sys.argv[1] == "--fix":
        quick_fix_suggestions()
        return
    
    print("🎯 Bomberman Training - Basit Versiyon")
    
    # Training dene
    if not simple_train():
        print("\n💡 Sorun varsa şu komutu dene:")
        print("python fix_and_train.py --fix")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n👋 İptal edildi")
    except Exception as e:
        print(f"\n💥 Beklenmeyen hata: {e}")