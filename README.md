# RabbitMQ Unified Multi-Server Monitor

Birden fazla **RabbitMQ Management HTTP API** sunucusunu tek bir karanlık temalı (Dark UI) masaüstü ekranından canlı olarak izlemenizi, kuyrukları filtrelemenizi ve mesajları kuyruktan silmeden/tüketmeden güvenli şekilde (Non-Destructive Peek) incelemenizi sağlayan taşınabilir (.NET 8 WPF) masaüstü uygulaması.

---

## Öne Çıkan Özellikler

- **Çoklu Sunucu ve Node (Donanım/Kaynak) İzleme:**
  - İstediğiniz kadar RabbitMQ ortamını (Local, Dev, Test, Stage, Prod) ekleyip aynı anda izleyebilirsiniz.
  - Her sunucu kartında **Online/Offline durumu**, **RabbitMQ & Erlang versiyonu**, **toplam kuyruk**, **toplam mesaj**, **aktif consumer sayısı** ve **anlık Publish/Deliver hızı (msg/s)** gösterilir.
  - **Nodes Tablosu (`/api/nodes`):** Ekranın üst kısmındaki **Nodes** panelinde her sunucunun düğüm adı (`rabbit@...`), **File descriptors** (kullanılan / toplam), **Erlang processes**, **Memory** (kullanılan / high watermark sınırı), **Disk space** (boş alan / low watermark sınırı), **Uptime** (`25d 2h`), **Cores** (CPU çekirdek sayısı) ve **Info** (`basic` / `rss`) bilgileri RabbitMQ Web UI görünümüyle birebir aynı yeşil/kırmızı durum çubuklarıyla canlı gösterilir. (İstatistik sıfırlama / Reset stats gibi yazma butonları içermez, %100 salt-okunurdur).
- **Birleşik Kuyruk Tablosu (Unified Queue Stream):**
  - Tüm sunuculardaki kuyrukları tek tabloda birleştirir veya sol panelden seçtiğiniz sunucuya göre filtreler.
  - **Anlık Arama & Hızlı Filtreler:** Kuyruk adı, sunucu adı veya vhost'a göre anlık arama; *"Only Non-Empty (>0 msgs)"* ve *"No Consumers (0 consumers)"* filtreleri.
- **%100 Güvenli ve Salt-Okunur (Read-Only / Non-Destructive) Mimari:**
  - Uygulama içerisinde hiçbir şekilde RabbitMQ servisini durdurma, kuyruk silme (Delete), kuyruk boşaltma (Purge) veya consumer kapatma özelliği **bulunmaz**.
  - **Peek (Mesajları İncele):** Kuyruktaki ilk 5 mesajı tüketmeden (`ack_requeue_true` / non-destructive) görüntüler; mesajlar kuyruktan **silinmez** ve sırası bozulmaz. JSON payload'ları otomatik biçimlendirir (pretty-print) ve panoya kopyalamanızı sağlar.
- **Masaüstü Bildirimleri & Sistem Tepsisi (System Tray):**
  - Pencereyi kapattığınızda veya küçülttüğünüzde arka planda sistem tepsisinde (System Tray) çalışmaya devam eder.
  - Bir sunucu **Offline** olduğunda veya kuyruk mesaj sayısı belirlediğiniz **eşik değerini (Queue Alert Threshold)** aştığında Windows bildirim balonu gösterir (3 dakikalık bildirim soğuma süresi ile).
- **Güvenli Şifre Saklama (Windows DPAPI):**
  - Sunucu profilleri uygulama dizinindeki `servers.json` dosyasında saklanır.
  - Şifreler düz metin (plain-text) olarak **yazılmaz**; Windows **DPAPI (`ProtectedData` - CurrentUser)** ile şifrelenerek kaydedilir.

---

## Gereksinimler

- **Hedef İşletim Sistemi:** Windows 10 / Windows 11 (x64)
- **RabbitMQ Tarafı:** İzlenecek RabbitMQ sunucularında `rabbitmq_management` eklentisinin aktif olması gerekir (varsayılan port: `15672`).
  ```bash
  rabbitmq-plugins enable rabbitmq_management
  ```
- **Geliştirme / Derleme İçin:** .NET 8.0 SDK (Tek dosyalık `publish/RabbitMqMonitor.exe` çıktısını çalıştırmak için hedef bilgisayarda .NET kurulu olmasına **gerek yoktur**).

---

## Kurulum ve Çalıştırma

### 1. Hazır `.exe` ile Çalıştırma (Kurulumsuz / Portable)
Proje içerisinde derlenmiş tek dosyalık çalıştırılabilir dosya doğrudan kullanılabilir:
```text
publish\RabbitMqMonitor.exe
```
Bu dosyayı istediğiniz klasöre veya başka bir Windows bilgisayara kopyalayıp doğrudan çift tıklayarak çalıştırabilirsiniz.

### 2. Kaynak Koddan Çalıştırma
```powershell
dotnet run --project RabbitMqMonitor.csproj
```

### 3. Tek Dosyalık (Single-File) `.exe` Üretme
Değişiklik yaptıktan sonra yeni bir taşınabilir `.exe` almak için:
```powershell
dotnet publish RabbitMqMonitor.csproj -c Release -o ./publish
```

---

## Proje Yapısı

```text
RabbitMqMonitor/
├── Assets/
│   └── rabbitmq.ico                     # Çok çözünürlüklü uygulama ve pencere ikonu
├── Models/
│   ├── QueueModels.cs                   # Kuyruk, mesaj (Peek) ve API DTO modelleri
│   └── ServerProfile.cs                 # Sunucu profili ve canlı telemetri modeli
├── Services/
│   ├── CredentialEncryptionService.cs   # Windows DPAPI şifreleme/çözme servisi
│   ├── RabbitMqApiService.cs            # RabbitMQ HTTP API istemcisi (Sadece Overview, Queues ve güvenli Peek)
│   ├── ServerConfigStorageService.cs    # servers.json okuma/yazma servisi
│   └── TrayNotificationService.cs       # Sistem tepsisi (System Tray) ve masaüstü bildirimleri
├── ViewModels/
│   ├── MainViewModel.cs                 # Ana ekran durumu, zamanlayıcı ve filtreleme mantığı
│   └── MvvmPrimitives.cs                # ObservableObject ve RelayCommand altyapısı
├── Views/
│   ├── PeekMessagesWindow.xaml(.cs)     # Kuyruk mesajlarını (JSON/Payload) inceleme penceresi
│   └── ServerEditorWindow.xaml(.cs)     # Sunucu ekleme/düzenleme ve bağlantı testi penceresi
├── MainWindow.xaml(.cs)                 # Ana kontrol paneli arayüzü
├── RabbitMqMonitor.csproj               # Proje ve tek-dosya publish yapılandırması
└── RabbitMqMonitor.sln                  # Visual Studio Solution dosyası
```

---

## Kullanım İpuçları

1. **Yeni Sunucu Ekleme:** Sol paneldeki **+ Add** butonuna tıklayın, sunucu bilgilerini (`http://localhost:15672`, kullanıcı adı, şifre, vhost) girin ve kaydetmeden önce **Test Connection** butonu ile bağlantıyı doğrulayın.
2. **Belirli Bir Sunucuya Odaklanma:** Sol listedeki bir sunucu kartına tıkladığınızda sağdaki tablo sadece o sunucunun kuyruklarını gösterir. Tekrar tüm sunucuları görmek için üstteki **Show All** butonuna tıklayın.
3. **Kuyruk Eşik Uyarısı (Alert Threshold):** Her sunucu profilinde belirlediğiniz mesaj eşiği (ör. `1000`) aşıldığında ilgili kuyruk satırı tabloda **Turuncu (Warning)** renkle vurgulanır ve masaüstü bildirimi gönderilir. Consumer sayısı `0` olan ve içinde mesaj bekleyen kuyruklar ise **Kırmızı (Stalled)** rozetiyle işaretlenir.

