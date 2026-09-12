# AGENTS.md — Apex (PrasKaa Revit Add-in Development, C#)

AGENTS.md resmi **repo Apex** — single source of truth pengembangan
tool C# Revit PrasKaa. File ini ikut ter-versioning di git, jadi bisa
di-update dari mesin mana pun.

Status saat ini: **Revit 2024 / 2025 / 2026 (termasuk 2026.5) —
multi-version**. Satu codebase, `dotnet build -c Release` sekali → DLL +
manifest ter-deploy ke `Addins\2024`, `Addins\2025`, dan `Addins\2026`
sekaligus (matrix runtime & aturan: §9). Semua tool C# hidup di
**satu add-in hub `Apex`** (satu repo, satu codebase, satu manifest) —
tab ribbon **"Apex"** adalah toolbar utamanya, satu panel per tool.
`UltimateJoinElements` adalah add-in pertama yang terbukti jalan;
semua pelajaran di bawah berasal dari sana, dan ia sudah dimigrasi
masuk sebagai tool pertama Apex (lihat §12).

---

## 0. Aturan wajib (TL;DR)

1. Target frameworks: `net48;net8.0-windows` (multi-version, §9),
   `UseWPF=true`, `PlatformTarget=x64`.
2. Referensi `RevitAPI.dll` + `RevitAPIUI.dll` dari instalasi Revit
   **per-TFM** — net48 → API Revit 2024, net8.0-windows → API Revit 2026 —
   dengan `Private=false`, dan `CopyLocalLockFileAssemblies=false`.
3. **`<Assembly>` di manifest `.addin` harus path absolut ATAU relatif
   ber-subfolder** (`NamaAddin\NamaAddin.dll`). **JANGAN pakai nama file
   polos** — Revit 2026 membaca manifestnya tapi tidak me-load assembly-nya,
   tanpa error sama sekali.
4. Layout deploy: DLL di
   `%AppData%\Autodesk\Revit\Addins\2026\<NamaAddin>\<NamaAddin>.dll`,
   manifest di `%AppData%\Autodesk\Revit\Addins\2026\<NamaAddin>.addin`.
5. **Tutup Revit 2026 sebelum `dotnet build`** (DLL terkunci saat Revit jalan).
6. Add-in unsigned akan memicu dialog keamanan Revit → pilih **"Always Load"**
   sekali; keputusan tersimpan di registry.
7. Selalu sediakan breadcrumb log di `OnStartup` (lihat §6.3) supaya kalau
   add-in tidak muncul, kita tahu apakah `OnStartup` dipanggil.
8. Jangan pernah mengubah DLL milik add-in lain di folder Addins.
9. **Tool C# baru masuk ke repo `Apex`** (folder `Tools/<NamaTool>/`),
   bukan add-in standalone baru, kecuali ada alasan kuat (lihat §12).
10. Seluruh Apex = **satu AddInId** → satu keputusan trust untuk semua
    tool; DLL di-sign lokal `CN=PrasKaa` lewat target `AuthenticodeSign`
    (lihat §3 dan §5).

---

## 1. Prasyarat mesin

- **.NET 8 SDK** — cek dengan `dotnet --list-sdks`. SDK 7.x **tidak bisa**
  build `net8.0` (error `NETSDK1045`). Install: `winget install Microsoft.DotNet.SDK.8`.
  (PC ini juga punya runtime .NET 8 & WindowsDesktop 8.0.x yang dibutuhkan
  Revit 2026 untuk menjalankan add-in.)
- **Revit 2024, 2025, dan 2026** di `C:\Program Files\Autodesk\Revit <tahun>`
  (diatur di `Directory.Build.props`: `RevitAPIPath2024`, `RevitAPIPath2025`,
  `RevitAPIPath`; override per-build dengan
  `dotnet build -p:RevitAPIPath2024="D:\Path\Lain"` dst.).
- **Tidak perlu SDK/tooling tambahan untuk net48** — build net48 lewat
  package NuGet `Microsoft.NETFramework.ReferenceAssemblies`
  (`PrivateAssets=all`), bukan targeting pack/Visual Studio. net48 juga
  **wajib** referensi API Revit 2024 — API 2026 (net8) tidak akan termuat
  di proses .NET Framework (§9). .NET 10 SDK juga belum diperlukan;
  jalur Revit 2027 (= .NET 10) terdokumentasi di §9.1.
- Tidak butuh Visual Studio. VS Code + .NET CLI cukup.

---

## 2. Struktur project

### 2.1 Repo Apex (standar ke depan)

Satu repo → satu csproj → satu DLL + satu manifest. Tab **"Apex"**,
satu panel per tool:

```
Apex/
├── Apex.sln
├── Apex.csproj              # net48;net8.0-windows, UseWPF, referensi RevitAPI(UI) per-TFM
├── Directory.Build.props    # RevitAPIPath2024/2025/2026 (path instalasi Revit)
├── Apex.addin               # <Assembly>Apex\Apex.dll</Assembly>, FullClassName Apex.App
├── App.cs                   # IExternalApplication: tab "Apex" + panel per tool
├── Icons/                   # PNG 16/32 px, embed sebagai <Resource>
├── Tools/
│   └── UltimateJoin/
│       ├── Commands/        # JoinCommand, ConfigureCommand
│       ├── Core/            # CategoryMap, JoinOrderManager, ConfigStore
│       └── UI/              # JoinOrderDialog.xaml(.cs)
└── README.md
```

- Namespace tool: `Apex.Tools.<NamaTool>.*`.
- Menambah tool = buat folder `Tools/<NamaTool>/` + daftarkan panel &
  tombol di `App.OnStartup` (checklist §12.2) — manifest/GUID/deploy tidak
  berubah.

### 2.2 Layout legacy — satu add-in per tool

Berlaku untuk `UltimateJoinElements` sampai dimigrasi ke Apex (§12.3);
jangan dipakai untuk tool baru.

```
NamaAddin/
├── NamaAddin.csproj          # net8.0-windows, UseWPF, referensi RevitAPI(UI)
├── Directory.Build.props     # RevitAPIPath (path instalasi Revit)
├── NamaAddin.addin           # manifest, dibaca Revit saat startup
├── App.cs                    # IExternalApplication: bikin ribbon tab/panel/tombol
├── Commands/                 # satu file per IExternalCommand (tombol)
├── Core/                     # logic murni (bebas UI), model, config store
├── UI/                       # WPF: *.xaml + *.xaml.cs
└── README.md
```

Aturan:
- Logic bisnis di `Core/` harus bebas dari `Autodesk.Revit.UI` bila mungkin,
  supaya bisa diuji tanpa Revit.
- Satu command = satu file di `Commands/`, namespace konsisten
  (`<RootNamespace>.Commands.<Nama>Command`).
- Jangan pernah commit `bin/`, `obj/`.

---

## 3. Build & deploy

```powershell
# Wajib: semua Revit (2024/2025/2026) tertutup
tasklist /FI "IMAGENAME eq Revit.exe" /NH

dotnet build -c Release
```

Target `DeployAddin` di `.csproj` (`AfterTargets="Build"`) otomatis:
1. `MakeDir` folder `%AppData%\Autodesk\Revit\Addins\<versi>\` — untuk
   Apex, sekali jalan untuk semua versi di `DeployRevitVersions` (§9);
2. `MakeDir` folder `%AppData%\Autodesk\Revit\Addins\<versi>\<NamaAddin>\`;
3. copy `.addin` ke root Addins, copy DLL ke subfolder;
4. hapus DLL lama di root (agar tidak ada dua salinan).

Contoh target (salin apa adanya):

```xml
<Target Name="DeployAddin" AfterTargets="Build">
  <ItemGroup>
    <AddinFiles Include="$(ProjectDir)NamaAddin.addin" />
    <BuiltDlls Include="$(TargetDir)NamaAddin.dll" />
  </ItemGroup>
  <MakeDir Directories="$(AppData)\Autodesk\Revit\Addins\2026" />
  <MakeDir Directories="$(AppData)\Autodesk\Revit\Addins\2026\NamaAddin" />
  <Copy SourceFiles="@(AddinFiles)" DestinationFolder="$(AppData)\Autodesk\Revit\Addins\2026" />
  <Copy SourceFiles="@(BuiltDlls)" DestinationFolder="$(AppData)\Autodesk\Revit\Addins\2026\NamaAddin" />
  <Delete Files="$(AppData)\Autodesk\Revit\Addins\2026\NamaAddin.dll" ContinueOnError="true" />
</Target>
```

Untuk Apex: `Apex.addin` → root Addins, `Apex.dll` →
`Addins\<versi>\Apex\` untuk tiap versi di `DeployRevitVersions`
(`2025;2026` untuk TFM net8, `2024` untuk net48 — §9). Contoh target
satu-versi di atas tetap berlaku untuk project legacy single-target.

Target `AuthenticodeSign` (`BeforeTargets="DeployAddin"`, sudah terbukti
jalan di `UltimateJoinElements.csproj` — bawa juga ke `Apex.csproj`)
men-sign DLL dengan sertifikat code-signing lokal `CN=PrasKaa`
(auto-create saat build pertama, umur 20 tahun), jadi dialog keamanan
Revit menampilkan Publisher/Issuer: PrasKaa, bukan "Unknown Publisher".
Urutannya setelah Build dan sebelum DeployAddin supaya DLL yang
di-deploy adalah yang sudah ter-sign:

```xml
<Target Name="AuthenticodeSign" BeforeTargets="DeployAddin">
  <Exec Command="powershell -NoProfile -ExecutionPolicy Bypass -Command &quot;$c = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=PrasKaa' -and $_.HasPrivateKey } | Select-Object -First 1; if (-not $c) { $c = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=PrasKaa' -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(20) }; Set-AuthenticodeSignature -FilePath '$(TargetPath)' -Certificate $c | Out-Null&quot;" />
</Target>
```

Verifikasi setelah build:

```powershell
cmd /c dir "%AppData%\Autodesk\Revit\Addins\2026\NamaAddin.*"
cmd /c dir "%AppData%\Autodesk\Revit\Addins\2026\NamaAddin"
```

---

## 4. Manifest `.addin` (paling rawan)

```xml
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Nama Addin</Name>
    <Assembly>NamaAddin\NamaAddin.dll</Assembly>
    <AddInId>GUID-UNIK-ANDA</AddInId>
    <FullClassName>NamaAddin.App</FullClassName>
    <VendorId>PRASKAA</VendorId>
    <VendorDescription>PrasKaa</VendorDescription>
  </AddIn>
</RevitAddIns>
```

Aturan keras:
- `<Assembly>` **wajib** mengandung folder (`NamaAddin\NamaAddin.dll`) atau
  path absolut. Nama polos = add-in tidak akan pernah start.
- Apex pakai **satu manifest** (`Apex.addin`, `<Name>Apex</Name>`,
  `<FullClassName>Apex.App</FullClassName>`) untuk semua tool — menambah
  tool tidak pernah menyentuh manifest (lihat §12).
- `<FullClassName>` harus persis sama dengan namespace + kelas `App`.
- `<AddInId>` GUID unik. Generate sekali, lalu **jangan diubah**.
  `[System.Guid]::NewGuid()` atau situs GUID generator.
- Cek bentrok: cari GUID lain di semua `.addin`:
  `C:\ProgramData\Autodesk\Revit\Addins\2026\` dan
  `%AppData%\Autodesk\Revit\Addins\2026\`.
- `.addin` taruh di root folder Addins (bukan di dalam subfolder DLL) supaya
  sama seperti add-in yang terbukti jalan (RevitLookup, BIMIL).

---

## 5. Keamanan / trust add-in (Revit 2024+)

DLL tanpa tanda tangan digital akan memicu dialog:

```
TaskDialog "The publisher of this add-in could not be verified. What do you want to do?"
Id : TaskDialog_Security_Unsigned_File_Loading
  1001 : Always Load | 1002 : Load Once | 1003 : Do Not Load
  DefaultButton : 1003
```

- Pilih **"Always Load"** sekali. Pilihan itu disimpan per-AddInId di registry:

  `HKCU\Software\Autodesk\Revit\Autodesk Revit 2026\CodeSigning\<AddInId>`

- Kalau pernah salah pilih **"Do Not Load"**, add-in akan di-skip **tanpa dialog
  lagi** di setiap sesi. Reset (saat Revit tertutup):

  ```powershell
  reg delete "HKCU\Software\Autodesk\Revit\Autodesk Revit 2026\CodeSigning" /v "<AddInId>" /f
  ```

  lalu buka Revit dan pilih "Always Load".

- Ini **berbeda** dari "Add-Ins Manager". File
  `%AppData%\Autodesk\Revit\Autodesk Revit 2026\AddinsData\AddInsSettings.json`
  menyimpan flag `Disabled` (aktif/nonaktif manual). Add-in bisa `Disabled:false`
  tapi tetap diblokir oleh gate CodeSigning di atas.
- **Signing lokal yang sudah terbukti**: `.csproj` men-sign DLL dengan cert
  code-signing self-signed `CN=PrasKaa` (target `AuthenticodeSign`, §3).
  Karena self-signed, Revit tetap bertanya **sekali per build baru**
  (hash DLL berubah tiap build) → jawab "Always Load"; agar tidak ditanya
  sama sekali, trust cert-nya ke `Cert:\CurrentUser\Root`. Satu AddInId
  untuk seluruh Apex berarti hanya ada satu keputusan trust per build.
- Untuk distribusi tanpa prompt: code signing dengan sertifikat tepercaya.
  Self-signed tidak cukup (OS tetap menganggap publisher tidak terverifikasi).

---

## 6. Diagnostik (playbook)

### 6.1 Revit journal (sumber kebenaran utama)

Letak: `%LocalAppData%\Autodesk\Revit\Autodesk Revit 2026\Journals\journal.NNNN.txt`
(nomor terbesar = sesi terbaru).

Cari dengan Grep:
- `Jrn.AddInManifest: NamaAddin.addin` → manifest terbaca? perhatikan
  `AddInVersion`, `AddInCodeSigningStatus`, `AddInLoadFailureMessage`, dan
  angka load time di akhir baris (`0.000000` = assembly TIDAK dimuat).
- `Starting External Application: Nama Addin` → **bukti `OnStartup` dipanggil**.
  Kalau baris ini tidak ada, Revit men-skip add-in sebelum memanggil OnStartup.
- `TaskDialog|Unsigned_File_Loading` → dialog keamanan muncul / hasilnya.
- `API_ERROR|Exception|conflicts with same preloaded module` → error saat load.

### 6.2 Cek status trust & disable

```powershell
reg query "HKCU\Software\Autodesk\Revit\Autodesk Revit 2026\CodeSigning" /s
cat "%AppData%\Autodesk\Revit\Autodesk Revit 2026\AddinsData\AddInsSettings.json"
```

### 6.3 Breadcrumb log di OnStartup (wajib untuk add-in baru)

Tulis baris log dari `OnStartup` ke `%AppData%\<NamaAddin>\startup.log`:

```csharp
private static void Log(string message)
{
    try
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NamaAddin");
        Directory.CreateDirectory(dir);
        File.AppendAllText(Path.Combine(dir, "startup.log"),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
    }
    catch { /* logging tidak boleh menjatuhkan add-in */ }
}
```

- Log ada → `OnStartup` jalan; lihat langkah mana yang gagal.
- Log tidak ada → Revit belum memanggil `OnStartup` (masalah manifest/trust/path).

---

## 7. Pitfall yang sudah terbukti (lesson learned)

1. **`NETSDK1022` Duplicate 'Page'** — dengan `UseWPF=true`, SDK sudah
   meng-glob `*.xaml` jadi `Page`. Jangan tambahkan `<Page Include="..."/>`
   manual di `.csproj`. Hapus blok itu.
2. **WPF implicit usings tidak punya `System.IO`** — `Path`, `File`,
   `Directory` error `CS0103`. Tambahkan `using System.IO;` di file yang
   memakainya (mis. `ConfigStore.cs`).
3. **`<Assembly>` nama polos menyebabkan skip senyap** (lihat §4). Gejala:
   manifest `NoError`, `AddInVersion: 0.0.0.0`, load time `0.000000`, tidak ada
   `Starting External Application`, tidak ada error. Ini yang paling
   menghabiskan waktu di add-in pertama.
4. **Dialog keamanan unsigned** bisa memblokir permanen kalau salah pilih
   (lihat §5).
5. **`dotnet build` gagal copy jika Revit terbuka** (`MSB3021`/`MSB3027`
   file in use). Tutup Revit dulu.
6. **Warning `MSB3277` (Microsoft.VisualBasic / System.Drawing conflict)**
   adalah noise dari DLL bawaan Revit + `Private=false`. **Aman diabaikan**;
   jangan dikejar.
7. **`GenerateAssemblyInfo=false`** membuat `AssemblyVersion=0.0.0.0`. Tidak
   fatal, tapi pertimbangkan memberi `<Version>1.0.0</Version>` agar versi
   terbaca di journal/Add-Ins Manager.

---

## 8. Pola kode

### 8.1 Ribbon (`IExternalApplication`)
- Tab **"Apex"** dimiliki `Apex.App` (satu-satunya `IExternalApplication`);
  panel per tool dibuat dari `OnStartup` yang sama.
- `CreateRibbonTab` melempar `ArgumentException` bila tab sudah ada → tetap
  bungkus `try/catch` sebagai pertahanan (tab bisa sudah dibuat add-in lain).
- `CreateRibbonPanel` **juga** melempar `ArgumentException` bila nama panel
  sudah dipakai di tab yang sama — terjadi nyata: legacy UltimateJoinElements
  gagal load (dialog error saat startup Revit) ketika Apex sudah membuat
  panel "Ultimate Join". Satu nama panel = satu pemilik; kalau dua add-in
  bisa bentrok, bungkus `CreateRibbonPanel` dengan try/catch juga.
- Beri `ToolTip`/`LongDescription`; tambahkan `Image`/`LargeImage` PNG 16/32 px
  bila perlu ikon.

### 8.2 Command (`IExternalCommand`)
- `[Transaction(TransactionMode.Manual)]` + `[Regeneration(RegenerationOption.Manual)]`.
- Bungkus semua perubahan model dalam `using (var t = new Transaction(doc, "..."))`,
  `Commit()` bila sukses, `RollBack()` di `catch` lalu rethrow.
- Tangani `OperationCanceledException` saat selection/pick → `Result.Cancelled`.
- `TransactionMode.ReadOnly` untuk command yang cuma baca (mis. dialog config).

### 8.3 WPF dialog
- Dialog WPF dari command harus `ShowDialog()` di thread UI; jangan ubah model
  di dalam dialog.
- Pindahkan hasil dialog ke command lalu simpan/ubah model setelah dialog tutup.

### 8.4 Config store (pengganti `script.get_config()` pyRevit)
- Konvensi Apex: JSON di `%AppData%\Apex\<NamaTool>\config.json`
  (satu subfolder per tool). Layout legacy
  `%AppData%\<NamaAddin>\config.json` tetap berlaku untuk
  `UltimateJoinElements` sampai dimigrasi (§12.3).
- Selalu `try/catch` baca/tulis; fallback ke default bila korup.
- Untuk sinkron antar-mesin, arahkan ke OneDrive/network share.

### 8.5 ID element & kompatibilitas versi
- Revit 2026: `element.Id.Value` (long).
- Revit 2024/2025: perlu fallback `IntegerValue`. Bungkus di helper
  `GetElementIdValue(ElementId)` supaya multi-version aman.

---

## 9. Multi-version (Revit 2024 / 2025 / 2026 / 2026.5)

Satu codebase, `dotnet build -c Release` sekali → DLL ter-sign + manifest
ter-deploy ke `Addins\2024`, `Addins\2025`, `Addins\2026` sekaligus.
Matrix runtime (fakta Autodesk Support, artikel ".NET 10 transition",
5 Sep 2026):

| Revit | Runtime | Build Apex yang dimuat |
|---|---|---|
| 2024 | .NET Framework 4.8 | `net48` (referensi API Revit **2024**) → `Addins\2024` |
| 2025 | .NET 8 (naik ke .NET 10 ~minggu ke-2 Sep 2026, in-place) | `net8.0-windows` (API 2026) → `Addins\2025` — DLL net8 tetap termuat di host .NET 10 (forward-compatible) |
| 2026 / 2026.5 | .NET 8 → **.NET 10** mulai 2026.5 | `net8.0-windows` → **satu folder `Addins\2026`** untuk dua minor (net8 = runtime terendah yang menang) |
| 2027 | .NET 10 native | **Belum didukung** (defer) — jalurnya di §9.1 |

Aturan keras (semua sudah diterapkan di `Apex.csproj`):

- Multi-target: `<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>`
  + `<AppendTargetFrameworkToOutputPath>true</AppendTargetFrameworkToOutputPath>`
  (wajib — output `bin\Release\net48` vs `net8.0-windows` tidak saling timpa).
- Referensi per-TFM: net48 → `$(RevitAPIPath2024)` (API 2024, era
  `IntegerValue`); net8.0-windows → `$(RevitAPIPath)` (API 2026, era
  `Id.Value`). net48 yang memakai API 2026 (net8) **tidak akan termuat**
  di proses .NET Framework.
- net48 buildable via dotnet CLI tanpa targeting pack:
  `<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies"
  Version="1.0.3" PrivateAssets="all" />` (kondisi net48 saja).
- API net8-only di jalur net48 **haram dipakai langsung**. `ElementId`
  wajib lewat helper `RevitCompat.GetElementIdValue(ElementId)`
  (`Tools/UltimateJoin/Core/RevitCompat.cs`: `#if NETFRAMEWORK` →
  `IntegerValue`, selain itu `Id.Value`).
- `record`/`init` butuh shim `IsExternalInit` untuk net48 — sudah ada di
  `IsExternalInit.cs` (guard `#if NETFRAMEWORK`; harus file sendiri,
  tidak bisa digabung dengan file yang pakai file-scoped namespace).
- Mapping deploy: property `DeployRevitVersions` per-TFM
  (net48 → `2024`; net8.0-windows → `2025;2026`); target `DeployAddin`
  mem-batch `%(DeployFolders.Identity)` atas versi-versi itu — copy
  manifest ke root `Addins\<v>`, copy DLL ke `Addins\<v>\Apex\`, hapus
  `Addins\<v>\Apex.dll` jelek lama.
- `AuthenticodeSign` dan `DeployAddin` wajib diberi
  `Condition="'$(TargetFramework)' != ''"` — tanpa itu keduanya ikut
  jalan di build luar cross-targeting (tanpa TFM), di mana `$(TargetPath)`
  kosong → sign gagal (`Set-AuthenticodeSignature -FilePath ''`).
- **Jangan deploy binary net10 ke `Addins\2026`** — folder itu harus
  berisi binary net8 (terendah) agar termuat di 2026.0 maupun 2026.5.
- AddInId tetap **SATU** untuk semua versi → keputusan "Always Load"
  per versi Revit (registry CodeSigning per versi), sekali per build hash.

### 9.1 Jalur Revit 2027 (.NET 10) — saat dikehendaki

1. Install .NET 10 SDK: `winget install Microsoft.DotNet.SDK.10`.
2. Tambah TFM `net10.0-windows` di `Apex.csproj` + referensi API
   `$(RevitAPIPath2027)` (path instalasi Revit 2027, tambahkan di
   `Directory.Build.props`), dan property `DeployRevitVersions`
   untuk net10 → `2027`.
3. Audit pemakaian API yang berubah di API 2027; kalau ada, pakai
   `#if NET10_0_OR_GREATER` / tambahkan cabang di `RevitCompat`.
4. Build → `Addins\2027` mendapat binary net10; folder 2024/2025/2026
   tidak berubah. Folder `Addins\2026` **tetap** harus menerima binary
   net8 (jangan pernah net10) sampai 2026.0 tidak lagi dipakai.

---

## 10. Migrasi pyRevit Python → C#

| pyRevit / Python | C# Revit API |
|---|---|
| `__revit__.ActiveUIDocument` / `revit.doc` | `commandData.Application.ActiveUIDocument` / `.Document` |
| `revit.uidoc.Selection` | `uidoc.Selection` |
| `script.get_config()` / `save_config()` | JSON sendiri di `%AppData%` (`Core/ConfigStore.cs`) |
| `forms.WPFWindow` / XAML | WPF `Window` (`UI/*.xaml`) |
| `pyrevit.output` / `print` | `TaskDialog.Show(...)` atau log file |
| `Transaction` (pyRevit context) | `using (var t = new Transaction(doc, "...")) { ... }` |
| `revit.doc.Create.NewFamilyInstance(...)` | sama, tapi lewat `Document` API |
| `#! python` script per tombol | satu `IExternalCommand` per tombol |
| pyRevit bundle `.pushbutton` | `PushButtonData` di `App.OnStartup` |
| `compat.get_element_id_value()` | helper `GetElementIdValue` (`Id.Value` vs `IntegerValue`) |

Prinsip: pindahkan **logic** ke `Core/` dulu (tanpa UI/Revit UI), baru bikin
`Commands/` + `UI/` sebagai lapisan tipis. Lebih mudah diuji & diporting.

---

## 11. Checklist "add-in selesai"

- [ ] `dotnet build -c Release` → `0 Error(s)`.
- [ ] `.addin` + DLL ada di `%AppData%\Autodesk\Revit\Addins\2024\`,
      `...\2025\`, dan `...\2026\` (layout §4; deploy multi-version §9).
- [ ] Revit dibuka → tab/panel/tombol muncul.
- [ ] Journal sesi baru punya `Starting External Application: <Nama>`.
- [ ] `AddInsSettings.json` → entri add-in `LoadTime` > 0.
- [ ] `%AppData%\<NamaAddin>\startup.log` berisi `OnStartup succeeded`.
- [ ] Fungsi utama diuji di dokumen uji (transaction commit, tidak ada error).
- [ ] `README.md` project diperbarui.
- [ ] Khusus Apex: panel tool muncul di tab "Apex"; config JSON per tool
      terbaca; registry CodeSigning hanya berisi SATU entri Apex per
      versi Revit (AddInId sama di semua versi).

---

## 12. Apex — repo & toolbar utama tool C#

### 12.1 Keputusan arsitektur

Semua tool C# hidup di **satu add-in hub `Apex`**: satu repo
`Documents\RevitAddin\Apex\` → satu `Apex.csproj` → satu `Apex.dll` +
satu `Apex.addin`. Tab ribbon **"Apex"**, **satu panel per tool**
(Ultimate Join = panel pertama).

Alasan:
- Deploy satu file; trust satu AddInId (sekali "Always Load" per build).
- Helper bersama: `ConfigStore`, loader ikon, breadcrumb log.
- Menambah tool tidak menyentuh manifest/registry — cukup folder tool
  baru + pendaftaran panel (§12.2).

Status: repo Apex ada di `Documents\RevitAddin\Apex\` (git). Migrasi
Ultimate Join → `Tools/UltimateJoin/` **selesai** (§12.3 langkah 1–5).
Deploy legacy `UltimateJoinElements*` sudah ditarik dari folder Addins
(§12.3 langkah 6) — dipindah ke `..\UltimateJoinElements\deploy-retired\`,
bukan dihapus. Project legacy tetap buildable (§2.2) tapi **jangan
di-deploy lagi**.

### 12.2 Checklist menambah tool baru

1. Buat folder `Tools/<NamaTool>/` berisi `Commands/`, `Core/`, `UI/`.
   Namespace `Apex.Tools.<NamaTool>.*`; pindahkan logic murni ke `Core/`
   dulu (pola §10).
2. Satu `IExternalCommand` per tombol (pola §8.2).
3. Daftarkan di `App.OnStartup`: `CreateRibbonPanel("Apex", "<NamaTool>")`
   + `PushButtonData` per tombol (id unik `Apex_<Tool>_<Aksi>`,
   `ToolTip`, ikon 16/32 px embed `<Resource>` di `Icons/`).
4. Config JSON per tool: `%AppData%\Apex\<NamaTool>\config.json` (§8.4).
5. Breadcrumb log tetap satu `%AppData%\Apex\startup.log` — prefix baris
   dengan nama tool, mis. `[UltimateJoin] ...`.
6. `.addin`, GUID, dan deploy tidak berubah — rebuild + restart Revit.

### 12.3 Migrasi UltimateJoinElements → Apex (selesai)

1. Scaffold repo `Apex\` (layout §2.1). `Apex.addin` memakai **AddInId
   GUID baru** (GUID lama milik UltimateJoinElements — jangan dipakai ulang).
2. Copy `Commands/`, `Core/`, `UI/` ke `Tools/UltimateJoin/`; rename
   namespace ke `Apex.Tools.UltimateJoin.*`; pindahkan ikon ke `Icons/`
   dan perbaiki pack URI jadi `/Apex;component/Icons/...`.
3. Pindahkan wiring ribbon dari `App.cs` lama ke `Apex.App` (panel
   "Ultimate Join" + tombol Join & Configure).
4. Config: pindahkan `%AppData%\UltimateJoinElements\config.json` ke
   `%AppData%\Apex\UltimateJoin\config.json` (copy manual; skema sama).
5. Bawa target `AuthenticodeSign` + `DeployAddin` dari csproj lama
   (§3), sesuaikan nama ke `Apex`.
6. ✅ SELESAI (2026-09-12): Apex terbukti jalan (dia yang bikin panel
   "Ultimate Join" lebih dulu); legacy lalu gagal load dengan
   `CreateRibbonPanel` duplicate → `ArgumentException`. Deploy legacy
   ditarik dari folder Addins ke `..\UltimateJoinElements\deploy-retired\`.
   **Jangan deploy ulang legacy.**
