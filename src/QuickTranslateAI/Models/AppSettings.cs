using System;

namespace QuickTranslateAI.Models;

public enum ProviderKind
{
    Claude,
    OpenAi,

    /// <summary>
    /// OpenAI uyumlu herhangi bir uc nokta: NVIDIA NIM, Groq, Together, OpenRouter,
    /// yerel Ollama/LM Studio... Adres ve model adi kullanici tarafindan girilir.
    /// </summary>
    Custom
}

/// <summary>Çeviri tamamlandığında ne olacağı.</summary>
public enum ResultBehavior
{
    /// <summary>İmlecin yanında cam bir pencerede göster, panoya dokunma. (varsayılan)</summary>
    Popup,

    /// <summary>Çeviriyi panoya yaz ve küçük bir bildirim göster.</summary>
    ClipboardAndToast,

    /// <summary>Çeviriyi panoya yaz ve Ctrl+V göndererek seçili metnin üzerine yaz.</summary>
    ReplaceSelection
}

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>Pencere arka planı: Windows 11'in yerel cam efektleri.</summary>
public enum BackdropMode
{
    /// <summary>Liquid glass — DWM Acrylic (yarı saydam, arkadaki içeriği bulanıklaştırır).</summary>
    Acrylic,

    /// <summary>Mica — masaüstü duvar kâğıdından renk alan opak doku.</summary>
    Mica,

    /// <summary>Düz renk (en düşük GPU maliyeti, uzak masaüstü için uygun).</summary>
    Solid
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>İlk açılış sihirbazı tamamlandı mı.</summary>
    public bool SetupCompleted { get; set; }

    // --- Çeviri motoru ---
    public ProviderKind Provider { get; set; } = ProviderKind.Claude;
    public string ClaudeModel { get; set; } = "claude-opus-5";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    /// <summary>Ozel saglayicinin OpenAI uyumlu taban adresi (sonunda /v1 ile).</summary>
    public string CustomBaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";

    public string CustomModel { get; set; } = "meta/llama-3.3-70b-instruct";

    /// <summary>Ayarlar ekraninda gosterilecek ad, orn. "NVIDIA NIM".</summary>
    public string CustomDisplayName { get; set; } = "NVIDIA NIM";

    /// <summary>
    /// Bazi OpenAI uyumlu sunucular response_format alanini reddediyor.
    /// Kapaliyken JSON sozlesmesi yalnizca istem metniyle saglanir.
    /// </summary>
    public bool CustomSupportsJsonMode { get; set; }

    // --- Diller ---
    /// <summary>Hedef dilin görünen adı, ör. "Türkçe".</summary>
    public string TargetLanguage { get; set; } = "Türkçe";

    /// <summary>Hedef dilin BCP-47 kodu, ör. "tr".</summary>
    public string TargetLanguageCode { get; set; } = "tr";

    /// <summary>
    /// Kaynak dil hedef dille aynı algılandığında bu dile çevir.
    /// Böylece Türkçe metin Almancaya, Almanca metin Türkçeye gider.
    /// Boşsa çeviri yapılmaz, "zaten hedef dilde" denir.
    /// </summary>
    public string? FallbackLanguage { get; set; } = "İngilizce";
    public string? FallbackLanguageCode { get; set; } = "en";

    // --- Davranış ---
    public ResultBehavior Behavior { get; set; } = ResultBehavior.Popup;
    public bool Enabled { get; set; } = true;

    /// <summary>İki Ctrl+C arasındaki en uzun süre (ms).</summary>
    public int DoubleCopyWindowMs { get; set; } = 500;

    public bool StartWithWindows { get; set; } = true;

    /// <summary>Metni API'ye göndermeden önce onay istenir (gizlilik).</summary>
    public bool ConfirmBeforeSend { get; set; }

    /// <summary>Bu uzunluğun üzerindeki metinler için her zaman onay istenir.</summary>
    public int MaxCharacters { get; set; } = 5000;

    /// <summary>"Yerine yaz" modunda yapıştırma sonrası özgün pano içeriği geri yüklenir.</summary>
    public bool RestoreClipboardAfterReplace { get; set; } = true;

    /// <summary>Popup kaç saniye sonra kendiliğinden kapansın. 0 = kapanmasın.</summary>
    public int PopupAutoCloseSeconds { get; set; } = 14;

    /// <summary>Modele verilen ek çeviri talimatı (ör. "resmî dil kullan").</summary>
    public string? CustomInstructions { get; set; }

    // --- Görünüm ---
    public AppThemeMode Theme { get; set; } = AppThemeMode.System;
    public BackdropMode Backdrop { get; set; } = BackdropMode.Acrylic;

    /// <summary>Vurgu rengini Windows'un kişiselleştirme ayarından al.</summary>
    public bool UseSystemAccent { get; set; } = true;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
