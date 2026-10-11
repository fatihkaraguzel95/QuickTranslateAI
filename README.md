# QuickTranslate AI

[![Sürüm](https://img.shields.io/github/v/release/fatihkaraguzel95/QuickTranslateAI?label=s%C3%BCr%C3%BCm)](https://github.com/fatihkaraguzel95/QuickTranslateAI/releases/latest)
[![İndirme](https://img.shields.io/github/downloads/fatihkaraguzel95/QuickTranslateAI/total?label=indirme)](https://github.com/fatihkaraguzel95/QuickTranslateAI/releases)
[![Lisans](https://img.shields.io/badge/lisans-MIT-blue)](LICENSE)

Windows'ta arka planda çalışan, yapay zekâ destekli hızlı çeviri aracı.
Metni nerede olursan ol seç, **Ctrl + C tuşuna iki kez bas**, çeviri imlecin yanında belirsin.

SAP GUI'deki bir hata mesajı, Outlook'taki Almanca bir e-posta, tarayıcıdaki bir paragraf —
uygulama değiştirmene, sekme açmana, yapıştırıp beklemene gerek yok.

### ⬇️ [QuickTranslateAI-Setup.exe indir](https://github.com/fatihkaraguzel95/QuickTranslateAI/releases/latest/download/QuickTranslateAI-Setup.exe)

51.7 MB · Windows 10 1809+ · **yönetici hakkı istemez** · .NET kurulumu gerekmez

> Paket kod imzalama sertifikasıyla imzalanmadığı için Windows SmartScreen uyarı
> gösterebilir: **Ek bilgi → Yine de çalıştır**.

---

## Nasıl çalışır

1. **Metni seç** — herhangi bir uygulamada.
2. **Ctrl + C, Ctrl + C** — iki kopyalama hareketi 500 ms içinde algılanır.
3. **Kaynak dil otomatik algılanır**, kurulumda belirlediğin hedef dile çevrilir.
4. **Sonuç imlecin yanında** cam bir pencerede görünür. Panoya hiç dokunulmaz.

Hedef dil Türkçe, ikinci dil Almanca seçtiysen tek kısayol iki yönlü çalışır:
Almanca metin Türkçeye, Türkçe metin Almancaya gider. Ayrıca dil seçmen gerekmez.

### Çeviri hazır olduğunda ne olacağını sen seçersin

| Mod | Davranış | Ne zaman uygun |
| --- | --- | --- |
| **Cam pencerede göster** (varsayılan) | Pano değişmez, özgün metin korunur | SAP'deki salt okunur hata mesajları, her yerde güvenli |
| **Panoya kopyala + bildirim** | Çeviri panoya yazılır, sağ altta bildirim | Çeviriyi başka bir yere yapıştıracaksan |
| **Seçili metnin yerine yaz** | Ctrl+V gönderilir, metin değişir | Yalnızca düzenlenebilir alanlarda |

Popup üzerinden çeviriyi kopyalayabilir, özgün metni görebilir veya
anlık olarak başka bir hedef dile çevirebilirsin.

---

## Kurulum

### Hazır paketle

1. [En son sürümü indir](https://github.com/fatihkaraguzel95/QuickTranslateAI/releases/latest/download/QuickTranslateAI-Setup.exe)
   ve çalıştır. Yönetici hakkı istemez; kurulum kendi kullanıcı profiline yapılır.
2. Uygulama ilk açılışta **kurulum sihirbazını** gösterir. Gereken her şey burada sorulur:
   - Çeviri sağlayıcısı ve **API anahtarı**
   - Model
   - Hedef dil ve ikinci dil
   - Çeviri hazır olduğunda ne olacağı
   - **Tema (Sistem / Açık / Koyu)** ve pencere arka planı
   - Windows açılışında otomatik başlatma
3. Sihirbazdaki **"Bağlantıyı test et"** düğmesiyle anahtarının çalıştığını hemen doğrula.

Bundan sonra uygulama sistem tepsisinde durur. Ana pencereyi açık tutmana gerek yoktur.

### Kaynaktan derleyerek

```powershell
winget install Microsoft.DotNet.SDK.9
git clone https://github.com/<kullanici>/QuickTranslateAI.git
cd QuickTranslateAI
.\build.ps1                 # publish\QuickTranslateAI.exe (kendi kendine yeten, ~177 MB)
.\build.ps1 -Installer      # ayrıca installer\Output\QuickTranslateAI-Setup.exe
```

`-FrameworkDependent` eklersen çıktı ~35 MB'a iner; bu durumda hedef bilgisayarda
[.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) kurulu olmalıdır.
Kurulum paketi için [Inno Setup 6](https://jrsoftware.org/isdl.php) gerekir
(`winget install JRSoftware.InnoSetup`).

---

## Çeviri sağlayıcısı

Sağlayıcı alanı **serbesttir**. Üç yol var:

### 1. Anthropic Claude
Anahtarı [console.anthropic.com](https://console.anthropic.com) üzerinden oluşturursun.
Varsayılan model `claude-opus-5`. Çeviri basit bir görev olduğu için istekler
`effort: low` ile gönderilir — gecikme ve maliyet belirgin düşer, kalite korunur.

### 2. OpenAI
Anahtarı [platform.openai.com/api-keys](https://platform.openai.com/api-keys) üzerinden alırsın.

### 3. Özel uç nokta — NVIDIA NIM, Groq, OpenRouter, yerel modeller
OpenAI uyumlu Chat Completions konuşan **her servis** çalışır.
Sihirbazda hazır ayar düğmeleri var; adresi ve model adını elle de yazabilirsin.

**NVIDIA için:**

1. [build.nvidia.com](https://build.nvidia.com) adresinde bir model aç.
2. Sağdaki **"Get API Key"** ile anahtarını üret (`nvapi-` ile başlar).
3. Sihirbazda **Özel uç nokta → NVIDIA NIM** hazır ayarına bas. Adres otomatik dolar:
   `https://integrate.api.nvidia.com/v1`
4. Model kimliğini NVIDIA'nın sayfasında yazdığı gibi gir, örn. `meta/llama-3.3-70b-instruct`.

| Hazır ayar | Adres |
| --- | --- |
| NVIDIA NIM | `https://integrate.api.nvidia.com/v1` |
| Groq | `https://api.groq.com/openai/v1` |
| OpenRouter | `https://openrouter.ai/api/v1` |
| Yerel (Ollama) | `http://localhost:11434/v1` |

> **JSON modu:** Bazı uyumlu sunucular `response_format` alanını reddeder.
> Emin değilsen kapalı bırak — çeviri biçimi yalnızca istem metniyle sağlanır
> ve yanıt ayrıştırıcısı esnek yazılmıştır.

---

## Gizlilik ve maliyet

- **API anahtarı Windows Kimlik Bilgisi Yöneticisi'nde** saklanır
  (`QuickTranslateAI:Anthropic` / `:OpenAI` / `:Custom`).
  Ne ayar dosyasına ne de kaynak koda yazılır.
- **Kopyalanan metin seçtiğin sağlayıcıya gönderilir.** Parola alanları ve hassas
  veriler için: Ayarlar → Görünüm → *"Göndermeden önce onay iste"* seçeneğini aç.
  Açıkken her çeviri öncesi metnin önizlemesi gösterilir ve onay beklenir.
- Belirlediğin karakter sınırını aşan metinler için onay **her zaman** istenir.
- Tepsi menüsünden dinlemeyi tek tıkla duraklatabilirsin.
- Harici bir API kullanıldığı için çeviri başına sağlayıcının tarifesine göre ücret oluşabilir.
  Yerel bir model (Ollama) seçersen hiçbir veri bilgisayardan çıkmaz ve ücret oluşmaz.

---

## Görünüm

Arayüz Windows 11'in **kendi cam efektini** kullanır — taklit bir bulanıklık değil,
DWM kompozisyonu (`DWMWA_SYSTEMBACKDROP_TYPE`):

- **Liquid glass (Acrylic)** — arkadaki içerik gerçekten bulanıklaşır ve renk sızdırır.
- **Mica** — masaüstü duvar kâğıdından renk alan sakin doku.
- **Düz renk** — uzak masaüstü ve sanal makineler için.

Tema **Sistem / Açık / Koyu** olarak seçilir; "Sistem" seçiliyken Windows'un
açık-koyu ayarı anında takip edilir. Vurgu rengi istersen Windows'un
kişiselleştirme ayarından alınır; yazı rengi her vurguda okunur kalsın diye
parlaklığa göre hesaplanır.

Windows 10'da cam efektleri bulunmadığı için düz renk kullanılır; uygulama çalışır.

---

## Ayarlar ve dosyalar

Tepsi simgesine **sol tıkla** ayarlar açılır, **sağ tıkla** hızlı menü gelir
(duraklat, hedef dil, çıkış).

| Ne | Nerede |
| --- | --- |
| Ayarlar | `%APPDATA%\QuickTranslateAI\settings.json` |
| Günlük | `%APPDATA%\QuickTranslateAI\quicktranslate.log` |
| API anahtarı | Windows Kimlik Bilgisi Yöneticisi |
| Otomatik başlatma | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |

Bir sorun yaşarsan günlük dosyası ilk bakılacak yerdir: kısayolun algılanıp
algılanmadığı, hangi sağlayıcının çağrıldığı ve hata ayrıntısı oraya yazılır.

---

## Mimari

| Bileşen | Teknoloji |
| --- | --- |
| Masaüstü uygulaması | C# / .NET 9, WPF |
| Global kısayol | `WH_KEYBOARD_LL` düşük seviye klavye kancası |
| Metin alma | Windows Clipboard API (yeniden deneme + sıra numarası takibi) |
| Cam efekti | `DwmSetWindowAttribute` (backdrop, yuvarlak köşe, koyu başlık) |
| Çeviri | Anthropic .NET SDK · OpenAI uyumlu HTTP istemcisi |
| Anahtar saklama | Windows Credential Manager (`CredRead`/`CredWrite`) |
| Ayarlar | `System.Text.Json` |
| Kurulum | Inno Setup 6 |

```
src/QuickTranslateAI/
├─ App.xaml(.cs)            Uygulama girişi, tek örnek kilidi
├─ AppController.cs         Tepsi simgesi, kısayol dinleme, çeviri akışı
├─ Models/                  Ayarlar, dil kataloğu, sonuç nesnesi
├─ Services/
│  ├─ DoubleCopyWatcher.cs  Çift Ctrl+C algılama
│  ├─ ClipboardService.cs   Pano okuma/yazma
│  ├─ SecretStore.cs        Credential Manager
│  ├─ SettingsStore.cs      JSON ayarlar
│  ├─ Native/               P/Invoke: klavye, DWM, SendInput
│  └─ Translation/          ITranslator + Claude / OpenAI uyumlu uygulamalar
├─ Theming/                 Açık-koyu palet, cam kontrol seti, ThemeManager
└─ Views/                   Kurulum sihirbazı (ayar ekranı olarak da çalışır), sonuç penceresi
```

### Dikkat edilen ayrıntılar

- **Otomatik tekrar sayılmaz.** Ctrl+C'yi basılı tutmak çift kopyalama sayılmaz;
  iki basış arasında bir tuş bırakma olayı aranır.
- **Pano kaybolmaz.** Varsayılan modda pano hiç değiştirilmez. "Yerine yaz" modunda
  özgün içerik yapıştırmadan sonra geri yüklenir.
- **Karışık DPI.** Popup konumu fiziksel piksel üzerinden hesaplanır, böylece farklı
  ölçekli monitörlerde doğru ekranda açılır.
- **Odağı çalmaz.** Sonuç penceresi `ShowActivated=False` ile açılır; SAP'deki
  seçimin ve klavye odağın bozulmaz.
- **Biçim korunur.** İstem; URL, e-posta, dosya yolu, yer tutucu, SAP işlem kodu ve
  mesaj kimliklerini (`ME21N`, `M8 147`) çevirmemesi için modele açıkça talimat verir.

---

## Yol haritası

- [ ] Çeviri geçmişi ve arama
- [ ] Dil çiftini kısayolla anlık değiştirme
- [ ] Sözlük / terim listesi (kurum içi terimleri sabitleme)
- [ ] Seçili metni kopyalamadan okuma (UI Automation)

## Lisans

MIT
