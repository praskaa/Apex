---
name: revit-csharp-addin
description: End-to-end workflow for PrasKaa native C# Revit add-ins (Revit 2026, .NET 8). Use whenever the user builds, deploys, scaffolds, debugs, or ports a Revit add-in: "buat add-in revit c#", "new revit addin", "build addin", "deploy addin revit", "addin tidak muncul", "tab tidak muncul", "revit addin not loading", "port pyrevit ke c#", "migrate pyrevit tool to c#", "revit journal error", "ganti RevitAPI path", or any task touching .addin manifests, ribbon tabs, IExternalApplication/IExternalCommand, DeployAddin targets, or Revit add-in scheduling/trust. Also trigger for multi-version support (Revit 2024/2025/2026) and for reading Revit journals or the CodeSigning registry key.
---

# Revit C# Add-in Skill (PrasKaa, Revit 2026 / .NET 8)

Panduan operasional untuk membangun, deploy, dan men-debug add-in C# Revit.
Ringkasan lengkap ada di `AGENTS.md` di root repo ini — baca itu dulu.

## Aturan paling penting (dari add-in pertama yang berhasil)

1. **`.addin` `<Assembly>` WAJIB path absolut atau relatif ber-subfolder**
   (`Nama\Nama.dll`). Nama file polos (`Nama.dll`) membuat Revit 2026
   men-skip add-in **tanpa error**: manifest terbaca (`NoError`), load time
   `0.000000`, tidak ada `Starting External Application`, tidak ada tab.
2. **Layout deploy**: manifest di `%AppData%\Autodesk\Revit\Addins\2026\`,
   DLL di `%AppData%\Autodesk\Revit\Addins\2026\<Nama>\`.
3. **Tutup Revit sebelum `dotnet build`** (DLL terkunci → MSB3021/MSB3027).
4. `UseWPF=true` sudah meng-glob `*.xaml` → **jangan** tambah `<Page Include>`.
5. WPF implicit usings **tidak** punya `System.IO` → `using System.IO;`.
6. Add-in unsigned memicu dialog keamanan → pilih **"Always Load"**.
   Keputusan disimpan di registry `...\CodeSigning\<AddInId>`; "Do Not Load"
   akan men-skip permanen tanpa dialog lagi. DLL sekarang di-sign lokal
   `CN=PrasKaa` (target `AuthenticodeSign` di csproj), tapi karena
   self-signed Revit tetap bertanya sekali per build baru — atau trust
   cert-nya ke `Cert:\CurrentUser\Root` agar tidak ditanya (AGENTS.md §5).
7. Butuh **.NET 8 SDK** (`dotnet --list-sdks`). SDK 7 tidak bisa build net8.
8. Warning `MSB3277` (Microsoft.VisualBasic/System.Drawing) = noise, abaikan.

## Prosedur: build + deploy

```powershell
tasklist /FI "IMAGENAME eq Revit.exe" /NH   # harus "No tasks", kalau ada tutup dulu
dotnet build -c Release
cmd /c dir "%AppData%\Autodesk\Revit\Addins\2026\NamaAddin.*"
cmd /c dir "%AppData%\Autodesk\Revit\Addins\2026\NamaAddin"
```

Sukses = `0 Error(s)`, `.addin` + DLL ada di layout di atas. Setelah itu buka
Revit 2026 (pilih "Always Load" bila dialog muncul), cek tab ribbon.

## Prosedur: diagnosa add-in tidak muncul

Kerjakan berurutan, kumpulkan bukti dulu, jangan menebak:

1. **Journal terbaru** (nomor terbesar):
   `%LocalAppData%\Autodesk\Revit\Autodesk Revit 2026\Journals\journal.NNNN.txt`
   - `Jrn.AddInManifest: NamaAddin.addin` → load time `0.000000` = assembly
     tidak dimuat.
   - `Starting External Application: Nama Addin` → **bukti `OnStartup` dipanggil**.
     Tidak ada baris ini = Revit skip sebelum OnStartup (masalah manifest/trust/path).
   - `TaskDialog|Unsigned_File_Loading`, `API_ERROR`, `Exception`.
2. **Trust/disable**:
   `reg query "HKCU\Software\Autodesk\Revit\Autodesk Revit 2026\CodeSigning" /s`
   dan baca `%AppData%\Autodesk\Revit\Autodesk Revit 2026\AddinsData\AddInsSettings.json`.
   `Disabled:false` ≠ tidak diblokir CodeSigning.
   Reset blok: (Revit tertutup)
   `reg delete "HKCU\Software\Autodesk\Revit\Autodesk Revit 2026\CodeSigning" /v "<AddInId>" /f`
3. **Breadcrumb**: `%AppData%\Apex\startup.log` (legacy:
   `%AppData%\<NamaAddin>\startup.log`).
   Ada = OnStartup jalan; tidak ada = Revit belum memanggilnya.
4. **Path `<Assembly>`** di `.addin` — cek aturan #1.

## Prosedur: menambah tool ke Apex (standar)

Semua tool C# baru masuk ke **repo ini** (root repo Apex, satu add-in hub)
— satu DLL, satu manifest, satu AddInId untuk semua tool (detail
AGENTS.md §12):

```
Apex/
├── Apex.csproj        net8.0-windows, UseWPF, ImplicitUsings, Nullable, x64,
│                      AppendTargetFrameworkToOutputPath=false,
│                      CopyLocalLockFileAssemblies=false, RevitAPI/RevitAPIUI
│                      Private=false, target AuthenticodeSign + DeployAddin
│                      (lihat AGENTS.md §3)
├── Directory.Build.props RevitAPIPath = C:\Program Files\Autodesk\Revit 2026
├── Apex.addin         <Assembly>Apex\Apex.dll</Assembly>, AddInId GUID baru
│                      (SEKALI untuk SEMUA tool),
│                      <FullClassName>Apex.App</FullClassName>
├── App.cs             IExternalApplication: tab "Apex" (try/catch
│                      ArgumentException) + panel per tool + tombol,
│                      plus breadcrumb log
└── Tools/<NamaTool>/  Commands/ (satu IExternalCommand per tombol),
                       Core/ (logic tanpa UI), UI/ (WPF dialog)
```

Menambah tool = buat `Tools/<NamaTool>/` + daftarkan panel & tombol di
`App.OnStartup` (checklist AGENTS.md §12.2). `.addin`, GUID, dan deploy
tidak berubah.

## Prosedur legacy: scaffold add-in standalone

Hanya bila add-in memang harus berdiri sendiri di luar Apex. Pola lama
UltimateJoinElements: `App.cs` + `Commands/ Core/ UI/` di root project,
manifest sendiri `<Nama>\<Nama>.dll`, AddInId sendiri. Deploy & trust
per-add-in (duplikasi manifest, log, dan keputusan "Always Load").
Detail: AGENTS.md §2.2.

`DeployAddin` target (berlaku untuk kedua pola — ganti `<Nama>`, mis. `Apex`):

```xml
<Target Name="DeployAddin" AfterTargets="Build">
  <ItemGroup>
    <AddinFiles Include="$(ProjectDir)<Nama>.addin" />
    <BuiltDlls Include="$(TargetDir)<Nama>.dll" />
  </ItemGroup>
  <MakeDir Directories="$(AppData)\Autodesk\Revit\Addins\2026" />
  <MakeDir Directories="$(AppData)\Autodesk\Revit\Addins\2026\<Nama>" />
  <Copy SourceFiles="@(AddinFiles)" DestinationFolder="$(AppData)\Autodesk\Revit\Addins\2026" />
  <Copy SourceFiles="@(BuiltDlls)" DestinationFolder="$(AppData)\Autodesk\Revit\Addins\2026\<Nama>" />
  <Delete Files="$(AppData)\Autodesk\Revit\Addins\2026\<Nama>.dll" ContinueOnError="true" />
</Target>
```

## Prosedur: migrasi pyRevit Python → C#

| pyRevit / Python | C# Revit API |
|---|---|
| `revit.doc` / `__revit__.ActiveUIDocument` | `commandData.Application.ActiveUIDocument` / `.Document` |
| `script.get_config()` | JSON per tool: `%AppData%\Apex\<NamaTool>\config.json` (legacy: `%AppData%\<Nama>\config.json`) |
| `forms.WPFWindow` | WPF `Window` (`UI/*.xaml`) |
| `pyrevit.output` / `print` | `TaskDialog.Show` atau file log |
| Transaction dari context | `using (var t = new Transaction(doc,"...")) { ... }` |
| `.pushbutton` bundle | `PushButtonData` di `App.OnStartup` |
| `compat.get_element_id_value()` | helper `Id.Value` (2026) vs `IntegerValue` (2024/2025) |

Prinsip: pindahkan logic ke `Core/` dulu (bebas UI/Revit UI), baru bikin
`Commands/` + `UI/` tipis. Satu tombol = satu `IExternalCommand`.

## Pola kode wajib

Ribbon (`IExternalApplication` — tab "Apex" dimiliki `Apex.App`):
```csharp
try { application.CreateRibbonTab("Apex"); }
catch (Autodesk.Revit.Exceptions.ArgumentException) { /* tab sudah ada */ }
var panel = application.CreateRibbonPanel("Apex", "<NamaTool>");
panel.AddItem(new PushButtonData("Apex_<Tool>_<Aksi>", "Text", asmPath,
    "Apex.Tools.<Tool>.Commands.<Aksi>Command"));
```

Command (`IExternalCommand`):
```csharp
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class Cmd : IExternalCommand
{
    public Result Execute(ExternalCommandData d, ref string m, ElementSet e)
    {
        var doc = d.Application.ActiveUIDocument.Document;
        using (var t = new Transaction(doc, "Nama Transaksi"))
        {
            t.Start();
            try { /* ubah model */ t.Commit(); }
            catch { if (t.HasStarted() && !t.HasEnded()) t.RollBack(); throw; }
        }
        return Result.Succeeded;
    }
}
```

Breadcrumb log (proves whether Revit invoked OnStartup) — lihat AGENTS.md §6.3.

## Multi-version (2024/2025/2026)

- Revit 2024 = `net48`, Revit 2025/2026 = `net8.0-windows`.
- `<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>` + referensi
  RevitAPI kondisional per-TFM + `RevitAPIPath` per versi.
- Satu `.addin` per folder versi (`Addins\2024\`, `Addins\2025\`, `Addins\2026\`).
- Jangan pakai API net8-only di jalur net48; gate dengan `#if NET8_0_OR_GREATER`.

## Checklist selesai

- [ ] `dotnet build -c Release` → 0 error.
- [ ] `.addin` + DLL di layout baku.
- [ ] Tab ribbon muncul di Revit 2026.
- [ ] Journal ada `Starting External Application: <Nama>`.
- [ ] `startup.log` berisi `OnStartup succeeded`.
- [ ] Fungsi diuji di dokumen uji.
- [ ] Khusus Apex: panel tool muncul di tab "Apex"; hanya SATU entri
      CodeSigning registry untuk Apex.

## Catatan lingkungan

- Skill ini ikut ter-versioning di repo Apex (`.kilo/skills/`) — sync
  lewat git; ter-load saat repo Apex dibuka sebagai workspace.
- Di workspace ini, frontmatter `.kilo/command/*.md` dan `.kilo/agent/*.md`
  saat ini gagal di-parse ("No context found for instance"), sedangkan
  `.kilo/skills/**/SKILL.md` berhasil. Karena itu workflow disimpan sebagai
  skill, bukan command/agent.
