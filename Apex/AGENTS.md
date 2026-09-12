# AGENTS.md — Apex (PrasKaa Revit Add-in Development, C#)

AGENTS.md resmi **repo Apex** — single source of truth pengembangan
tool C# Revit PrasKaa. File ini ikut ter-versioning di git, jadi bisa
di-update dari mesin mana pun.

Status saat ini: **Revit 2026 / .NET 8**. Jalur migrasi dari toolkit
pyRevit `PyPrasKaa` ke add-in native compiled. Semua tool C# hidup di
**satu add-in hub `Apex`** (satu repo, satu DLL, satu manifest) — tab
ribbon **"Apex"** adalah toolbar utamanya, satu panel per tool.
`UltimateJoinElements` adalah add-in pertama yang terbukti jalan;
semua pelajaran di bawah berasal dari sana, dan ia sudah dimigrasi
masuk sebagai tool pertama Apex (lihat §12).

---

## 0. Aturan wajib (TL;DR)

1. Target framework: `net8.0-windows`, `UseWPF=true`, `PlatformTarget=x64`.
2. Referensi `RevitAPI.dll` + `RevitAPIUI.dll` dari instalasi Revit,
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
- **Revit 2026** di `C:\Program Files\Autodesk\Revit 2026` (diatur di
  `Directory.Build.props`; override per-build dengan
  `dotnet build -p:RevitAPIPath="D:\Path\Lain"`).
- Tidak butuh Visual Studio. VS Code + .NET CLI cukup.

---

## 2. Struktur project

### 2.1 Repo Apex (standar ke depan)

Satu repo → satu csproj → satu DLL + satu manifest. Tab **"Apex"**,
satu panel per tool:

```
Apex/
├── Apex.sln
├── Apex.csproj              # net8.0-windows, UseWPF, referensi RevitAPI(UI)
├── Directory.Build.props    # RevitAPIPath (path instalasi Revit)
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
# Wajib: Revit 2026 tertutup
tasklist /FI "IMAGENAME eq Revit.exe" /NH

dotnet build -c Release
```

Target `DeployAddin` di `.csproj` (`AfterTargets="Build"`) otomatis:
1. `MakeDir` folder `%AppData%\Autodesk\Revit\Addins\2026\`;
2. `MakeDir` folder `%AppData%\Autodesk\Revit\Addins\2026\<NamaAddin>\`;
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
`Addins\2026\Apex\` (aturan subfolder §4 tetap berlaku).

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

## 9. Multi-version (Revit 2024 / 2025 / 2026)

- Revit **2024** = .NET Framework 4.8 (`net48`); **2025 & 2026** = .NET 8
  (`net8.0-windows`).
- Multi-target di `.csproj`:

  ```xml
  <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
  ```
  dengan referensi RevitAPI kondisional per-TFM dan `RevitAPIPath` per-versi
  (mis. `RevitAPIPath2024`, `RevitAPIPath2026`).
- Manifest per versi: satu `.addin` per Revit version ke folder
  `Addins\2024\`, `Addins\2025\`, `Addins\2026\`.
- Jangan pakai API net8-only di jalur net48. Cek `#if NET8_0_OR_GREATER`.
- Deploy target harus meng-copy ke folder Addins sesuai versi target.

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
- [ ] `.addin` + DLL ada di `%AppData%\Autodesk\Revit\Addins\2026\` (layout §4).
- [ ] Revit dibuka → tab/panel/tombol muncul.
- [ ] Journal sesi baru punya `Starting External Application: <Nama>`.
- [ ] `AddInsSettings.json` → entri add-in `LoadTime` > 0.
- [ ] `%AppData%\<NamaAddin>\startup.log` berisi `OnStartup succeeded`.
- [ ] Fungsi utama diuji di dokumen uji (transaction commit, tidak ada error).
- [ ] `README.md` project diperbarui.
- [ ] Khusus Apex: panel tool muncul di tab "Apex"; config JSON per tool
      terbaca; registry CodeSigning hanya berisi SATU entri untuk Apex.

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
