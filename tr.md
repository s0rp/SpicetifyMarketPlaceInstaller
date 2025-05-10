# Spicetify Marketplace Installer

### [English Version](https://github.com/s0rp/SpicetifyMarketPlaceInstaller/blob/main/README.md)

Bu, [Spicetify](https://github.com/spicetify/cli) & [Spicetify Marketplace](https://github.com/spicetify/marketplace) kurulumunu ve eğer yüklü değilse Spicetify CLI'ın kendisini otomatikleştirmek için C# ile oluşturulmuş bir komut satırı yardımcı programıdır. Özellikle manuel kurulum adımlarında sorun yaşayabilecek kullanıcılar için daha sorunsuz bir kurulum deneyimi sunmayı amaçlar (`--bypass-admin` bayrağı desteği ile).

## Ne Yapar?

Yükleyici aşağıdaki eylemleri gerçekleştirir:

1.  **Spicetify CLI Kontrolü**: Spicetify CLI'ın yüklü ve erişilebilir olup olmadığını doğrular.
2.  **Spicetify CLI Kurulumu (gerekirse)**: Spicetify CLI bulunamazsa, resmi PowerShell kurulum betiğini indirir ve çalıştırır. Bu kurulum sırasındaki istemleri otomatik olarak yönetir.
3.  **Spicetify Yollarını Belirler**: Gerekli Spicetify `userdata` dizinini bir yedek mekanizma ile bulur.
4.  **Marketplace'i İndirir**: Spicetify Marketplace'in en son sürümünü (`marketplace.zip`) GitHub'dan alır.
5.  **Marketplace'i Kurar**:
    *   `CustomApps/marketplace` ve `Themes/marketplace` içindeki önceki Marketplace kurulumlarını temizler.
    *   İndirilen dosyaları `CustomApps/marketplace` dizinine çıkarır.
    *   Yaygın arşiv çıkarma yapılarını akıllıca yönetir (örneğin, iç içe geçmiş bir `marketplace-dist` klasöründen dosyaları taşıma).
    *   Marketplace tema işlevselliği için gerekli olan `marketplace` yer tutucu temasını (`color.ini`) indirir ve `Themes/marketplace` içine kurar.
6.  **Spicetify'ı Yapılandırır**:
    *   Gerekli Spicetify yapılandırmalarını ayarlar (`inject_css=1`, `replace_colors=1`, `custom_apps=marketplace`).
    *   Eğer mevcut bir tema aktifse, 'marketplace' temasına geçilip geçilmeyeceğini sorar (önerilir).
    *   Değişiklikleri `spicetify backup` ve `spicetify apply` komutlarını çalıştırarak uygular.
7.  **Günlükleme (Logging)**: Çalıştırılabilir dosya ile aynı dizinde ayrıntılı bir `log.txt` dosyası oluşturur, tüm adımları ve karşılaşılan hataları kaydeder.
8.  **Kullanıcı Seçenekleri**: Gelişmiş kontrol için komut satırı argümanlarını ve doğrulama için etkileşimli istemleri destekler.

## Özellikler

*   **Otomatik Spicetify CLI Kurulumu**: Önce Spicetify CLI'ı manuel olarak kurmanıza gerek yoktur.
*   **En Son Marketplace Sürümü**: Her zaman Marketplace'in en yeni sürümünü indirir.
*   **Zorla Yeniden Kurulum Seçeneği**: Sorunlar ortaya çıkarsa, Spicetify veri klasörlerini temizleme dahil olmak üzere temiz bir yeniden kurulum sağlar.
*   **Yönetici Atlama Desteği**: Yükleyici yönetici haklarıyla çalıştırılırsa veya bayrak sağlanırsa Spicetify'ın `--bypass-admin` bayrağını otomatik olarak kullanabilir.
*   **Yerelleştirme**: Konsol çıktısı İngilizce veya Türkçe.
*   **Ayrıntılı Günlükleme**: Sorun giderme için kapsamlı `log.txt`.

## Ön Gereksinimler

*   **Windows İşletim Sistemi**: Yükleyici öncelikle Windows için tasarlanmıştır (Spicetify'ın doğası ve PowerShell kullanımı nedeniyle).
*   **.NET Çalışma Zamanı (Runtime)**: Derlenmiş `.exe` dosyasını çalıştırmak için uyumlu bir .NET Çalışma Zamanı'nın kurulu olması gerekir (derlemeye bağlı olarak .NET 5.0 veya üstü).
*   **PowerShell**: Spicetify CLI kurulum betiği için gereklidir. Genellikle Windows'ta varsayılan olarak bulunur.
*   **İnternet Bağlantısı**: Spicetify CLI ve Marketplace'i indirmek için.

## Nasıl Kullanılır?

1.  **İndirin**: Bu yükleyicinin derlenmiş `.exe` dosyasını edinin.
2.  **Çalıştırın**: `.exe` dosyasını bir komut isteminden çalıştırın veya çift tıklayın.
    ```bash
    SpicetifyMarketplaceInstaller.exe
    ```

### Komut Satırı Argümanları:

*   `-f` veya `--forcereinstall`:
    *   "Zorla yeniden kurulum" gerçekleştirir. Bu şunları yapar:
        1.  Mevcut Spicetify verilerini temizlemeye çalışır (`spicetify restore` çalıştırır ve `%APPDATA%/spicetify`, `%LOCALAPPDATA%/spicetify` gibi Spicetify klasörlerini siler).
        2.  Spicetify CLI'nın (gerekirse) ve Marketplace'in yeni bir kurulumuyla devam eder.
    *   Önceki bir kurulum denemesi başarısız olduysa veya Marketplace düzgün çalışmıyorsa kullanışlıdır.

*   `--bypass-admin` (veya takma adları `-a`, `-b`):
    *   Eğer bu bayrak sağlanırsa VEYA yükleyici Yönetici ayrıcalıklarıyla çalıştırılırsa, yürüttüğü tüm `spicetify` komutlarına otomatik olarak `--bypass-admin` bayrağını geçirir.
    *   Bu, Spicetify Spotify dosyalarını değiştirmeye çalıştığında izinle ilgili sorunları çözmeye yardımcı olabilir.

**Örnek Kullanımlar:**

*   Standart kurulum:
    ```bash
    SpicetifyMarketplaceInstaller.exe
    ```
*   Zorla yeniden kurulum:
    ```bash
    SpicetifyMarketplaceInstaller.exe -f
    ```
*   Yönetici atlama ile çalıştırma (eğer yükleyiciyi zaten yönetici olarak çalıştırmıyorsanız ancak Spicetify'ın kendi atlamasını kullanmasını istiyorsanız):
    ```bash
    SpicetifyMarketplaceInstaller.exe --bypass-admin
    ```
    (Not: `SpicetifyMarketplaceInstaller.exe` dosyasını Yönetici olarak çalıştırırsanız, yükleyicinin `--bypass-admin` bayrağını açıkça sağlamasanız bile Spicetify komutları için `--bypass-admin` bayrağı otomatik olarak kullanılır.)

## Kurulum Sonrası

1.  **Spotify'ı Yeniden Başlatın**: Tüm değişikliklerin etkili olması için Spotify'ı **TAMAMEN** yeniden başlatmanız gerekir. Bu, sistem tepsisinden (eğer orada çalışıyorsa) çıkıp ardından yeniden açmanız anlamına gelir.
2.  **Marketplace'i Kontrol Edin**: Spotify'ın sol kenar çubuğunda "Marketplace" sekmesini arayın.

## Sorun Giderme

*   **`log.txt` Dosyasını Kontrol Edin**: Bu dosya, `SpicetifyMarketplaceInstaller.exe` ile aynı dizinde oluşturulur. Her adım ve karşılaşılan hatalar hakkında ayrıntılı bilgiler içerir. Bir şeyler ters giderse bakılacak ilk yer burasıdır.
*   **Spotify Doğru Yeniden Başlatılmadı**: Spotify'ın tamamen kapatıldığından (sistem tepsisinden çıkıldığından) ve yeniden açıldığından emin olun.
*   **İzinler**: Spicetify komutları başarısız olursa, yükleyiciyi Yönetici olarak çalıştırmayı deneyin. Yükleyici bu durumda Spicetify'ın `--bypass-admin` işlevini otomatik olarak kullanmaya çalışacaktır.
*   **Antivirüs/Güvenlik Duvarı**: Güvenlik yazılımınızın yükleyiciyi, ağ bağlantılarını (indirmeler için) ya da PowerShell yürütmesini engellemediğinden emin olun.
*   **Zorla Yeniden Kurulum**: Sorunlar devam ederse, yükleyiciyi `-f` veya `--forcereinstall` bayrağıyla çalıştırmayı deneyin.
