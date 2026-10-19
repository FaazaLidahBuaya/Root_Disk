using System;
using Root.Core.Mvvm;
using Root.Core.Services;

namespace Root.App.ViewModels;

public class AboutViewModel : ViewModelBase
{
    private readonly IAdminService? _adminService;

    public AboutViewModel(IAdminService? adminService = null)
    {
        _adminService = adminService;
    }

    public string AppName => "ROOT";
    public string Version => "Fase 8.0 (Audit Menyeluruh, Kesiapan Rilis & Distribusi)";
    public string Description => "Sistem Penjelajah dan Analisis Penyimpanan Cepat untuk Windows dengan Dasbor Visual Modern, Pemantauan Kapasitas Drive, Riwayat Pemindaian SQLite, Komparasi Historis Baru vs Lama, Treemap Interaktif, Manajemen Hak Administrator UAC, Ketahanan Sistem Berkas, Pencari Duplikat SHA-256, Pembersihan Konservatif, Audit Keamanan Menyeluruh, dan Jaminan Privasi Penuh Tanpa Telemetri.";
    
    public bool IsRunningAsAdmin => _adminService?.IsRunningAsAdmin() ?? false;
    public string AdminPrivilegeText => IsRunningAsAdmin 
        ? "Administrator (Elevasi Penuh)" 
        : "Pengguna Standar (Hak Terbatas)";

    public string SecurityGuarantee => 
        "ROOT mematuhi batas keamanan Windows: analisis pembersihan berorientasi konservatif dengan proteksi direktori sistem Windows (C:\\Windows, System32, Program Files), pemeriksaan ulang target (pre-deletion re-validation), konfirmasi eksplisit pengguna, dan jaminan keamanan tanpa penghapusan berkas otomatis serta tanpa pengiriman data keluar (100% offline & privat).";

    public string OsVersion => $"{Environment.OSVersion} ({Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")})";
    public string DotNetVersion => $".NET {Environment.Version}";
    public int ProcessorCount => Environment.ProcessorCount;
    public string MachineName => Environment.MachineName;
    public string UserName => Environment.UserName;

    public string PhaseFeatures => 
@"Cakupan Fitur ROOT (Fase 1 - 8 Lengkap):

⚡ FASE 1: Fondasi & Pemindai Berkas Berkecepatan Tinggi
- Pemilihan Drive lokal otomatis & Pemilihan Folder kustom.
- Pemindaian rekursif asinkron multi-threading dengan pelaporan kemajuan waktu nyata.
- Perhitungan ukuran bita dan konversi format cerdas (B, KB, MB, GB, TB).
- Pembatalan instan berbasis CancellationToken tanpa UI freeze.
- Pencatatan aktivitas sistem lengkap (Log Audit) dengan ekspor dan clipboard.

🗺️ FASE 2: Treemap Interaktif Proporsional
- Mesin Squarified Treemap Layout berbasis data nyata pemindaian.
- Navigasi drill-down direktori dan jejak lokasi (breadcrumb trail).
- Tooltip hover interaktif, pemilihan kotak, dan inspektor item rincian.
- Penyesuaian responsif dinamis saat ukuran jendela berubah.

🌳 FASE 3: Eksplorasi Hierarkis & Penyaringan
- Pengurutan data instan dalam memori (Ukuran, Nama, Tanggal Diubah).
- Penyaringan ekstensi berkas dinamis dan batas ukuran minimum.
- Tampilan pohon hierarkis responsif sinkron dengan treemap.
- Halaman eksplorasi 100 berkas berukuran terbesar (Large Files Explorer).

🛡️ FASE 4: Hak Administrator Windows & Ketahanan Sistem Berkas
- Deteksi status hak akses Administrator waktu nyata via Windows Identity & Principal.
- Elevasi aplikasi aman melalui dialog UAC Windows ('runas' verb).
- Penanganan penolakan/pembatalan dialog UAC oleh pengguna (Win32 Error 1223) tanpa crash.
- Penanganan izin ditolak (UnauthorizedAccessException) dengan pencatatan rapi.
- Ketahanan terhadap folder/berkas yang hilang atau dihapus di tengah pemindaian (DirectoryNotFoundException & FileNotFoundException).
- Proteksi siklus tautan simbolis dan NTFS Junction points.
- Jaminan integritas: Hasil pemindaian parsial TIDAK PERNAH dilaporkan sebagai pemindaian lengkap (status SelesaiSebagian / Dibatalkan).
- Kepatuhan keamanan Windows: strictly read-only, tanpa bypass keamanan, tanpa manipulasi ACLs atau take-ownership otomatis.

👥 FASE 5: Pencarian Berkas Duplikat Berbasis Ukuran & Kriptografi SHA-256
- Pengelompokan awal kandidat duplikat berdasarkan ukuran bita identik (size-grouping) yang mengeliminasi >80-99% berkas tanpa pembacaan disk.
- Verifikasi kriptografi hash SHA-256 penuh untuk memastikan isi berkas 100% identik tanpa kesalahan.
- Optimasi kinerja berkas besar melalui pre-filter hash parsial (header hash) dan streaming FileStream async aman RAM.
- Penanganan cerdas berkas kosong (0-byte) dengan hash konstan terverifikasi dan opsi sertakan/abaikan.
- Ketahanan terhadap berkas yang berubah ukuran/isinya saat pemeriksaan berlangsung.
- Penanganan kegagalan izin (UnauthorizedAccessException), berkas hilang, dan pembatalan instan.
- Pengukuran kinerja waktu nyata: throughput bita per detik, jumlah berkas ter-hash, dan durasi.
- Tampilan antarmuka Master-Detail interaktif dengan potensi penghematan ruang penyimpanan.
- Jaminan keamanan mutlak: Pencari duplikat TIDAK PERNAH menghapus berkas secara otomatis.

🧹 FASE 6: Analisis Kandidat Pembersihan Konservatif & Proteksi Direktori Sistem
- Analisis kandidat pembersihan konservatif: berkas sementara usang (.tmp, .bak > 14 hari), crash dump usang (.dmp > 14 hari), unduhan terbengkalai (.crdownload > 7 hari), log kadaluarsa (> 30 hari), dan basis data thumbnail lokal lama.
- Prinsip konservatif ketat: TIDAK menganggap semua cache atau berkas besar aman dihapus; berkas aktif/baru dan dokumen pengguna selalu dilindungi.
- Perlindungan sistem otomatis (SystemDirectoryProtection): Direktori C:\Windows, System32, Program Files, WinSxS, boot metadata, dan biner eksekutabel (.sys, .dll, .exe) mutlak ditolak dan dilarang dihapus.
- Pratinjau sebelum tindakan: menampilkan lokasi lengkap, alasan detail, ukuran bita, perkiraan ruang yang dibebaskan, dan tingkat keamanan.
- Konfirmasi eksplisit: tindakan pembersihan hanya berjalan setelah dialog konfirmasi eksplisit disetujui pengguna (pembatalan menghentikan seluruh proses).
- Pemeriksaan ulang target (Pre-Deletion Re-Validation): verifikasi keberadaan, proteksi sistem, kesesuaian ukuran, dan waktu modifikasi sesaat sebelum berkas dihapus. Berkas yang termodifikasi otomatis dilewati.
- Penanganan aman kegagalan: menangani izin akses (UnauthorizedAccessException) dan berkas yang terkunci/digunakan proses lain (IOException) tanpa menghentikan batch.
- Audit menyeluruh dan ekspor laporan pembersihan terstruktur dalam format Markdown berbahasa Indonesia.

📊 FASE 7: Dasbor Modern ROOT, Kapasitas Drive, Riwayat SQLite, & Komparasi Historis
- Dasbor terpadu bergaya visual elegan mengikuti merek ROOT (Dark Theme, Neon Accents, Status Badges).
- Pemantauan kapasitas drive waktu nyata: kapasitas total, ruang terpakai, ruang kosong, dan visualisasi usage bar dua warna proporsional.
- Metrik pemindaian komprehensif: total ukuran bita, waktu pemindaian, jumlah berkas, jumlah folder, kecepatan per detik, dan item dilewati/izin ditolak.
- Penanganan status komprehensif: Memuat (Loading), Kosong (Empty), Gagal (Failed), Parsial (Partial), dan Selesai (Completed) secara visual dan informatif.
- Persistensi riwayat pemindaian otomatis ke basis data SQLite lokal (Microsoft.Data.Sqlite) dengan penandaan jelas data historis.
- Fitur komparasi hasil pemindaian: membandingkan pemindaian terkini dengan pemindaian sebelumnya pada target yang sama dengan kalkulasi delta ukuran (+/- MB/GB), delta jumlah berkas, tren pertumbuhan, dan ringkasan perubahan informatif.

🔍 FASE 8: Audit Menyeluruh, Jaminan Privasi, Kesiapan Rilis & Panduan Distribusi
- Audit sistem berkas, junction/symlink loop prevention, dan penanganan pemindaian parsial.
- Audit isolasi hak akses administrator dan peluncuran UAC Windows tanpa manipulasi ACLs.
- Audit integritas algoritma treemap (Squarified aspect ratio clamping, boundary snapping).
- Audit keamanan streaming hash SHA-256 dan deteksi berkas berukuran sama.
- Audit perlindungan berkas sistem dan pemeriksaan ulang ganda pra-penghapusan.
- Verifikasi mutlak privasi: 100% offline, zero network telemetry, zero external data leakage.
- Pembersihan dan pelepasan sumber daya (Dispose, CancellationTokenSource, FileStream, dan koneksi SQLite).
- Kesiapan paket distribusi rilis mandiri (Release build & stand-alone publish).";
}
