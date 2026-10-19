# Laporan Audit ROOT

**Tanggal:** 19 Oktober 2026  
**Cakupan:** pemeriksaan statis kode, struktur, konfigurasi, dan upaya menjalankan build/test.  
**Batasan:** tidak mengubah kode produksi, tidak menjalankan operasi filesystem destruktif, dan tidak menjalankan aplikasi WPF secara interaktif. Prosedur pengujian UI/data sintetis belum dapat dieksekusi sampai toolchain/test runner berfungsi.

## 1. Ringkasan eksekutif

ROOT adalah aplikasi WPF Windows berbahasa Indonesia dengan implementasi nyata untuk pemindaian filesystem, hierarki, treemap, daftar file besar, pencarian duplikat SHA-256, analisis/penghapusan kandidat, riwayat SQLite, log, dan elevasi UAC. Ini bukan sekadar mock UI. Namun, audit belum membuktikan fungsi runtime: build tidak menghasilkan diagnostik, runner tes tidak selesai, dan UI tidak dijalankan.

Temuan terpenting adalah risiko integritas saat pembersihan: file ReadOnly diubah atributnya sebelum penghapusan dan kegagalan delete tidak mengembalikan atribut; validasi target juga berbasis path dan rentan race antara pemeriksaan dengan `File.Delete`. Pemindaian bersifat rekursif dan dapat kehabisan stack pada pohon sangat dalam. Penjumlahan byte menggunakan `long` tanpa overflow check. Treemap dan filter dihitung sinkron saat perubahan UI, sehingga berpotensi membekukan UI pada direktori besar. Tidak ditemukan bukti pengujian runtime yang cukup untuk menyatakan ukuran hasil, jalur sistem, atau pengalaman UI sudah benar.

**Kesimpulan sementara:** belum layak untuk penggunaan sehari-hari atau distribusi; layak hanya untuk pengujian terbatas pada data uji, setelah risiko penghapusan dipahami dan pengujian dijalankan pada mesin Windows yang sesuai.

## 2. Daftar fitur yang diperiksa

| Fitur | Bukti implementasi | Metode pemeriksaan | Hasil audit |
|---|---|---|---|
| Pemindaian folder/drive | `FileSystemScanner`, `DriveService`, `ScannerViewModel` | Statis | Enumerasi file/direktori, progress, cancellation, error parsial, top 50 tersedia. Belum diuji runtime. |
| Hierarki, filter, sortir | `FileTreeService`, `HierarchyViewModel` | Statis | Operasi pohon in-memory tersedia; rekursif. Belum diuji runtime dan skala besar. |
| Treemap & drilldown | `SquarifiedTreemapEngine`, `TreemapViewModel`, `TreemapView` | Statis | Algoritme layout dan navigasi tersedia; tidak ada data dummy tetap terlihat di jalur produksi. Belum diuji render/runtime. |
| File terbesar | `LargeFilesViewModel` | Statis | Mengambil daftar dari hasil scan; belum diuji UI. |
| Duplikat | `DuplicateFinderService`, `DuplicateFinderViewModel` | Statis | Pengelompokan ukuran dan hashing SHA-256 tersedia; belum diuji runtime. |
| Analisis dan pembersihan | `CleanupAnalyzerService`, `CleanupExecutionService`, `CleanupViewModel` | Statis | Ada preview, konfirmasi eksplisit, proteksi, pemeriksaan ulang, dan hapus permanen. Ada temuan integritas (lihat F-01/F-02). Belum dilakukan delete uji. |
| Hak Administrator | `AdminService`, `MainViewModel` | Statis | Pemeriksaan elevasi dan restart melalui UAC tersedia; pembatalan UAC ditangani. Tidak diuji interaktif. |
| Riwayat scan | `SqliteScanHistoryRepository`, `DashboardViewModel` | Statis | SQLite dan perbandingan historis tersedia; belum diuji terhadap database runtime. |
| Log dan ekspor | `LoggerService`, `ActivityLogViewModel` | Statis | Log, salin/ekspor dan bersih log tersedia; belum diuji. |
| Pengaturan/aplikasi distribusi | Struktur proyek dan UI | Statis | Tidak tampak modul pengaturan khusus atau paket installer pada inventaris; ZIP `dist` tersedia. Kesiapan instalasi/update belum terbukti. |

## 3. Temuan bug

### F-01 — Atribut ReadOnly dapat berubah walau penghapusan gagal

- **Keparahan:** Sedang (integritas file; dapat menjadi tinggi jika file penting dipilih).
- **Lokasi:** `Root.Core/Services/CleanupExecutionService.cs`, blok `File.SetAttributes` sebelum `File.Delete` (sekitar baris 197–201).
- **Bukti:** atribut ReadOnly dihapus dari file, kemudian `File.Delete` dijalankan. Jika penghapusan melempar `UnauthorizedAccessException`, `IOException`, atau gagal karena kondisi lain, catch menghasilkan status gagal tetapi atribut asli tidak dipulihkan.
- **Reproduksi yang disarankan:** direktori uji terisolasi, buat file ReadOnly dan buat kondisi delete gagal dengan aman; bandingkan atribut sebelum/sesudah. Belum dijalankan.
- **Hasil diharapkan:** file gagal dihapus mempertahankan atribut dan keadaan awalnya.
- **Hasil aktual dari alur kode:** atribut ReadOnly telah diubah sebelum hasil operasi diketahui dan tidak ada rollback.
- **Dampak:** perubahan file tanpa penghapusan sukses dan tanpa persetujuan terpisah.
- **Rekomendasi:** hindari mengubah atribut; jika benar-benar diperlukan, simpan atribut awal dan pulihkan pada semua jalur kegagalan. Uji hanya pada file sintetis.

### F-02 — Pemeriksaan target pembersihan rentan race (TOCTOU)

- **Keparahan:** Tinggi untuk fitur penghapusan.
- **Lokasi:** `CleanupExecutionService.ProcessSingleCandidate`, pemeriksaan proteksi/keberadaan/ukuran/waktu lalu `File.Delete(path)` (sekitar baris 144–201).
- **Bukti:** proteksi, `File.Exists`, metadata `FileInfo`, dan penghapusan adalah operasi terpisah berbasis nama path. Target dapat diganti setelah pemeriksaan dan sebelum delete. Pemeriksaan ulang mengurangi risiko perubahan biasa, tetapi tidak mengikat objek yang divalidasi dengan objek yang dihapus.
- **Hasil diharapkan:** objek yang dihapus adalah objek yang identitasnya telah divalidasi dan berada dalam batas target yang disetujui.
- **Hasil aktual:** operasi terakhir menghapus apa pun yang berada pada path saat `File.Delete` berjalan.
- **Dampak:** pada skenario race/proses lokal yang mampu mengganti target, objek berbeda dapat terhapus dari path yang disetujui.
- **Rekomendasi:** desain ulang eksekusi dengan validasi identitas file/handle dan kebijakan reparse point yang eksplisit tepat sebelum penghapusan; dokumentasikan batas jaminan Windows. Jangan mengandalkan pemeriksaan timestamp/ukuran saja.

### F-03 — Pemindaian rekursif rentan kedalaman ekstrem dan stack overflow

- **Keparahan:** Sedang.
- **Lokasi:** `Root.Core/Services/FileSystemScanner.cs`, fungsi lokal rekursif `ScanDirectory` (sekitar baris 111 dan pemanggilan sekitar 315).
- **Bukti:** setiap subdirektori memanggil fungsi yang sama pada call stack. Tidak ada batas kedalaman atau traversal iteratif.
- **Reproduksi:** struktur folder sangat dalam di direktori uji; belum dijalankan karena runner belum siap.
- **Dampak:** `StackOverflowException` dapat menghentikan proses dan membatalkan hasil scan.
- **Rekomendasi:** gunakan stack/queue eksplisit, tambah batas aman yang menghasilkan status parsial, dan tes struktur dalam sintetis.

### F-04 — Akumulasi ukuran dapat overflow dan merusak angka/treemap

- **Keparahan:** Sedang; dapat mengacaukan ukuran direktori, statistik, proporsi layout.
- **Lokasi:** `FileSystemScanner.cs` penjumlahan `SizeBytes`/`totalBytesScanned` (sekitar baris 212, 216, 322); `SquarifiedTreemapEngine.cs` penjumlahan `totalBytes` (sekitar baris 33); `FileTreeService.FilterHierarchy` juga mengagregasi ukuran.
- **Bukti:** operasi `long +=` tidak memakai `checked` atau penanganan overflow. Pada overflow, nilai integer dapat wrap dan menjadi negatif/kecil. Treemap berikutnya menganggap ukuran non-positif sebagai item nol byte.
- **Dampak:** ukuran total, folder terbesar, dan luas treemap dapat tidak konsisten pada dataset ekstrem.
- **Rekomendasi:** gunakan penjumlahan checked atau akumulator aman dengan status error/parsial; uji batas long dengan model sintetis dan validasi konsistensi antar panel.

### F-05 — Treemap/filter diproses sinkron pada jalur UI

- **Keparahan:** Sedang untuk scan besar.
- **Lokasi:** `Root.App/ViewModels/TreemapViewModel.cs`, setter filter/sort dan `RecalculateTreemap()` (sekitar baris 158–206, 318–370); `FileTreeService.FilterHierarchy` dan `SortHierarchy` rekursif.
- **Bukti:** perubahan filter langsung memanggil rekalkulasi, transformasi pohon, sort, dan layout tanpa `Task.Run`/virtualisasi/debounce. Hasil dibuat sebagai `ObservableCollection` node.
- **Dampak:** perubahan ukuran jendela, pengetikan pencarian, atau perubahan filter dapat membuat UI tidak responsif pada folder dengan banyak item.
- **Rekomendasi:** ukur pada dataset terkontrol; pindahkan komputasi berat keluar UI thread, debounce input, dan batasi elemen render atau gunakan virtualisasi.

### F-06 — Identitas versi UI tidak konsisten

- **Keparahan:** Rendah.
- **Lokasi:** `Root.App/MainWindow.xaml` menampilkan “Versi 1.0”, sementara `Root.App/Root.App.csproj` menetapkan `Version 8.0.0`.
- **Bukti:** nilai literal UI berbeda dari metadata build.
- **Dampak:** pengguna dan dukungan teknis dapat salah mengidentifikasi versi yang berjalan.
- **Rekomendasi:** binding dari assembly/version metadata yang sama dan cek label versi About serta paket distribusi.

### F-07 — Jalur reparse point root tidak dilewati

- **Keparahan:** Sedang (akurasi/cakupan filesystem).
- **Lokasi:** `Root.Core/Services/FileSystemScanner.cs`, kondisi ReparsePoint mengecualikan jalur root (sekitar baris 155).
- **Bukti:** junction/symlink pada subdirektori dilewati, tetapi jika target pemindaian itu sendiri adalah reparse point, traversal berlanjut. `DirectoryInfo.FullName` dan `visitedPaths` berbasis path leksikal sehingga tidak membuktikan target fisik unik.
- **Dampak:** scan root yang berupa junction dapat melintasi ke lokasi lain yang tidak diperkirakan atau memengaruhi cakupan/angka; pencegahan siklus belum terbukti untuk semua skenario link.
- **Rekomendasi:** tentukan apakah root reparse point ditolak, diikuti secara eksplisit, atau diresolusikan; identifikasi target fisik dan deduplikasi traversal. Uji junction/symlink di lingkungan uji.

### F-08 — Metode `ConvertBack` melempar `NotImplementedException`

- **Keparahan:** Rendah, berpotensi sedang jika binding dua arah ditambahkan.
- **Lokasi:** `Root.App/Converters/ValueConverters.cs` beberapa converter.
- **Bukti:** implementasi `ConvertBack` secara eksplisit melempar `NotImplementedException`.
- **Dampak:** binding dua arah yang memakai converter tersebut akan gagal pada runtime. Pemakaian saat ini perlu dipastikan dari binding XAML; belum ada bukti runtime kegagalan pada alur satu arah.
- **Rekomendasi:** gunakan `Binding.DoNothing`/`DependencyProperty.UnsetValue` untuk converter satu arah atau implementasikan konversi balik untuk binding dua arah.

## 4. Hasil pengujian

### Matriks pengujian

| ID | Fitur | Langkah/tujuan | Hasil yang diharapkan | Hasil aktual | Status | Bukti/log | Keparahan bila gagal |
|---|---|---|---|---|---|---|---|
| T-01 | Inventaris & Git | `git status --short --branch` | Status perubahan dapat dibaca | Gagal: direktori bukan repository Git | Terblokir | Output `fatal: not a git repository` | Informasi |
| T-02 | Restore | `dotnet restore Root.slnx --ignore-failed-sources -p:MSBuildEnableWorkloadResolver=false` | Restore selesai dengan status | Tool tidak mengeluarkan hasil/exit yang dapat diverifikasi | Terblokir | Output kosong pada tool | Informasi |
| T-03 | Build | `dotnet build Root.slnx --no-restore --verbosity minimal -p:MSBuildEnableWorkloadResolver=false` | Build selesai dan diagnostik tersedia | `Build FAILED`, 0 warning, 0 error; penyebab tidak dilaporkan | Terblokir | Output MSBuild | Informasi |
| T-04 | Unit/integrasi | `dotnet test Root.slnx --no-restore --verbosity minimal` | Tes selesai dan hasil pass/fail | Keluar tanpa output hasil yang dapat diverifikasi | Terblokir | Exit code 1 dari perintah awal | Informasi |
| T-05 | Runner langsung | `dotnet vstest Root.Tests/bin/Release/net10.0-windows/Root.Tests.dll --logger:console` | Ringkasan test pass/fail | Mencapai “Starting test execution”; tidak ada ringkasan sampai batas tunggu | Terblokir | Output runner, assembly test lama tersedia | Informasi |
| T-06 | Pemindaian ukuran diketahui | Scan direktori sintetis dengan ukuran yang diketahui | Total file/folder sama dengan pembanding | Tidak dijalankan | Belum diuji | Runtime tidak dapat diverifikasi | Tinggi |
| T-07 | Error akses/parsial | Folder tanpa izin, file hilang saat scan | Status parsial dan skipped/error konsisten | Penanganan tampak dalam kode, tidak diuji runtime | Belum diuji | `FileSystemScanner` | Tinggi |
| T-08 | Cancel/restart | Batal scan, lalu mulai scan baru | Pembatalan berakhir dan scan baru dapat berjalan | Tidak dijalankan | Belum diuji | `ScannerViewModel` dan scanner tersedia | Sedang |
| T-09 | Reparse points | Scan symlink/junction sintetis | Tidak ada siklus; cakupan sesuai kebijakan | Tidak dijalankan | Belum diuji | Ada filter reparse untuk subfolder, root dikecualikan | Sedang |
| T-10 | Treemap geometri | Ukuran diketahui, nol byte, resize, overlap/bounds | Luas sesuai ukuran dan semua node dalam kanvas | Tes kode ada, tetapi runner tidak selesai; render UI tidak dijalankan | Terblokir | `TreemapAlgorithmTests.cs` terinventaris | Sedang |
| T-11 | Navigasi treemap | Drilldown, breadcrumb, naik induk | Path detail dan folder aktif konsisten | Tidak dijalankan | Belum diuji | Binding/command tersedia | Sedang |
| T-12 | Pembersihan aman | Hanya data uji; gagal delete/ReadOnly/race | Tidak mengubah target di luar pilihan; gagal tidak merusak atribut | Tidak dijalankan; temuan F-01/F-02 dari kode | Belum diuji | `CleanupExecutionService` | Tinggi |
| T-13 | Elevasi/UAC | Status elevasi, cancel UAC | Tidak ada elevasi diam-diam; cancel ditangani | Tidak dijalankan interaktif | Belum diuji | Unit test tersedia dalam source | Sedang |
| T-14 | Performa | Banyak file, waktu/CPU/RAM/UI responsiveness | Benchmark terukur dan responsif | Tidak diukur | Belum diuji | Tidak ada hasil benchmark | Sedang |
| T-15 | Distribusi | Jalankan paket pada Windows bersih | Startup, dependency, versi, update jelas | Tidak dijalankan | Belum diuji | Ada folder `dist/ROOT-win-x64` dan ZIP | Tinggi |

**Jumlah status yang dapat dipertanggungjawabkan:** lulus 0, gagal 0, terblokir 6, belum diuji 9. “Terblokir” berarti prosedur tidak menghasilkan verdict fungsional, bukan berarti fitur gagal. Tidak ada tes yang dihitung lulus hanya karena test source atau binary tersedia.

## 5. Kekurangan aplikasi

### Kekurangan fungsional

- Belum terbukti secara runtime pemindaian, pembatalan, navigasi, filter, dan hasil parsial.
- UI Treemap memiliki daftar ekstensi filter terbatas (kombobox XAML hanya sejumlah ekstensi umum), sehingga filter jenis file tertentu belum mudah ditemukan.
- Belum tampak pengaturan produk khusus atau installer/update flow; distribusi hanya berupa hasil `dist` yang ditemukan.
- Tidak ada pengukuran/pelaporan ruang disk yang benar-benar dialokasikan; data yang dipakai adalah `FileInfo.Length`, yaitu ukuran logis. Pengguna dapat menganggap angka tersebut sama dengan ruang cluster fisik padahal tidak.

### Kekurangan keamanan

- Proteksi pembersihan berbasis daftar direktori/ekstensi/nama; bukan batas keamanan menyeluruh. File berekstensi `.exe/.dll/.sys` diblokir secara luas, tetapi file penting dengan ekstensi lain tetap bergantung pada path dan kriteria analyzer.
- Pemeriksaan ulang path tidak mengikat identitas objek file dan rentan TOCTOU (F-02).
- Berkas ReadOnly dimodifikasi sebelum delete tanpa rollback (F-01).
- Error/log menyertakan jalur penuh. Ini berguna untuk audit lokal, tetapi log dapat memuat nama/path sensitif; kebijakan penyimpanan/retensi tidak tampak dari pemeriksaan ini.
- Tidak ada audit runtime koneksi jaringan. Pemeriksaan statis tidak cukup untuk menyatakan tidak ada komunikasi tak terduga di seluruh dependency/runtime.

### Kekurangan kinerja

- Scanner menahan seluruh model file/direktori serta daftar file/dir/top items di memori. Pada volume dengan jutaan entri, penggunaan RAM dapat besar.
- Pemindaian direktori memakai traversal rekursif dan enumerasi serial.
- Filter/hierarki/treemap sinkron dan rekursif (F-03/F-05).
- Tidak ada benchmark CPU, RAM, throughput, maupun jumlah pembaruan UI yang berhasil didapat.

### Kekurangan UI/UX

- Versi di sidebar (1.0) tidak sesuai metadata aplikasi (8.0.0) (F-06).
- Status elevasi tersedia, namun tampilan “admin” tidak membuktikan bahwa semua lokasi dapat dibaca; pesan hasil parsial tetap penting.
- Belum dilakukan inspeksi kontras, skala DPI, jendela kecil, navigasi keyboard, screen reader, dan fokus kontrol secara interaktif.
- Penghapusan permanen dikonfirmasi dan diperingatkan, tetapi belum ada hasil uji alur lengkap pada file uji.

### Kekurangan arsitektur dan pemeliharaan

- Struktur memisahkan `Core`/`App`/`Tests`, tetapi fungsi scan/filter/layout menggunakan rekursi dalam beberapa jalur.
- Tidak ada repo Git pada direktori, sehingga perubahan lokal dan riwayat sulit diverifikasi.
- `ConvertBack` yang melempar exception memerlukan kontrak jelas sebagai converter satu arah (F-08).
- Test suite besar (134 `[Fact]`/`[Theory]` terdeteksi), tetapi CI/toolchain tidak menghasilkan hasil yang bisa diaudit.

### Fitur yang layak ditambahkan

- Sajikan “ukuran logis” dan “ukuran disk teralokasi” secara terpisah jika pengguna perlu mengosongkan storage berdasarkan ruang fisik.
- Simpan informasi cakupan/parsial yang menonjol dan jumlah error agar pengguna tidak menyimpulkan folder kecil karena tidak terbaca.
- Tambahkan opsi kebijakan eksplisit untuk symlink/junction dan identitas hard link agar pemindaian dapat menjelaskan cakupan/potensi hitung ganda.
- Tambahkan validasi instalasi/versi dan halaman informasi build untuk memudahkan dukungan distribusi.

## 6. Risiko yang belum terselesaikan

- Tidak ada verdict build, tes unit, atau integrasi yang dapat dipercaya dari sesi ini; lingkungan dotnet melaporkan kegagalan akses Service Control Manager pada `dotnet --info`, dan proses test tidak mencapai ringkasan.
- UI WPF belum dibuka, sehingga XAML binding, navigasi nyata, responsivitas, aksesibilitas, dan perilaku resize tidak terverifikasi.
- Tidak dilakukan test filesystem sintetis runtime, termasuk izin, jalur panjang, nama Unicode, perubahan file, symlink/junction, dan drive terputus.
- Akurasi ukuran terhadap data yang diketahui belum dibandingkan; ukuran saat ini merepresentasikan panjang logis file, bukan alokasi cluster. Hard link dapat dihitung lebih dari sekali; untuk tautan file, ukuran metadata dapat merefleksikan target, sementara perilaku jalur khusus belum diuji.
- Tidak dilakukan delete uji, sehingga risiko pembersihan tetap penting dan harus diperbaiki/diuji sebelum fitur itu dipakai untuk data pengguna.
- Tidak diperiksa advisory dependency online dan tidak diklaim ada/tidak ada kerentanan dependency.

## 7. Rencana perbaikan berdasarkan prioritas

### P0 — sebelum penggunaan dengan fitur pembersihan

1. Atasi F-02: ikat validasi dan penghapusan pada identitas target yang sama; definisikan kebijakan symlink/reparse point.
2. Atasi F-01: jangan tinggalkan atribut berubah ketika penghapusan gagal; tambah rollback dan tes file sintetis.
3. Jalankan pengujian pembersihan hanya di temp sandbox dengan target sentinel di luar folder sasaran untuk memastikan tidak ada path traversal/race yang menyentuhnya.

### P1 — sebelum rilis

1. Pulihkan toolchain runner dan dapatkan build/test yang berakhir dengan hasil; catat versi SDK, exit code, dan seluruh diagnostik.
2. Ganti rekursi scan/filter/layout yang berisiko kedalaman dengan traversal iteratif atau batas aman (F-03).
3. Terapkan penjumlahan overflow-safe untuk ukuran dan statistik (F-04), lalu uji konsistensi dashboard, hasil scan, list, detail, dan treemap.
4. Tegaskan kebijakan root reparse, junction, symlink, dan hard link (F-07) dan tambahkan tes Windows khusus.
5. Jalankan uji UI manual pada Windows: akses ditolak, cancel, resize, navigasi, DPI/keyboard, serta data sintetis kecil/menengah/besar.
6. Pastikan artefak distribusi dapat dijalankan di VM/mesin Windows bersih dan versi UI sama dengan metadata (F-06).

### P2 — peningkatan penting

1. Pindahkan kalkulasi treemap/filter dari UI thread, debounce pencarian, dan uji responsivitas (F-05).
2. Ukur RAM/CPU/waktu untuk jumlah file bertingkat, serta ukur waktu pembatalan.
3. Audit retensi dan sensitivitas log path; sediakan redaksi/retensi yang sesuai bila diperlukan.
4. Berikan label ukuran logis secara eksplisit dan, bila dibutuhkan, ukuran alokasi terpisah.

### P3 — opsional

1. Perluas filter ekstensi/search dan kemudahan menjelajahi file besar bila data pengguna menunjukkan kebutuhan.
2. Perjelas binding satu arah converter dan tambahkan pemeriksaan XAML/binding saat startup.

## 8. Keputusan kesiapan

**Keputusan: Layak untuk pengujian terbatas.** Gunakan hanya pada data uji atau salinan yang dapat dipulihkan; hindari fitur penghapusan pada data nyata sampai F-01/F-02 ditangani dan diuji. Keputusan ini didukung oleh implementasi produk yang nyata, tetapi build dan test tidak menghasilkan verdict, UI tidak dijalankan, dan pengujian filesystem/benchmark belum berlangsung. Karena itu aplikasi belum layak direkomendasikan untuk pemakaian harian atau distribusi.

| Aspek | Status | Alasan |
|---|---|---|
| Ketepatan fungsi | Belum dapat dinilai | Fitur nyata tampak dalam kode, namun tes runtime tidak selesai. |
| Stabilitas | Belum dapat dinilai | Build/test tidak memberi hasil; rekursi dalam menjadi risiko statis. |
| Keamanan data | Belum siap | Jalur hapus permanen dengan TOCTOU dan atribut ReadOnly tanpa rollback. |
| Akurasi perhitungan | Belum dapat dinilai | Ukuran logis dijumlahkan tanpa perlindungan overflow; belum dibandingkan terhadap fixture runtime. |
| Kinerja | Belum dapat dinilai | Tidak ada benchmark; transformasi UI sinkron berpotensi lambat. |
| Kualitas antarmuka | Belum dapat dinilai | Hanya inspeksi XAML statis; mismatch versi ditemukan. |
| Kemudahan penggunaan | Belum dapat dinilai | Tidak ada sesi pengguna atau uji navigasi/aksesibilitas. |
| Pemeliharaan kode | Siap dengan catatan | Proyek Core/App/Tests dan abstraksi layanan tersedia; Git tidak ada dan beberapa jalur rekursif/exception perlu perhatian. |
| Kesiapan distribusi | Belum siap | Paket `dist` ada, tetapi build, startup pada mesin bersih, versi UI, dan installer belum diverifikasi. |

## 9. Langkah selanjutnya

1. Jalankan ulang build/test di Windows developer environment yang dapat mengakses dotnet workload metadata; perbaiki hambatan toolchain terlebih dahulu tanpa menonaktifkan tes.
2. Tinjau dan perbaiki F-01 serta F-02 sebelum menjalankan pembersihan terhadap data apa pun.
3. Tambahkan tes unit regresi overflow, traversal dalam, root symlink/junction, dan atribut ReadOnly; lakukan tes destructive hanya di direktori temp yang dapat dibuat ulang.
4. Buat fixture sintetis dengan ukuran byte diketahui; bandingkan total scanner, agregasi folder, daftar terbesar, dashboard, dan luas treemap.
5. Jalankan uji manual WPF dengan akses standar dan admin opsional, fokus pada akses ditolak, laporan parsial, UAC cancel, cancel scan, navigasi, resize, dan tampilan kecil.
6. Ukur waktu/RAM/CPU pada set data kecil-menengah-besar dan verifikasi UI tetap responsif.
7. Samakan versi UI/assembly, verifikasi distribusi pada Windows bersih, lalu lakukan audit ulang sebelum keputusan rilis.

## Lampiran A — perintah yang dicoba

| Perintah | Hasil |
|---|---|
| `git status --short --branch` | `fatal: not a git repository` |
| `dotnet --info` | SDK 10.0.401 dan runtime terdeteksi, tetapi tool mengeluarkan exception akses Service Control Manager / Win32 error 5. |
| `dotnet restore Root.slnx --ignore-failed-sources -p:MSBuildEnableWorkloadResolver=false --verbosity minimal` | Output kosong dan tidak ada hasil exit yang dapat diverifikasi dari tool. |
| `dotnet build Root.slnx --no-restore --verbosity minimal -p:MSBuildEnableWorkloadResolver=false` | `Build FAILED`, 0 warning, 0 error, tanpa penyebab. |
| `dotnet test Root.slnx --no-restore --verbosity minimal` | Exit code 1 tanpa ringkasan tes/diagnostik. |
| `dotnet test Root.Tests/Root.Tests.csproj --no-restore --verbosity normal` | MSBuild melaporkan gagal, 0 warning, 0 error. |
| `dotnet vstest Root.Tests/bin/Release/net10.0-windows/Root.Tests.dll --logger:console` | Menemukan 1 assembly dan mulai eksekusi; tidak menghasilkan ringkasan sebelum batas tunggu. |

Tidak ada source produksi yang diubah. Berkas laporan ini adalah satu-satunya artefak baru.

## Addendum pemeriksaan ulang — 19 Oktober 2026

Pemeriksaan ulang dilakukan setelah perubahan yang kini berada pada `main` (`2be0d67`, pesan commit: `feat(root): implement complete Phase 1-8 features with verified audit fixes (F-01 to F-08)`). Working tree Git bersih pada saat pemeriksaan. Ini adalah pemeriksaan kode statis; tidak membuktikan perubahan berjalan sampai build dan tes menghasilkan verdict.

### Perubahan yang tampak memperbaiki temuan sebelumnya

- **F-01 sebagian besar ditangani:** `CleanupExecutionService` menyimpan atribut awal dan mencoba memulihkannya di jalur gagal/finally; ada tes regresi `F01_ReadOnlyFileRollback_WhenCleanupFails_RestoresOriginalAttribute`.
- **F-02 dimitigasi, belum tertutup sepenuhnya:** reparse point ditolak dan target dibuka dengan `FileShare.None` untuk pemeriksaan ulang. Namun `FileStream` dilepas di akhir blok `using` sebelum `File.Delete(path)`. Setelah handle dilepas, ada kembali celah antara objek yang divalidasi dan path yang dihapus. Ini mitigasi race, bukan pengikatan delete ke handle yang sama.
- **F-03 diperbaiki secara struktural:** scanner memakai `Stack<DirectoryScanFrame>` dan batas kedalaman 256; enumerator subdirektori dikelola/dispose melalui frame. Ada tes kedalaman 40 level. Batas 256 tetap membuat hasil lebih dalam menjadi parsial, yang harus terlihat oleh pengguna.
- **F-04 hanya sebagian ditangani:** scanner dan `FileTreeService` memakai checked add dengan saturasi ke `long.MaxValue`/`int.MaxValue`. Ini mencegah wrap-around, tetapi saturasi tetap menghasilkan nilai yang bukan total sebenarnya dan tidak otomatis menandai laporan sebagai parsial. `SquarifiedTreemapEngine` masih memakai `totalBytes += item.SizeBytes` tanpa perlindungan overflow.
- **F-05 belum diatasi:** `TreemapViewModel.RecalculateTreemap()` tetap menjalankan filter, sort, layout, dan pembaruan `Nodes` sinkron. Penanganan resize hanya mengabaikan perubahan kecil; tidak memindahkan kerja berat dari UI thread.
- **F-06 diperbaiki:** XAML kini binding ke `AppVersionText` di `MainViewModel` (Versi 8.0), tidak lagi teks 1.0 statis.
- **F-07 sebagian diperjelas:** scanner mencatat root yang reparse point dan tetap memindainya; subfolder reparse point dilewati. Kode masih tidak menyelesaikan path root ke identitas target fisik/canonical path, jadi kebijakan “ikuti root link” perlu diterangkan dan diuji.
- **F-08 tampak diperbaiki:** tes baru memanggil beberapa `ConvertBack` dan mengharapkan `Binding.DoNothing`; hasil tes belum diketahui karena runner terblokir.

### Hasil build/test ulang

- `dotnet build Root.slnx --no-restore --verbosity minimal` tetap menghasilkan `Build FAILED`, 0 warning, 0 error, tanpa penyebab.
- `dotnet test Root.slnx --no-restore --verbosity minimal` keluar dengan code 1 tanpa output hasil tes.
- Karena itu tidak ada perubahan status tes dari “terblokir”; tes tambahan yang terlihat di source belum dapat dihitung lulus.

### Penilaian ulang

Implementasi **lebih baik secara statis** dibanding audit awal: isu rekursi scanner dan versi UI ditangani, serta ada tes regresi untuk beberapa perbaikan. Namun belum “optimal” dalam arti siap dipakai atau terverifikasi. Sisa prioritas adalah menutup race penghapusan dengan operasi berbasis handle/identitas yang konsisten, membuat overflow treemap konsisten dan terlaporkan, memindahkan kalkulasi berat dari UI thread, dan memperoleh hasil build/test yang nyata. Kesimpulan kesiapan tetap **layak untuk pengujian terbatas**, bukan penggunaan harian/distribusi.
