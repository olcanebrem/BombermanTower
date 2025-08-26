#!/usr/bin/env python3
"""
Simple ML-Agents Training Script
Bu script ML-Agents import problemlerini bypass eder
"""

import os
import sys
import subprocess
import time
from datetime import datetime

# Protobuf compatibility
os.environ['PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION'] = 'python'

def run_training():
    """ML-Agents training'ini subprocess ile çalıştır"""
    
    print("🚀 Bomberman ML-Agents Training")
    print("="*40)
    print(f"📁 Dizin: {os.getcwd()}")
    print(f"🐍 Python: {sys.version}")
    
    # Config kontrol
    config = "config/bomberman_ppo_simple.yaml"
    if not os.path.exists(config):
        print(f"❌ Config bulunamadı: {config}")
        return False
    
    print(f"✅ Config: {config}")
    
    # Run ID
    run_id = f"simple_{datetime.now().strftime('%m%d_%H%M%S')}"
    print(f"🆔 Run ID: {run_id}")
    
    # Command oluştur
    cmd = [
        sys.executable,  # Mevcut Python executable
        "-m", "mlagents.trainers.learn",
        config,
        f"--run-id={run_id}",
        "--force"
    ]
    
    print(f"\n📋 Komut:")
    print(f"   {' '.join(cmd)}")
    print(f"\n🎮 HAZIR OLDUĞUNDA UNITY'DE PLAY'E BAS!")
    print(f"✅ 'Connected to Unity environment' mesajını bekle")
    print(f"⛔ Durdurmak için Ctrl+C")
    print("="*40)
    
    input("Enter'a bas...")
    
    try:
        # Subprocess ile çalıştır
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            universal_newlines=True,
            bufsize=1,
            cwd=os.getcwd()
        )
        
        print(f"🟢 Training başladı (PID: {process.pid})")
        print("-" * 40)
        
        # Real-time output
        for line in process.stdout:
            line = line.rstrip()
            if line:
                # Önemli mesajları highlight et
                if "Connected to Unity" in line:
                    print(f"✅ BAĞLANDI: {line}")
                elif "Listening on port" in line:
                    print(f"🔗 PORT: {line}")
                elif "error" in line.lower():
                    print(f"❌ HATA: {line}")
                elif "Step:" in line or "reward" in line.lower():
                    print(f"📊 {line}")
                elif "INFO:" in line:
                    print(f"ℹ️  {line}")
                else:
                    print(f"   {line}")
        
        # Process sonucu
        return_code = process.wait()
        
        if return_code == 0:
            print(f"\n✅ Training başarıyla tamamlandı!")
        else:
            print(f"\n⚠️ Training bitti (kod: {return_code})")
        
        print(f"📁 Sonuçlar: results/{run_id}/")
        return True
        
    except KeyboardInterrupt:
        print(f"\n⏹️ Training durduruldu")
        try:
            process.terminate()
            process.wait(timeout=5)
        except:
            pass
        return True
        
    except Exception as e:
        print(f"\n💥 Subprocess hatası: {e}")
        return False

def main():
    """Ana fonksiyon"""
    
    print("🎯 Basit ML-Agents Training")
    print("Bu script ML-Agents'ı subprocess ile çalıştırır")
    print("Import problemlerini bypass eder")
    print()
    
    if not run_training():
        print("\n💡 Sorun giderme:")
        print("1. Python 3.8-3.10 kullan")  
        print("2. pip install mlagents")
        print("3. Unity'de PlayerAgent ayarlarını kontrol et")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n👋 İptal edildi")
    except Exception as e:
        print(f"\n💥 Beklenmeyen hata: {e}")
        import traceback
        traceback.print_exc()