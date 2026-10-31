# Asistanım

Windows için hafif masaüstü uygulaması: **Notlar** ve **Fikirler** sekmeleri.

- Tek `Asistanim.exe`, çift tıkla açılır (kurulum / komut gerekmez, .NET dahil).
- Arka planda / tray'de çalışmaz; pencere kapanınca süreç tamamen biter.
- Madde ekle (Enter), tıkla → detay, basılı tutup sürükle → sırala.
- Kaydedilmemiş değişiklikle çıkarken uyarı verir. `Ctrl+S` ile kaydet.
- Veri: exe ile aynı klasörde `asistanim.txt` (düz metin, elle de düzenlenebilir).

## Exe'yi indirme
Bot yetkisi `.github/workflows` altına dosya yazamadığı için workflow `build/build-windows.yml` içinde duruyor.
Bir kez GitHub web arayüzünde bu dosyayı `.github/workflows/build-windows.yml` olarak kopyala/taşı.
Sonra Actions → **Build Windows exe** → Run workflow. Exe, **Releases → latest** altına ve
`Asistanim-windows` artifact'ine konur.
Yerelde: `dotnet publish src/Asistanim.csproj -c Release -o publish`
