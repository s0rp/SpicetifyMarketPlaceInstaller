using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides utility methods for logging messages to a file.
/// </summary>
public static class Logger
{
    private static readonly string _logFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "log.txt"
    );
    private static readonly object _lock = new object();

    /// <summary>
    /// Initializes the logger, creating or clearing the log file.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            string initialMessage =
                $"Log initialized at {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}";
            File.WriteAllText(_logFilePath, initialMessage);
            Console.WriteLine($"Logging to: {_logFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"CRITICAL: Failed to initialize logger at {_logFilePath}: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Writes a log entry to the log file.
    /// </summary>
    /// <param name="level">The log level (e.g., INFO, WARN, ERROR).</param>
    /// <param name="message">The message to log.</param>
    private static void WriteLine(string level, string message)
    {
        lock (_lock)
        {
            try
            {
                string logEntry =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToUpperInvariant()}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL: Failed to write to log file: {ex.Message}");
                Console.WriteLine($"Original log message [{level.ToUpperInvariant()}]: {message}");
            }
        }
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Info(string message) => WriteLine("INFO", message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Warn(string message, string OPT = null) =>
        WriteLine("WARN", (OPT != null ? (message + "\n" + OPT) : message));

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Error(string message) => WriteLine("ERROR", message);

    /// <summary>
    /// Logs an error message with exception details.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="ex">The exception to log.</param>
    public static void Error(string message, Exception ex) =>
        WriteLine(
            "ERROR",
            $"{message} | Exception: {ex.ToString().Replace(Environment.NewLine, " ")}"
        );

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Debug(string message) => WriteLine("DEBUG", message);
}

/// <summary>
/// Handles the installation of Spicetify Marketplace.
/// Supports localization, Spicetify CLI installation, Marketplace download and setup,
/// and force reinstallation options.
/// </summary>
public static class SpicetifyMarketplaceInstaller
{
    private static bool _bypassAdmin = false;
    private static string _currentLanguage = "en";

    private static readonly Dictionary<string, Dictionary<string, string>> _translations =
        new Dictionary<string, Dictionary<string, string>>
        {
            {
                "SettingUp",
                new Dictionary<string, string>
                {
                    { "en", "Initializing Spicetify Marketplace Installer..." },
                    { "tr", "Spicetify Marketplace Yükleyicisi başlatılıyor..." }
                }
            },
            {
                "SpicetifyNotFound",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Spicetify CLI not found. It appears Spicetify is not installed or not in PATH."
                    },
                    {
                        "tr",
                        "Spicetify CLI bulunamadı. Spicetify yüklü değil veya PATH ortam değişkeninde tanımlı değil gibi görünüyor."
                    }
                }
            },
            {
                "InstallingSpicetify",
                new Dictionary<string, string>
                {
                    { "en", "Attempting to install Spicetify CLI..." },
                    { "tr", "Spicetify CLI yüklenmeye çalışılıyor..." }
                }
            },
            {
                "InstallationFailed",
                new Dictionary<string, string>
                {
                    { "en", "Installation process failed." },
                    { "tr", "Yükleme işlemi başarısız oldu." }
                }
            },
            {
                "SpicetifyCliInstallScriptFin",
                new Dictionary<string, string>
                {
                    { "en", "Spicetify CLI installation script has finished executing." },
                    { "tr", "Spicetify CLI yükleme betiği çalıştırılması tamamlandı." }
                }
            },
            {
                "RunningPowershellInstaller",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Executing Spicetify CLI PowerShell installer (this may take a moment, automatically answering 'No' to prompts)..."
                    },
                    {
                        "tr",
                        "Spicetify CLI için PowerShell yükleyici çalıştırılıyor (bu biraz zaman alabilir, istemlere otomatik olarak 'Hayır' yanıtı veriliyor)..."
                    }
                }
            },
            {
                "ErrorFromSpicetify",
                new Dictionary<string, string>
                {
                    { "en", "Received error from Spicetify process:" },
                    { "tr", "Spicetify işleminden hata alındı:" }
                }
            },
            {
                "ErrorRunningSpicetify",
                new Dictionary<string, string>
                {
                    { "en", "An error occurred while trying to run a Spicetify command:" },
                    { "tr", "Bir Spicetify komutu çalıştırılırken bir hata oluştu:" }
                }
            },
            {
                "FailedToGetSpicetifyPath",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "CRITICAL ERROR: Failed to determine Spicetify userdata path. Cannot proceed."
                    },
                    {
                        "tr",
                        "KRİTİK HATA: Spicetify kullanıcı verileri yolu belirlenemedi. Devam edilemiyor."
                    }
                }
            },
            {
                "SpicetifyPathCommandOutput",
                new Dictionary<string, string>
                {
                    { "en", "Raw output from 'spicetify path userdata' command:" },
                    { "tr", "'spicetify path userdata' komutunun ham çıktısı:" }
                }
            },
            {
                "SpicetifyPathInvalidFallback",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Warning: Spicetify path command did not yield a valid directory. Using fallback path: {0}"
                    },
                    {
                        "tr",
                        "Uyarı: Spicetify yol komutu geçerli bir dizin sağlamadı. Yedek yola geçiliyor: {0}"
                    }
                }
            },
            {
                "SpicetifyUserDataPath",
                new Dictionary<string, string>
                {
                    { "en", "Determined Spicetify UserData Path: " },
                    { "tr", "Belirlenen Spicetify Kullanıcı Veri Yolu: " }
                }
            },
            {
                "RemovingCreatingMarketplaceFolders",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Preparing Marketplace directories (removing existing if present, then creating new)..."
                    },
                    {
                        "tr",
                        "Marketplace dizinleri hazırlanıyor (mevcutlar varsa kaldırılıyor, ardından yenileri oluşturuluyor)..."
                    }
                }
            },
            {
                "ErrorDeletingFolder",
                new Dictionary<string, string>
                {
                    { "en", "Error occurred while deleting directory" },
                    { "tr", "Dizin silinirken hata oluştu" }
                }
            },
            {
                "DownloadingMarketplace",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Downloading latest Spicetify Marketplace release (marketplace.zip)..."
                    },
                    { "tr", "En son Spicetify Marketplace sürümü (marketplace.zip) indiriliyor..." }
                }
            },
            {
                "UnzippingAndInstalling",
                new Dictionary<string, string>
                {
                    { "en", "Extracting and installing Marketplace files..." },
                    { "tr", "Marketplace dosyaları arşivden çıkarılıp yükleniyor..." }
                }
            },
            {
                "DetectedExtractedFolder",
                new Dictionary<string, string>
                {
                    { "en", "Detected extracted content in subdirectory: " },
                    { "tr", "Çıkarılmış içerik alt dizinde bulundu: " }
                }
            },
            {
                "InsteadOf",
                new Dictionary<string, string>
                {
                    { "en", " (instead of expected " },
                    { "tr", " (beklenen yerine " }
                }
            },
            {
                "FilesExtractedDirectly",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Marketplace files appear to be extracted directly into the target application path. No subdirectory move needed."
                    },
                    {
                        "tr",
                        "Marketplace dosyaları doğrudan hedef uygulama yoluna çıkarılmış gibi görünüyor. Alt dizinden taşıma gerekmiyor."
                    }
                }
            },
            {
                "WarningExpectedFolderNotFound",
                new Dictionary<string, string>
                {
                    { "en", "Warning: Expected extracted content directory '" },
                    { "tr", "Uyarı: Beklenen çıkarılmış içerik dizini '" }
                }
            },
            {
                "NotFoundConsiderManual",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "' was not found. Files might be in an unexpected location. If issues persist, consider manual extraction and placement into the CustomApps/marketplace directory."
                    },
                    {
                        "tr",
                        "' bulunamadı. Dosyalar beklenmedik bir konumda olabilir. Sorun devam ederse, CustomApps/marketplace dizinine manuel çıkarma ve yerleştirmeyi düşünün."
                    }
                }
            },
            {
                "MovingItemsFrom",
                new Dictionary<string, string>
                {
                    { "en", "Moving items from subdirectory '" },
                    { "tr", "Öğeler alt dizinden taşınıyor: '" }
                }
            },
            {
                "ToMarketplaceRoot",
                new Dictionary<string, string>
                {
                    { "en", "' to Marketplace application root directory..." },
                    { "tr", "' Marketplace uygulama kök dizinine..." }
                }
            },
            {
                "ConfiguringSpicetify",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Configuring Spicetify for Marketplace (as per official Spicetify guide)..."
                    },
                    {
                        "tr",
                        "Marketplace için Spicetify yapılandırılıyor (resmi Spicetify rehberine göre)..."
                    }
                }
            },
            {
                "DownloadingPlaceholderTheme",
                new Dictionary<string, string>
                {
                    { "en", "Downloading Marketplace placeholder theme (color.ini)..." },
                    { "tr", "Marketplace yer tutucu teması (color.ini) indiriliyor..." }
                }
            },
            {
                "LocalThemeFound",
                new Dictionary<string, string>
                {
                    { "en", "An existing Spicetify theme ('{0}') was detected." },
                    { "tr", "Mevcut bir Spicetify teması ('{0}') algılandı." }
                }
            },
            {
                "ReplaceThemePrompt",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Do you want to replace it with the Marketplace placeholder theme? This is recommended to easily install themes from Marketplace."
                    },
                    {
                        "tr",
                        "Bunu Marketplace yer tutucu temasıyla değiştirmek istiyor musunuz? Marketplace'ten kolayca tema yüklemek için bu önerilir."
                    }
                }
            },
            {
                "YesPromptChar",
                new Dictionary<string, string> { { "en", "Y" }, { "tr", "E" } }
            },
            {
                "NoPromptChar",
                new Dictionary<string, string> { { "en", "N" }, { "tr", "H" } }
            },
            {
                "YesPromptFull",
                new Dictionary<string, string> { { "en", "Yes" }, { "tr", "Evet" } }
            },
            {
                "NoPromptFull",
                new Dictionary<string, string> { { "en", "No" }, { "tr", "Hayır" } }
            },
            {
                "SettingCurrentThemeMarketplace",
                new Dictionary<string, string>
                {
                    { "en", "Setting current Spicetify theme to 'marketplace'..." },
                    { "tr", "Geçerli Spicetify teması 'marketplace' olarak ayarlanıyor..." }
                }
            },
            {
                "BackingUpAndApplying",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Backing up current Spicetify configuration and applying all changes..."
                    },
                    {
                        "tr",
                        "Mevcut Spicetify yapılandırması yedekleniyor ve tüm değişiklikler uygulanıyor..."
                    }
                }
            },
            {
                "Done",
                new Dictionary<string, string>
                {
                    { "en", "Process completed!" },
                    { "tr", "İşlem tamamlandı!" }
                }
            },
            {
                "CheckErrors",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "If Spotify's appearance hasn't changed, please review the messages above for any errors. Also, check 'log.txt' for detailed logs."
                    },
                    {
                        "tr",
                        "Eğer Spotify görünümünde bir değişiklik olmadıysa, lütfen yukarıdaki mesajları olası hatalar için gözden geçirin. Ayrıca, detaylı kayıtlar için 'log.txt' dosyasını kontrol edin."
                    }
                }
            },
            {
                "RestartSpotify",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "IMPORTANT: Please restart Spotify completely (quit from system tray if running, then reopen) for all changes to take full effect."
                    },
                    {
                        "tr",
                        "ÖNEMLİ: Tüm değişikliklerin tam olarak etkili olması için lütfen Spotify'ı tamamen yeniden başlatın (çalışıyorsa sistem tepsisinden çıkın, sonra tekrar açın)."
                    }
                }
            },
            {
                "PressAnyKeyToExit",
                new Dictionary<string, string>
                {
                    { "en", "Press any key to exit this installer." },
                    { "tr", "Bu yükleyiciden çıkmak için herhangi bir tuşa basın." }
                }
            },
            {
                "ErrorLabel",
                new Dictionary<string, string> { { "en", "ERROR:" }, { "tr", "HATA:" } }
            },
            {
                "InstallationCompletePromptTitle",
                new Dictionary<string, string>
                {
                    { "en", "Installation Attempt Finished - Verification Required" },
                    { "tr", "Kurulum Denemesi Tamamlandı - Doğrulama Gerekiyor" }
                }
            },
            {
                "InstallationCompletePromptQuestion",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Was the installation successful and is Marketplace visible in Spotify (after a full restart of Spotify)? (Enter 'N' for force reinstall if not)"
                    },
                    {
                        "tr",
                        "Kurulum başarılı oldu mu ve Marketplace Spotify'da (Spotify'ı tam yeniden başlattıktan sonra) görünüyor mu? (Başarısız olduysa, zorla yeniden kurulum için 'H' girin)"
                    }
                }
            },
            {
                "GreatSuccess",
                new Dictionary<string, string>
                {
                    { "en", "Excellent! Marketplace should now be available. Exiting installer." },
                    {
                        "tr",
                        "Harika! Marketplace şimdi kullanılabilir olmalı. Yükleyici sonlandırılıyor."
                    }
                }
            },
            {
                "ProceedingWithForceReinstall",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Understood. Proceeding with a force reinstall to attempt to resolve potential issues."
                    },
                    {
                        "tr",
                        "Anlaşıldı. Olası sorunları çözmek amacıyla zorla yeniden kurulum ile devam ediliyor."
                    }
                }
            },
            {
                "ForceReinstallStarting",
                new Dictionary<string, string>
                {
                    { "en", "Starting force reinstall process for Spicetify Marketplace..." },
                    {
                        "tr",
                        "Spicetify Marketplace için zorla yeniden kurulum işlemi başlatılıyor..."
                    }
                }
            },
            {
                "CleaningSpicetifyData",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Attempting to clean existing Spicetify data (this includes running 'spicetify restore' and deleting data directories)..."
                    },
                    {
                        "tr",
                        "Mevcut Spicetify verileri temizlenmeye çalışılıyor ('spicetify restore' çalıştırılacak ve veri dizinleri silinecektir)..."
                    }
                }
            },
            {
                "DeletingFolder",
                new Dictionary<string, string>
                {
                    { "en", "Deleting directory: {0}" },
                    { "tr", "Dizin siliniyor: {0}" }
                }
            },
            {
                "SpicetifyFoldersCleaned",
                new Dictionary<string, string>
                {
                    { "en", "Spicetify data directories have been cleaned." },
                    { "tr", "Spicetify veri dizinleri temizlendi." }
                }
            },
            {
                "ErrorCleaningFolders",
                new Dictionary<string, string>
                {
                    { "en", "An error occurred while cleaning Spicetify data directories" },
                    { "tr", "Spicetify veri dizinleri temizlenirken bir hata oluştu" }
                }
            },
            {
                "DataCleanedAttemptingFreshInstall",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Spicetify data cleaned. Now attempting a fresh installation of Spicetify Marketplace..."
                    },
                    {
                        "tr",
                        "Spicetify verileri temizlendi. Şimdi Spicetify Marketplace için yeni bir kurulum deneniyor..."
                    }
                }
            },
            {
                "AdminRightsDetected",
                new Dictionary<string, string>
                {
                    {
                        "en",
                        "Administrator rights detected or --bypass-admin flag used. The '--bypass-admin' flag will be automatically used for Spicetify commands."
                    },
                    {
                        "tr",
                        "Yönetici hakları algılandı veya --bypass-admin bayrağı kullanıldı. Spicetify komutları için '--bypass-admin' bayrağı otomatik olarak kullanılacak."
                    }
                }
            },
            {
                "DuringForceReinstall",
                new Dictionary<string, string>
                {
                    { "en", "An error occurred during the force reinstall process" },
                    { "tr", "Zorla yeniden kurulum işlemi sırasında bir hata oluştu" }
                }
            },
            {
                "DuringStandardInstall",
                new Dictionary<string, string>
                {
                    { "en", "An error occurred during the standard install process" },
                    { "tr", "Standart kurulum işlemi sırasında bir hata oluştu" }
                }
            },
            {
                "SpicetifyCommandFailed",
                new Dictionary<string, string>
                {
                    { "en", "Spicetify command '{0}' failed with exit code {1}." },
                    { "tr", "Spicetify komutu '{0}', {1} çıkış koduyla başarısız oldu." }
                }
            },
            {
                "SpicetifyCommandOutputLog",
                new Dictionary<string, string> { { "en", "Output:" }, { "tr", "Çıktı:" } }
            },
        };

    /// <summary>
    /// Gets a translated string for the given key and current language, formatting it with provided arguments.
    /// Falls back to English if the translation is not found for the current language.
    /// If the key itself is not found, returns the key.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <param name="args">Optional arguments for string formatting.</param>
    /// <returns>The translated and formatted string, or the key itself if not found.</returns>
    private static string T(string key, params object[] args)
    {
        Logger.Debug(
            $"Translating key: '{key}', Lang: '{_currentLanguage}', Args count: {args.Length}"
        );
        if (_translations.TryGetValue(key, out var langDict))
        {
            string formatString;
            if (langDict.TryGetValue(_currentLanguage, out var translation))
            {
                formatString = translation;
                Logger.Debug(
                    $"Found translation for '{key}' in '{_currentLanguage}': '{formatString}'"
                );
            }
            else if (langDict.TryGetValue("en", out var englishTranslation))
            {
                formatString = englishTranslation;
                Logger.Warn(
                    $"Translation for key '{key}' not found in '{_currentLanguage}'. Fell back to English: '{formatString}'"
                );
            }
            else
            {
                Logger.Error(
                    $"Translation missing for key: '{key}' in any registered language. Returning key."
                );
                return key;
            }

            if (args == null || args.Length == 0)
            {
                return formatString;
            }

            try
            {
                return string.Format(formatString, args);
            }
            catch (FormatException ex)
            {
                Logger.Error(
                    $"Translation formatting error for key '{key}', language '{_currentLanguage}'. Format string: '{formatString}'. Args: [{string.Join(", ", args.Select(a => a?.ToString() ?? "null"))}].",
                    ex
                );
                return formatString + $" (FORMATTING ERROR: Expected {args.Length} args)";
            }
        }
        Logger.Error($"Translation key not found in _translations: '{key}'. Returning key.");
        return key;
    }

    /// <summary>
    /// Writes a message to the console with a specified color and logs it.
    /// </summary>
    /// <param name="message">The message to write and log.</param>
    /// <param name="color">The console color to use. Defaults to Gray.</param>
    /// <param name="logLevel">The log level for file logging. Defaults to "INFO".</param>
    private static void WriteHost(
        string message,
        ConsoleColor color = ConsoleColor.Gray,
        string logLevel = "INFO"
    )
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();

        string sanitizedMessage = message.Replace(Environment.NewLine, " ");
        switch (logLevel.ToUpperInvariant())
        {
            case "INFO":
                Logger.Info(sanitizedMessage);
                break;
            case "WARN":
                Logger.Warn(sanitizedMessage);
                break;
            case "ERROR":
                Logger.Error(sanitizedMessage);
                break;
            case "DEBUG":
                Logger.Debug(sanitizedMessage);
                break;
            default:
                Logger.Info(sanitizedMessage);
                break;
        }
    }

    /// <summary>
    /// Represents the result of an executed process, including its output and exit code.
    /// </summary>
    private class ProcessResult
    {
        /// <summary>Gets or sets the standard output and standard error (if any) combined from the process.</summary>
        public string Output { get; set; } = "";

        /// <summary>Gets or sets the exit code of the process.</summary>
        public int ExitCode { get; set; }
    }

    /// <summary>
    /// Invokes a Spicetify command with specified arguments, wrapped by cmd.exe.
    /// </summary>
    /// <param name="arguments">The arguments to pass to the Spicetify CLI.</param>
    /// <param name="captureOutput">Whether to capture the standard output and error streams. Defaults to false, meaning output goes to console.</param>
    /// <returns>A Task representing the asynchronous operation, yielding a <see cref="ProcessResult"/> containing the output and exit code.</returns>
    private static async Task<ProcessResult> InvokeSpicetify(
        string[] arguments,
        bool captureOutput = false
    )
    {
        Logger.Debug(
            $"Entering {nameof(InvokeSpicetify)} with args: [{string.Join(", ", arguments)}], captureOutput: {captureOutput}"
        );
        var spicetifyArgsList = new List<string>();
        if (_bypassAdmin)
        {
            spicetifyArgsList.Add("--bypass-admin");
            Logger.Debug("Added --bypass-admin flag to Spicetify arguments.");
        }
        spicetifyArgsList.AddRange(arguments);

        string spicetifyCommand = "spicetify";
        string spicetifyFullArgs = string.Join(
            " ",
            spicetifyArgsList.Select(a => a.Contains(" ") ? $"\"{a}\"" : a)
        );

        string cmdArguments = $"/c \"{spicetifyCommand} {spicetifyFullArgs}\"";
        Logger.Info($"Executing Spicetify command via cmd.exe: cmd.exe {cmdArguments}");

        var psi = new ProcessStartInfo("cmd.exe", cmdArguments)
        {
            UseShellExecute = false,
            CreateNoWindow = captureOutput,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            StandardOutputEncoding = captureOutput ? Encoding.UTF8 : null,
            StandardErrorEncoding = captureOutput ? Encoding.UTF8 : null
        };
        if (!captureOutput)
        {
            psi.CreateNoWindow = false;
            Logger.Debug("Output will not be captured directly; CreateNoWindow set to false.");
        }

        string output = "";
        int exitCode = -1;
        var processStopwatch = Stopwatch.StartNew();

        try
        {
            using (var process = new Process { StartInfo = psi })
            {
                if (captureOutput)
                {
                    var outBuilder = new StringBuilder();
                    var errBuilder = new StringBuilder();
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            outBuilder.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            errBuilder.AppendLine(e.Data);
                    };

                    process.Start();
                    Logger.Debug(
                        $"cmd.exe process (PID: {process.Id}) started for '{spicetifyFullArgs}'. Capturing output."
                    );
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await process.WaitForExitAsync();
                    exitCode = process.ExitCode;

                    output = outBuilder.ToString().Trim();
                    if (errBuilder.Length > 0)
                    {
                        string stderrContent = errBuilder.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(stderrContent))
                        {
                            output +=
                                (string.IsNullOrWhiteSpace(output) ? "" : Environment.NewLine)
                                + "STDERR:"
                                + Environment.NewLine
                                + stderrContent;
                            Logger.Debug($"STDERR from cmd.exe wrapper:\n{stderrContent}");
                        }
                    }
                    Logger.Debug($"Captured Output from cmd.exe wrapper (trimmed):\n{output}");
                }
                else
                {
                    process.Start();
                    Logger.Debug(
                        $"cmd.exe process (PID: {process.Id}) started for '{spicetifyFullArgs}'. Not capturing output directly."
                    );
                    await process.WaitForExitAsync();
                    exitCode = process.ExitCode;
                }
                processStopwatch.Stop();
                Logger.Debug(
                    $"cmd.exe process for '{spicetifyFullArgs}' exited with code {exitCode}. Duration: {processStopwatch.ElapsedMilliseconds}ms."
                );
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            processStopwatch.Stop();
            output = $"Win32Exception during cmd.exe/Spicetify execution: {ex.Message}";
            exitCode = ex.NativeErrorCode != 0 ? ex.NativeErrorCode : -100; // Use native error code if available
            Logger.Error(
                $"Win32Exception running cmd.exe for Spicetify command '{spicetifyFullArgs}'. Duration: {processStopwatch.ElapsedMilliseconds}ms.",
                ex
            );
        }
        catch (Exception ex)
        {
            processStopwatch.Stop();
            output = $"Exception during cmd.exe/Spicetify execution: {ex.ToString()}";
            exitCode = -1; // General error
            Logger.Error(
                $"Generic Exception running cmd.exe for Spicetify command '{spicetifyFullArgs}'. Duration: {processStopwatch.ElapsedMilliseconds}ms.",
                ex
            );
        }

        var result = new ProcessResult { Output = output.Trim(), ExitCode = exitCode };

        if (exitCode != 0 && captureOutput)
        {
            WriteHost(
                T("SpicetifyCommandFailed", $"cmd.exe {cmdArguments}", exitCode),
                ConsoleColor.Yellow,
                "WARN"
            );
            WriteHost(
                $"{T("SpicetifyCommandOutputLog")}\n{result.Output}",
                ConsoleColor.DarkYellow,
                "WARN"
            );
        }
        else if (exitCode != 0 && !captureOutput)
        {
            Logger.Warn(
                $"Spicetify command '{cmdArguments}' (not captured) failed with exit code {exitCode}."
            );
        }
        Logger.Debug($"Exiting {nameof(InvokeSpicetify)} for '{spicetifyFullArgs}'.");
        return result;
    }

    /// <summary>
    /// Checks if Spicetify CLI is installed and accessible by running 'spicetify --version'.
    /// </summary>
    /// <returns>True if Spicetify CLI is detected, false otherwise.</returns>
    private static bool IsSpicetifyInstalled()
    {
        Logger.Debug($"Entering {nameof(IsSpicetifyInstalled)}.");
        Logger.Info("Checking if Spicetify CLI is installed by running 'spicetify --version'.");
        var psi = new ProcessStartInfo("spicetify", "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        try
        {
            using (var p = Process.Start(psi))
            {
                if (p == null)
                {
                    Logger.Error(
                        "Failed to start 'spicetify --version' process (Process.Start returned null)."
                    );
                    Logger.Debug($"Exiting {nameof(IsSpicetifyInstalled)} with result: false.");
                    return false;
                }

                string versionOutput = p.StandardOutput.ReadToEnd();
                string errorOutput = p.StandardError.ReadToEnd();
                p.WaitForExit(5000);

                bool installed = p.ExitCode == 0;
                Logger.Info(
                    $"'spicetify --version' exited with code {p.ExitCode}. Installed: {installed}."
                );
                if (!string.IsNullOrWhiteSpace(versionOutput))
                    Logger.Debug($"'spicetify --version' STDOUT: {versionOutput.Trim()}");
                if (!string.IsNullOrWhiteSpace(errorOutput))
                    Logger.Warn($"'spicetify --version' STDERR: {errorOutput.Trim()}");

                Logger.Debug($"Exiting {nameof(IsSpicetifyInstalled)} with result: {installed}.");
                return installed;
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Logger.Warn(
                $"'spicetify --version' command failed to start (Win32Exception). This usually means Spicetify is not installed or not in PATH. Message: {ex.Message}"
            );
            Logger.Debug($"Exiting {nameof(IsSpicetifyInstalled)} with result: false.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(
                "An unexpected error occurred while checking Spicetify installation ('spicetify --version').",
                ex
            );
            Logger.Debug($"Exiting {nameof(IsSpicetifyInstalled)} with result: false.");
            return false;
        }
    }

    /// <summary>
    /// Installs Spicetify CLI using the official PowerShell script.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation of installing Spicetify CLI.</returns>
    private static async Task InstallSpicetifyCli()
    {
        Logger.Debug($"Entering {nameof(InstallSpicetifyCli)}.");
        WriteHost(T("InstallingSpicetify"), ConsoleColor.Cyan, "INFO");
        Logger.Info("Attempting to download and execute Spicetify CLI PowerShell install script.");
        try
        {
            string scriptUrl = "https://raw.githubusercontent.com/spicetify/cli/main/install.ps1";
            string tempScriptPath = Path.Combine(Path.GetTempPath(), "install-spicetify.ps1");

            Logger.Debug(
                $"Downloading Spicetify PowerShell install script from '{scriptUrl}' to '{tempScriptPath}'."
            );
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SpicetifyMarketplaceInstaller/1.0");
                var scriptContent = await http.GetStringAsync(scriptUrl);
                await File.WriteAllTextAsync(tempScriptPath, scriptContent);
                Logger.Info("Spicetify PowerShell install script downloaded successfully.");
            }

            var psi = new ProcessStartInfo(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\""
            )
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            WriteHost(T("RunningPowershellInstaller"), ConsoleColor.DarkCyan, "INFO");
            Logger.Info($"Executing PowerShell: {psi.FileName} {psi.Arguments}");

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    Logger.Error("Failed to start PowerShell process for Spicetify installation.");
                    WriteHost(T("InstallationFailed"), ConsoleColor.Red, "ERROR");
                    Logger.Debug(
                        $"Exiting {nameof(InstallSpicetifyCli)} due to process start failure."
                    );
                    return;
                }

                Logger.Debug(
                    $"PowerShell process (PID: {process.Id}) started. Sending 'n' twice to stdin for prompts."
                );
                await process.StandardInput.WriteLineAsync("n");
                await process.StandardInput.WriteLineAsync("n");
                process.StandardInput.Close();
                Logger.Debug("Closed PowerShell process StandardInput.");

                string psOut = await process.StandardOutput.ReadToEndAsync();
                string psErr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                Logger.Info(
                    $"PowerShell script execution finished with ExitCode: {process.ExitCode}."
                );

                if (!string.IsNullOrWhiteSpace(psOut))
                    Logger.Debug($"PowerShell STDOUT:\n{psOut.Trim()}");
                if (!string.IsNullOrWhiteSpace(psErr))
                    Logger.Warn($"PowerShell STDERR:\n{psErr.Trim()}");

                if (process.ExitCode != 0)
                {
                    WriteHost(
                        $"{T("ErrorFromSpicetify")} {T("InstallationFailed")}",
                        ConsoleColor.Red,
                        "ERROR"
                    );
                    if (!string.IsNullOrWhiteSpace(psOut))
                        WriteHost(
                            $"PowerShell Installer Output:\n{psOut.Trim()}",
                            ConsoleColor.Yellow,
                            "WARN"
                        );
                    if (!string.IsNullOrWhiteSpace(psErr))
                        WriteHost(
                            $"PowerShell Installer Error Output:\n{psErr.Trim()}",
                            ConsoleColor.Red,
                            "ERROR"
                        );
                }
                else
                {
                    WriteHost(T("SpicetifyCliInstallScriptFin"), ConsoleColor.Green, "INFO");
                    if (
                        !string.IsNullOrWhiteSpace(psOut)
                        && (
                            psOut.Contains("error", StringComparison.OrdinalIgnoreCase)
                            || psOut.Contains("failed", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        WriteHost(
                            $"PowerShell Installer Output (please review for potential issues):\n{psOut.Trim()}",
                            ConsoleColor.Yellow,
                            "WARN"
                        );
                    }
                }
            }

            try
            {
                File.Delete(tempScriptPath);
                Logger.Info(
                    $"Successfully deleted temporary PowerShell script: '{tempScriptPath}'."
                );
            }
            catch (Exception exDelete)
            {
                Logger.Warn(
                    $"Failed to delete temporary PowerShell script '{tempScriptPath}'.",
                    exDelete.Message
                );
            }
        }
        catch (HttpRequestException httpEx)
        {
            WriteHost(
                $"{T("ErrorLabel")} Failed to download Spicetify install script: {httpEx.Message}",
                ConsoleColor.Red,
                "ERROR"
            );
            Logger.Error(
                "HttpRequestException during Spicetify CLI installation script download.",
                httpEx
            );
        }
        catch (Exception ex)
        {
            WriteHost($"{T("ErrorRunningSpicetify")} {ex.Message}", ConsoleColor.Red, "ERROR");
            Logger.Error("An unexpected error occurred during Spicetify CLI installation.", ex);
        }
        Logger.Debug($"Exiting {nameof(InstallSpicetifyCli)}.");
    }

    /// <summary>
    /// Cleans Spicetify data by running 'spicetify restore' and deleting common Spicetify-related data directories.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation of cleaning Spicetify data.</returns>
    private static async Task CleanSpicetifyDataAsync()
    {
        Logger.Debug($"Entering {nameof(CleanSpicetifyDataAsync)}.");
        WriteHost(T("CleaningSpicetifyData"), ConsoleColor.Yellow, "WARN");
        Logger.Info("Initiating Spicetify data cleaning process.");

        WriteHost("Attempting to run 'spicetify restore'...", ConsoleColor.DarkYellow, "INFO");
        Logger.Info("Executing 'spicetify restore' command.");
        await InvokeSpicetify(new[] { "restore" }, captureOutput: false);
        Logger.Info("'spicetify restore' command execution finished.");

        string appDataSpicetify = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "spicetify"
        );
        string localAppDataSpicetify = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "spicetify"
        );
        string userProfileSpicetify = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".spicetify"
        );

        string[] foldersToDelete =
        {
            appDataSpicetify,
            localAppDataSpicetify,
            userProfileSpicetify
        };
        Logger.Debug(
            $"Potential Spicetify directories to delete: {string.Join("; ", foldersToDelete)}"
        );

        foreach (var folderPath in foldersToDelete)
        {
            if (Directory.Exists(folderPath))
            {
                WriteHost(T("DeletingFolder", folderPath), ConsoleColor.DarkYellow, "INFO");
                Logger.Info($"Attempting to delete directory: '{folderPath}'.");
                try
                {
                    Directory.Delete(folderPath, true);
                    Logger.Info($"Successfully deleted directory: '{folderPath}'.");
                }
                catch (Exception ex)
                {
                    string errorMessage = T("ErrorDeletingFolder");
                    WriteHost(
                        $"{errorMessage} '{folderPath}': {ex.Message}",
                        ConsoleColor.Red,
                        "ERROR"
                    );
                    Logger.Error($"Failed to delete directory '{folderPath}'.", ex);
                }
            }
            else
            {
                Logger.Debug($"Directory not found, skipping deletion: '{folderPath}'.");
            }
        }
        WriteHost(T("SpicetifyFoldersCleaned"), ConsoleColor.Green, "INFO");
        Logger.Info("Spicetify data cleaning process completed.");
        Logger.Debug($"Exiting {nameof(CleanSpicetifyDataAsync)}.");
    }

    /// <summary>
    /// Core logic for installing Spicetify Marketplace.
    /// This includes checking/installing Spicetify CLI, determining paths, downloading Marketplace,
    /// extracting files, and configuring Spicetify.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation of the Marketplace installation logic.</returns>
    /// <exception cref="Exception">Throws an exception if critical steps fail (e.g., Spicetify CLI installation or userdata path retrieval cannot be resolved).</exception>
    private static async Task InstallMarketplaceLogicAsync()
    {
        Logger.Debug($"Entering {nameof(InstallMarketplaceLogicAsync)}.");
        Logger.Info("Starting core Spicetify Marketplace installation logic.");

        if (!IsSpicetifyInstalled())
        {
            WriteHost(T("SpicetifyNotFound"), ConsoleColor.Yellow, "WARN");
            Logger.Warn("Spicetify CLI not detected. Attempting installation.");
            await InstallSpicetifyCli();
            if (!IsSpicetifyInstalled())
            {
                string errorMsg = T("SpicetifyNotFound") + " " + T("InstallationFailed");
                WriteHost(errorMsg, ConsoleColor.Red, "ERROR");
                Logger.Error(
                    "Spicetify CLI could not be installed or detected after installation attempt. This is a critical failure."
                );
                Logger.Debug(
                    $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to Spicetify CLI unavailability."
                );
                throw new Exception(errorMsg);
            }
            WriteHost(
                "Spicetify CLI has been installed/verified successfully.",
                ConsoleColor.Green,
                "INFO"
            );
            Logger.Info("Spicetify CLI successfully installed or verified post-attempt.");
        }
        else
        {
            Logger.Info("Spicetify CLI is already installed and detected.");
        }

        string spiceUserDataPath = "";
        Logger.Info(
            "Attempting to determine Spicetify userdata path via 'spicetify path userdata'."
        );
        var userDataResult = await InvokeSpicetify(
            new[] { "path", "userdata" },
            captureOutput: true
        );

        WriteHost(T("SpicetifyPathCommandOutput"), ConsoleColor.DarkGray, "DEBUG");
        WriteHost(userDataResult.Output, ConsoleColor.DarkGray, "DEBUG");
        Logger.Debug(
            $"Raw output from 'spicetify path userdata' (Exit Code: {userDataResult.ExitCode}):\n{userDataResult.Output}"
        );

        if (userDataResult.ExitCode != 0)
        {
            WriteHost(T("ErrorFromSpicetify"), ConsoleColor.Red, "ERROR");
            WriteHost(userDataResult.Output, ConsoleColor.Red, "ERROR");
            Logger.Error(
                $"'spicetify path userdata' command failed with exit code {userDataResult.ExitCode}. Output: {userDataResult.Output}"
            );
        }

        string foundPath = userDataResult
            .Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && Directory.Exists(line));

        if (!string.IsNullOrWhiteSpace(foundPath))
        {
            spiceUserDataPath = foundPath;
            Logger.Info(
                $"Successfully parsed Spicetify userdata path from command output: '{spiceUserDataPath}'."
            );
        }
        else
        {
            string fallbackPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "spicetify"
            );
            WriteHost(T("SpicetifyPathInvalidFallback", fallbackPath), ConsoleColor.Yellow, "WARN");
            Logger.Warn(
                $"Failed to get a valid Spicetify userdata path from 'spicetify path userdata' command (or command failed). Using fallback path: '{fallbackPath}'. Raw command output was: {userDataResult.Output}"
            );
            spiceUserDataPath = fallbackPath;
        }

        WriteHost(
            $"{T("SpicetifyUserDataPath")}{spiceUserDataPath}",
            ConsoleColor.DarkGray,
            "INFO"
        );
        Logger.Info($"Using Spicetify UserData Path: '{spiceUserDataPath}'.");

        if (string.IsNullOrWhiteSpace(spiceUserDataPath))
        {
            Logger.Error(
                "Spicetify userdata path is empty even after attempting fallback. This is a critical error."
            );
            Logger.Debug(
                $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to empty userdata path."
            );
            throw new Exception(T("FailedToGetSpicetifyPath"));
        }

        try
        {
            if (!Directory.Exists(spiceUserDataPath))
            {
                Logger.Info(
                    $"Spicetify userdata directory '{spiceUserDataPath}' does not exist. Attempting to create it."
                );
                Directory.CreateDirectory(spiceUserDataPath);
                Logger.Info($"Created Spicetify userdata directory: '{spiceUserDataPath}'.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                $"Failed to create Spicetify userdata directory '{spiceUserDataPath}'.",
                ex
            );
            WriteHost(
                $"{T("ErrorLabel")} Could not create Spicetify directory '{spiceUserDataPath}'. Please check permissions and path validity.",
                ConsoleColor.Red,
                "ERROR"
            );
            Logger.Debug(
                $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to failure creating userdata directory."
            );
            throw;
        }

        string customAppsBasePath = Path.Combine(spiceUserDataPath, "CustomApps");
        string themesBasePath = Path.Combine(spiceUserDataPath, "Themes");
        string marketAppPath = Path.Combine(customAppsBasePath, "marketplace");
        string marketThemePath = Path.Combine(themesBasePath, "marketplace");

        Logger.Debug($"Marketplace CustomApp Path will be: '{marketAppPath}'.");
        Logger.Debug($"Marketplace Theme Path will be: '{marketThemePath}'.");

        WriteHost(T("RemovingCreatingMarketplaceFolders"), ConsoleColor.Cyan, "INFO");
        try
        {
            Action<string> deleteDirIfExists = (path) =>
            {
                if (Directory.Exists(path))
                {
                    Logger.Info($"Existing directory found at '{path}'. Deleting it.");
                    try
                    {
                        Directory.Delete(path, true);
                        Logger.Info($"Successfully deleted directory: '{path}'.");
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = T("ErrorDeletingFolder");
                        WriteHost(
                            $"{errorMsg} '{path}': {ex.Message}",
                            ConsoleColor.Yellow,
                            "WARN"
                        );
                        Logger.Warn($"Error deleting directory '{path}'.", ex.Message);
                    }
                }
            };
            deleteDirIfExists(marketAppPath);
            deleteDirIfExists(marketThemePath);

            Logger.Info(
                $"Creating CustomApps base directory if not exists: '{customAppsBasePath}'."
            );
            Directory.CreateDirectory(customAppsBasePath);
            Logger.Info($"Creating Themes base directory if not exists: '{themesBasePath}'.");
            Directory.CreateDirectory(themesBasePath);

            Logger.Info($"Creating Marketplace application directory: '{marketAppPath}'.");
            Directory.CreateDirectory(marketAppPath);
            Logger.Info($"Creating Marketplace theme directory: '{marketThemePath}'.");
            Directory.CreateDirectory(marketThemePath);
            Logger.Info("Marketplace directories prepared successfully.");
        }
        catch (Exception ex)
        {
            WriteHost($"{T("ErrorLabel")} {ex.Message.Trim()}", ConsoleColor.Red, "ERROR");
            Logger.Error("Exception during Marketplace directory preparation.", ex);
            Logger.Debug(
                $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to directory preparation failure."
            );
            throw;
        }

        WriteHost(T("DownloadingMarketplace"), ConsoleColor.Cyan, "INFO");
        string marketArchivePath = Path.Combine(Path.GetTempPath(), "marketplace.zip"); // Download to temp first
        string expectedExtractedDirName = "marketplace-dist";
        string extractedContentSourceDir = Path.Combine(marketAppPath, expectedExtractedDirName);

        Logger.Debug($"Marketplace archive will be downloaded to: '{marketArchivePath}'.");
        Logger.Debug(
            $"Expected extracted content subdirectory within Marketplace app path: '{expectedExtractedDirName}' (full path: '{extractedContentSourceDir}')."
        );

        try
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SpicetifyMarketplaceInstaller/1.0");
                var marketplaceZipUrl =
                    "https://github.com/spicetify/marketplace/releases/latest/download/marketplace.zip";
                Logger.Info($"Downloading Marketplace from: {marketplaceZipUrl}");
                var resp = await http.GetAsync(marketplaceZipUrl);
                resp.EnsureSuccessStatusCode();
                using (
                    var fs = new FileStream(
                        marketArchivePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None
                    )
                )
                {
                    await resp.Content.CopyToAsync(fs);
                }
                Logger.Info(
                    $"Marketplace archive downloaded successfully to '{marketArchivePath}' ({new FileInfo(marketArchivePath).Length} bytes)."
                );
            }

            WriteHost(T("UnzippingAndInstalling"), ConsoleColor.Cyan, "INFO");
            Logger.Info($"Extracting '{marketArchivePath}' to '{marketAppPath}'.");
            ZipFile.ExtractToDirectory(marketArchivePath, marketAppPath, true);
            Logger.Info("Marketplace archive extraction complete.");

            if (!Directory.Exists(extractedContentSourceDir))
            {
                Logger.Warn(
                    $"Expected extracted subdirectory '{expectedExtractedDirName}' not found directly in '{marketAppPath}'. Checking for alternatives."
                );
                var extractedDirs = Directory.GetDirectories(marketAppPath);
                string actualExtractedDir = null;

                if (
                    extractedDirs.Length == 1
                    && (
                        Path.GetFileName(extractedDirs[0])
                            .Equals(expectedExtractedDirName, StringComparison.OrdinalIgnoreCase)
                        || Path.GetFileName(extractedDirs[0])
                            .ToLowerInvariant()
                            .Contains("marketplace")
                    )
                )
                {
                    actualExtractedDir = extractedDirs[0];
                    WriteHost(
                        T("DetectedExtractedFolder")
                            + $"{Path.GetFileName(actualExtractedDir)}{T("InsteadOf")}{expectedExtractedDirName})",
                        ConsoleColor.DarkYellow,
                        "WARN"
                    );
                    Logger.Info(
                        $"Detected extracted content in alternative subdirectory: '{actualExtractedDir}' instead of the expected '{expectedExtractedDirName}'."
                    );
                }
                else if (
                    Directory
                        .GetFiles(marketAppPath)
                        .Any(f =>
                            f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)
                        )
                    && extractedDirs.Length == 0
                )
                {
                    actualExtractedDir = null;
                    WriteHost(T("FilesExtractedDirectly"), ConsoleColor.DarkYellow, "WARN");
                    Logger.Info(
                        "Files seem to be extracted directly into Marketplace application path. No move from subdirectory needed."
                    );
                }
                else
                {
                    WriteHost(
                        $"{T("WarningExpectedFolderNotFound")}{expectedExtractedDirName}{T("NotFoundConsiderManual")}",
                        ConsoleColor.Yellow,
                        "WARN"
                    );
                    Logger.Warn(
                        $"Expected extracted subdirectory '{expectedExtractedDirName}' not found, and no clear alternative was identified. Files might be misplaced. Check contents of '{marketAppPath}'."
                    );
                }
                extractedContentSourceDir = actualExtractedDir;
            }

            if (extractedContentSourceDir != null && Directory.Exists(extractedContentSourceDir))
            {
                string subDirName = Path.GetFileName(extractedContentSourceDir);
                WriteHost(
                    T("MovingItemsFrom") + $"{subDirName}{T("ToMarketplaceRoot")}",
                    ConsoleColor.DarkCyan,
                    "INFO"
                );
                Logger.Info(
                    $"Moving items from subdirectory '{extractedContentSourceDir}' to Marketplace application root '{marketAppPath}'."
                );
                foreach (var file in Directory.GetFiles(extractedContentSourceDir))
                {
                    string destFile = Path.Combine(marketAppPath, Path.GetFileName(file));
                    File.Move(file, destFile, true);
                    Logger.Debug($"Moved file: '{file}' -> '{destFile}'.");
                }
                foreach (var dir in Directory.GetDirectories(extractedContentSourceDir))
                {
                    string destDir = Path.Combine(marketAppPath, new DirectoryInfo(dir).Name);
                    if (Directory.Exists(destDir))
                    {
                        Logger.Debug(
                            $"Target directory '{destDir}' already exists. Deleting before move."
                        );
                        Directory.Delete(destDir, true);
                    }
                    Directory.Move(dir, destDir);
                    Logger.Debug($"Moved directory: '{dir}' -> '{destDir}'.");
                }
                try
                {
                    Directory.Delete(extractedContentSourceDir, true);
                    Logger.Info(
                        $"Successfully deleted source extracted subdirectory: '{extractedContentSourceDir}'."
                    );
                }
                catch (Exception exDelSubDir)
                {
                    Logger.Warn(
                        $"Could not delete source extracted subdirectory '{extractedContentSourceDir}'.",
                        exDelSubDir.Message
                    );
                }
            }

            try
            {
                File.Delete(marketArchivePath);
                Logger.Info(
                    $"Successfully deleted downloaded Marketplace archive: '{marketArchivePath}'."
                );
            }
            catch (Exception exDelArchive)
            {
                Logger.Warn(
                    $"Failed to delete downloaded Marketplace archive '{marketArchivePath}'.",
                    exDelArchive.Message
                );
            }
            Logger.Info("Marketplace files successfully installed into application directory.");
        }
        catch (HttpRequestException httpEx)
        {
            WriteHost(
                $"{T("ErrorLabel")} Failed to download Marketplace: {httpEx.Message}",
                ConsoleColor.Red,
                "ERROR"
            );
            Logger.Error("HttpRequestException during Marketplace download.", httpEx);
            Logger.Debug(
                $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to download failure."
            );
            throw;
        }
        catch (Exception ex)
        {
            WriteHost($"{T("ErrorLabel")} {ex.Message}", ConsoleColor.Red, "ERROR");
            Logger.Error("Exception during Marketplace download/extraction/installation.", ex);
            Logger.Debug(
                $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to installation file operation failure."
            );
            throw;
        }

        WriteHost(T("DownloadingPlaceholderTheme"), ConsoleColor.Cyan, "INFO");
        Logger.Info("Downloading Marketplace placeholder theme (color.ini).");
        try
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SpicetifyMarketplaceInstaller/1.0");
                var themeIniPath = Path.Combine(marketThemePath, "color.ini");
                var themeUrl =
                    "https://raw.githubusercontent.com/spicetify/marketplace/main/resources/color.ini";
                Logger.Debug(
                    $"Marketplace theme color.ini will be saved to: '{themeIniPath}', from URL: '{themeUrl}'."
                );

                var resp = await http.GetAsync(themeUrl);
                resp.EnsureSuccessStatusCode();
                string themeContent = await resp.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(themeIniPath, themeContent);
                Logger.Info(
                    $"Marketplace placeholder theme (color.ini) downloaded and saved to '{themeIniPath}' successfully."
                );
            }
        }
        catch (HttpRequestException httpEx)
        {
            WriteHost(
                $"{T("ErrorLabel")} Failed to download placeholder theme: {httpEx.Message}",
                ConsoleColor.Red,
                "ERROR"
            );
            Logger.Error("HttpRequestException downloading placeholder theme.", httpEx);
            Logger.Debug(
                $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to theme download failure."
            );
            throw;
        }
        catch (Exception ex)
        {
            WriteHost($"{T("ErrorLabel")} {ex.Message.Trim()}", ConsoleColor.Red, "ERROR");
            Logger.Error("Exception downloading/saving placeholder theme.", ex);
            Logger.Debug(
                $"Exiting {nameof(InstallMarketplaceLogicAsync)} due to theme saving failure."
            );
            throw;
        }

        Logger.Info("Checking for existing Spicetify theme configuration.");
        var currentThemeResult = await InvokeSpicetify(
            new[] { "config", "current_theme" },
            captureOutput: true
        );
        string currentThemeName = "";
        bool isAnyThemeEffectivelyApplied = false;

        if (
            currentThemeResult.ExitCode == 0
            && !string.IsNullOrWhiteSpace(currentThemeResult.Output)
        )
        {
            currentThemeName = currentThemeResult.Output.Trim();
            Logger.Info($"Spicetify reported current_theme: '{currentThemeName}'.");
            isAnyThemeEffectivelyApplied =
                !string.IsNullOrEmpty(currentThemeName)
                && !currentThemeName.Equals("blank", StringComparison.OrdinalIgnoreCase)
                && !currentThemeName.Equals("default", StringComparison.OrdinalIgnoreCase)
                && !currentThemeName.Contains("???"); // Spicetify uses "???" for unreadable config
        }
        else
        {
            Logger.Warn(
                $"'spicetify config current_theme' command failed or returned empty. ExitCode: {currentThemeResult.ExitCode}, Output: '{currentThemeResult.Output}'. Assuming no specific theme is set."
            );
        }
        Logger.Debug(
            $"Based on 'config current_theme': currentThemeName='{currentThemeName}', isAnyThemeEffectivelyApplied={isAnyThemeEffectivelyApplied}."
        );

        bool setThemeToMarketplace = true;
        if (isAnyThemeEffectivelyApplied && currentThemeName != "marketplace")
        {
            while (Console.KeyAvailable)
                Console.ReadKey(true);
            Console.WriteLine(T("LocalThemeFound", currentThemeName));
            Console.Write(
                $"{T("ReplaceThemePrompt")} ({T("YesPromptChar")}/{T("NoPromptChar")}) [{T("YesPromptChar")}]: "
            );
            string choice = Console.ReadLine()?.Trim().ToUpperInvariant() ?? T("YesPromptChar");
            Logger.Debug(
                $"User choice for replacing theme '{currentThemeName}' with 'marketplace': '{choice}'."
            );
            if (choice == T("NoPromptChar").ToUpperInvariant())
            {
                setThemeToMarketplace = false;
                Logger.Info(
                    $"User chose NOT to replace existing theme '{currentThemeName}' with 'marketplace' theme."
                );
            }
            else
            {
                Logger.Info(
                    $"User chose to replace existing theme '{currentThemeName}' with 'marketplace' theme (or entered non-No/defaulted)."
                );
            }
        }
        else if (currentThemeName == "marketplace")
        {
            Logger.Info(
                "Current theme is already 'marketplace'. No change needed for theme setting."
            );
            setThemeToMarketplace = false; // Already set, no need to re-apply this specific config.
        }
        else
        {
            Logger.Info(
                "No conflicting theme detected or theme is blank/default. Will set theme to 'marketplace'."
            );
        }

        WriteHost(T("ConfiguringSpicetify"), ConsoleColor.DarkCyan, "INFO");
        Logger.Info(
            "Configuring Spicetify settings: inject_css=1, replace_colors=1, custom_apps=marketplace."
        );
        await InvokeSpicetify(new[] { "config", "inject_css", "1" }, captureOutput: false);
        await InvokeSpicetify(new[] { "config", "replace_colors", "1" }, captureOutput: false);
        if (setThemeToMarketplace)
        {
            WriteHost(T("SettingCurrentThemeMarketplace"), ConsoleColor.DarkCyan, "INFO");
            Logger.Info("Setting Spicetify 'current_theme' to 'marketplace'.");
            await InvokeSpicetify(
                new[] { "config", "current_theme", "marketplace" },
                captureOutput: false
            );
        }
        await InvokeSpicetify(
            new[] { "config", "custom_apps", "marketplace" },
            captureOutput: false
        );
        Logger.Info("Spicetify configuration commands executed.");

        WriteHost(T("BackingUpAndApplying"), ConsoleColor.DarkCyan, "INFO");
        Logger.Info("Running 'spicetify backup' and 'spicetify apply' to finalize changes.");
        await InvokeSpicetify(new[] { "backup" }, captureOutput: false);
        await InvokeSpicetify(new[] { "apply" }, captureOutput: false);
        Logger.Info("'spicetify backup' and 'spicetify apply' commands executed.");

        Logger.Info("Spicetify Marketplace installation logic completed successfully.");
        Logger.Debug($"Exiting {nameof(InstallMarketplaceLogicAsync)}.");
    }

    /// <summary>
    /// Performs a full reinstall of Spicetify Marketplace. This includes cleaning existing Spicetify data
    /// and then running the standard installation logic.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation of the full reinstallation.</returns>
    private static async Task PerformFullReinstallAsync()
    {
        Logger.Debug($"Entering {nameof(PerformFullReinstallAsync)}.");
        WriteHost(T("ForceReinstallStarting"), ConsoleColor.Cyan, "INFO");
        Logger.Info("Starting full (force) reinstall process for Spicetify Marketplace.");
        await CleanSpicetifyDataAsync();
        WriteHost(T("DataCleanedAttemptingFreshInstall"), ConsoleColor.Cyan, "INFO");
        Logger.Info(
            "Spicetify data has been cleaned. Proceeding with a fresh Marketplace installation."
        );
        try
        {
            await InstallMarketplaceLogicAsync();
            WriteHost(T("Done"), ConsoleColor.Green, "INFO");
            Logger.Info("Force reinstall process completed successfully.");
        }
        catch (Exception ex)
        {
            WriteHost(
                $"{T("ErrorLabel")} {T("DuringForceReinstall")}: {ex.Message}",
                ConsoleColor.Red,
                "ERROR"
            );
            Logger.Error("An error occurred during the force reinstall process.", ex);
        }
        Logger.Debug($"Exiting {nameof(PerformFullReinstallAsync)}.");
    }

    /// <summary>
    /// Performs a standard installation of Spicetify Marketplace and then prompts the user for verification.
    /// </summary>
    /// <returns>A Task yielding true if the user confirms the installation was successful, false otherwise (indicating a force reinstall might be chosen next).</returns>
    private static async Task<bool> PerformStandardInstallAsync()
    {
        Logger.Debug($"Entering {nameof(PerformStandardInstallAsync)}.");
        Logger.Info("Starting standard Spicetify Marketplace installation process.");
        try
        {
            await InstallMarketplaceLogicAsync();
            WriteHost(T("Done"), ConsoleColor.Green, "INFO");
            Logger.Info("Standard installation logic completed successfully.");
        }
        catch (Exception ex)
        {
            WriteHost(
                $"{T("ErrorLabel")} {T("DuringStandardInstall")}: {ex.Message}",
                ConsoleColor.Red,
                "ERROR"
            );
            Logger.Error("An error occurred during the standard installation process.", ex);
        }

        WriteHost(T("InstallationCompletePromptTitle"), ConsoleColor.Yellow, "INFO");
        Console.Write(
            $"{T("InstallationCompletePromptQuestion")} ({T("YesPromptChar")}/{T("NoPromptChar")}) [{T("NoPromptChar").ToUpperInvariant()}]: "
        );
        string choice = Console.ReadLine()?.Trim().ToUpperInvariant();
        Logger.Info($"User verification choice after standard install: '{choice}'.");

        if (
            choice == T("YesPromptChar").ToUpperInvariant()
            || choice == T("YesPromptFull").ToUpperInvariant()
        )
        {
            WriteHost(T("GreatSuccess"), ConsoleColor.Green, "INFO");
            Logger.Info("User confirmed successful installation.");
            Logger.Debug($"Exiting {nameof(PerformStandardInstallAsync)} with result: true.");
            return true;
        }
        else
        {
            WriteHost(T("ProceedingWithForceReinstall"), ConsoleColor.Yellow, "WARN");
            Logger.Warn(
                "User indicated installation was not successful or chose force reinstall option."
            );
            Logger.Debug($"Exiting {nameof(PerformStandardInstallAsync)} with result: false.");
            return false;
        }
    }

    /// <summary>
    /// Checks if the current user has administrative privileges on Windows.
    /// On non-Windows platforms, this method currently returns false.
    /// </summary>
    /// <returns>True if the current user is determined to be an administrator (on Windows), false otherwise.</returns>
    private static bool IsUserAdministrator()
    {
        Logger.Debug($"Entering {nameof(IsUserAdministrator)}.");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    Logger.Info(
                        $"Current user Windows administrator status: {isAdmin} (User: {identity.Name})."
                    );
                    Logger.Debug($"Exiting {nameof(IsUserAdministrator)} with result: {isAdmin}.");
                    return isAdmin;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Exception occurred while checking for administrator status on Windows. Assuming not admin.",
                    ex.Message
                );
                Logger.Debug($"Exiting {nameof(IsUserAdministrator)} with result: false.");
                return false;
            }
        }
        Logger.Info(
            "Non-Windows platform detected. Administrator check is not applicable in the same way; returning false."
        );
        Logger.Debug($"Exiting {nameof(IsUserAdministrator)} with result: false.");
        return false;
    }

    /// <summary>
    /// Main entry point for the Spicetify Marketplace Installer application.
    /// Initializes logging, sets language, checks admin rights, and proceeds with installation.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <returns>A Task representing the asynchronous execution of the installer.</returns>
    public static async Task Main(string[] args)
    {
        Logger.Initialize();
        Logger.Info(
            $"SpicetifyMarketplaceInstaller started. Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}"
        );
        Logger.Debug($"Operating System: {RuntimeInformation.OSDescription}");
        Logger.Debug($"Framework: {RuntimeInformation.FrameworkDescription}");
        Logger.Debug(
            $"Command line arguments: {(args.Length > 0 ? string.Join(" ", args) : "None")}"
        );

        try
        {
            if (
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals(
                    "tr",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                _currentLanguage = "tr";
                Logger.Info("Turkish UI language detected and set for installer messages.");
            }
            else
            {
                _currentLanguage = "en";
                Logger.Info(
                    "Defaulting to English language for installer messages (Turkish UI not detected)."
                );
            }
        }
        catch (Exception ex)
        {
            _currentLanguage = "en";
            Logger.Warn(
                $"Error detecting UI language. Defaulting to English. Message: {ex.Message}"
            );
        }

        bool hasBypassAdminFlag = args.Any(a =>
            a.Equals("--bypass-admin", StringComparison.OrdinalIgnoreCase)
            || a.Equals("-a", StringComparison.OrdinalIgnoreCase)
            || a.Equals("-b", StringComparison.OrdinalIgnoreCase)
        );
        bool isCurrentUserAdmin = IsUserAdministrator();
        bool isUsernameLiterallyAdministrator = Environment.UserName.Equals(
            "administrator",
            StringComparison.OrdinalIgnoreCase
        );

        if (hasBypassAdminFlag)
            Logger.Info(
                "--bypass-admin (or alias -a, -b) flag detected in command line arguments."
            );
        if (isCurrentUserAdmin)
            Logger.Info("Current user process is running with Windows Administrator privileges.");
        if (isUsernameLiterallyAdministrator)
            Logger.Info("Current username is literally 'Administrator' (case-insensitive).");

        if (hasBypassAdminFlag || isCurrentUserAdmin || isUsernameLiterallyAdministrator)
        {
            _bypassAdmin = true;
            WriteHost(T("AdminRightsDetected"), ConsoleColor.DarkCyan, "INFO");
            Logger.Info(
                "Bypass admin mode enabled for Spicetify commands due to explicit flag or detected admin rights."
            );
        }
        else
        {
            Logger.Info(
                "No admin rights detected and no bypass-admin flag used. Spicetify commands will run with standard user privileges."
            );
        }

        bool forceReinstallArg = args.Any(a =>
            a.Equals("-f", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--forcereinstall", StringComparison.OrdinalIgnoreCase)
        );
        if (forceReinstallArg)
            Logger.Info(
                "Force reinstall argument (-f or --forcereinstall) detected. Will perform a full reinstall."
            );

        try
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            Logger.Info(
                $"Set System.Net.ServicePointManager.SecurityProtocol to Tls12 | Tls13: {System.Net.ServicePointManager.SecurityProtocol}"
            );
        }
        catch (Exception exProto)
        {
            Logger.Warn(
                $"Could not set Tls13, falling back to Tls12 for SecurityProtocol. Error: {exProto.Message}"
            );
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System
                    .Net
                    .SecurityProtocolType
                    .Tls12;
                Logger.Info(
                    $"Set System.Net.ServicePointManager.SecurityProtocol to Tls12: {System.Net.ServicePointManager.SecurityProtocol}"
                );
            }
            catch (Exception exProto12)
            {
                Logger.Error(
                    "Failed to set SecurityProtocol to Tls12. HTTPS connections might fail.",
                    exProto12
                );
            }
        }

        WriteHost(T("SettingUp"), ConsoleColor.Cyan, "INFO");

        if (forceReinstallArg)
        {
            await PerformFullReinstallAsync();
        }
        else
        {
            bool standardInstallSuccess = await PerformStandardInstallAsync();
            if (!standardInstallSuccess)
            {
                Logger.Info(
                    "Standard installation was not confirmed successful by user or user opted for force reinstall. Proceeding with full reinstall."
                );
                await PerformFullReinstallAsync();
            }
            else
            {
                Logger.Info("Standard installation confirmed successful by user.");
            }
        }

        WriteHost(T("CheckErrors"), ConsoleColor.Yellow, "WARN");
        WriteHost(T("RestartSpotify"), ConsoleColor.Cyan, "INFO");
        Logger.Info("Installation process concluded. Prompting user to press any key to exit.");
        WriteHost(T("PressAnyKeyToExit"), ConsoleColor.Gray);
        Console.ReadKey();
        Logger.Info("User pressed a key to exit. Application terminating.");
    }
}

/// <summary>
/// Provides extension methods for the <see cref="Process"/> class.
/// </summary>
public static class ProcessExtensions
{
    /// <summary>
    /// Waits asynchronously for the associated process to exit.
    /// </summary>
    /// <param name="process">The process to wait for an exit notification.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous wait operation.
    /// The task will complete when the process exits or cancellation is requested.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="process"/> is null.</exception>
    public static Task WaitForExitAsync(
        this Process process,
        CancellationToken cancellationToken = default
    )
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));

        if (process.HasExited)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<object>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        process.EnableRaisingEvents = true;
        process.Exited += (s, e) => tcs.TrySetResult(null);

        if (cancellationToken != default)
        {
            cancellationToken.Register(() =>
            {
                if (!process.HasExited) // Only try to cancel if not already exited
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
            });
        }

        return process.HasExited ? Task.CompletedTask : tcs.Task;
    }
}
