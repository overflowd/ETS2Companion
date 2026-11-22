# ETS 2 Otomatik Uygulama Başlatıcı

Euro Truck Simulator 2 (`eurotrucks2.exe`) açıldığında **Lossless Scaling**, **LookPilot** ve **WestPlay** uygulamalarını arka planda otomatik olarak başlatan hafif Windows sistem tepsisi (System Tray) uygulaması.

## Özellikler

- **Otomatik ETS 2 Algılama:** Arka planda düşük kaynak kullanımıyla `eurotrucks2.exe` (isterseniz `amtrucks` vb.) işlemini izler.
- **Akıllı Otomatik Bulma:** Steam kütüphaneleri (`libraryfolders.vdf` / `appmanifest_*.acf`), Windows kayıt defteri, Başlat Menüsü/Masaüstü kısayolları, standart program klasörleri ve o an çalışan işlemler üzerinden uygulama yollarını otomatik tespit eder.
- **Çift Açılmayı Önleme:** Uygulamalardan biri zaten açıksa tekrar başlatmaz.
- **Gecikme ve Çıkışta Kapatma:** Her uygulama için ayrı başlatma gecikmesi (saniye) ve **ETS 2 kapanınca otomatik kapat** seçeneği ayarlanabilir.
- **Windows Başlangıcında Çalışma:** Tek tıkla Windows açıldığında doğrudan sistem tepsisinde (simge durumunda) başlayacak şekilde ayarlanır.
- **İsteğe Bağlı Uygulama Ekleme:** İstediğiniz başka yardımcı uygulamaları (örn. TruckersMP, SimHub, CrewChief vb.) veya `steam://rungameid/...` bağlantılarını da ekleyebilirsiniz.

## Nasıl Kullanılır?

1. `publish\ETS2CompanionLauncher.exe` dosyasına çift tıklayarak programı açın.
2. İlk açılışta bilgisayarınızdaki **Lossless Scaling**, **LookPilot** ve **WestPlay** uygulamalarını otomatik olarak arar.
   - Eğer bir uygulama farklı bir klasörde kuruluysa ve bulunamazsa, ilgili satıra çift tıklayıp **Gözat...** ile `.exe` dosyasını seçin (veya ilgili uygulamayı açıp **Otomatik Bul** butonuna basın).
3. İsterseniz **Şimdi Başlat (Test)** butonuyla ETS 2 açmadan tüm uygulamaların düzgün açıldığını test edin.
4. **"Windows ile birlikte sistem tepsisinde başlat"** kutucuğunu işaretleyin.
5. Pencereyi `X` ile kapattığınızda program sistem tepsisine (saatin yanına) küçülür ve arka planda ETS 2'yi bekler. Tamamen kapatmak için tepsi simgesine sağ tıklayıp **Çıkış**'ı seçebilirsiniz.

## Ayar Dosyası

Tüm ayarlarınız otomatik olarak şurada saklanır:
`%APPDATA%\ETS2CompanionLauncher\config.json`

## Kaynak Koddan Derleme

.NET 9 SDK ile:

```powershell
dotnet publish -c Release -o publish --no-self-contained
```
