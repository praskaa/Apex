# Apex — hub add-in Revit C# (PrasKaa)

Satu codebase, satu manifest (`Apex.addin`) — tab ribbon **"Apex"**
dengan **satu panel per tool**. Panduan lengkap ada di
[`AGENTS.md`](AGENTS.md).

| Tool | Panel | Tombol |
|---|---|---|
| Ultimate Join | Ultimate Join | Join, Configure |

## Kompatibilitas Revit

Satu `dotnet build -c Release` → DLL ter-deploy ke **Revit 2024, 2025,
dan 2026/2026.5** sekaligus (AGENTS.md §9):

| Revit | Runtime | Binary yang dimuat | Folder deploy |
|---|---|---|---|
| 2024 | .NET Framework 4.8 | `net48` (API Revit 2024) | `Addins\2024` |
| 2025 | .NET 8 → .NET 10 (update in-place Sep 2026) | `net8.0-windows` (API Revit 2026) | `Addins\2025` |
| 2026 / 2026.5 | .NET 8 → .NET 10 mulai 2026.5 | `net8.0-windows` (net8 jalan di kedua runtime) | `Addins\2026` (satu folder untuk 2026.0 & 2026.5) |
| 2027 | .NET 10 native | belum didukung — jalur: TFM `net10.0-windows` + .NET 10 SDK (AGENTS.md §9.1) | `Addins\2027` |

## Build & install (VS Code + dotnet CLI, tanpa Visual Studio)

1. **Cek path Revit API.** `Directory.Build.props` defaultnya
   `C:\Program Files\Autodesk\Revit 2024/2025/2026` (per versi);
   sesuaikan bila perlu (override per-build:
   `dotnet build -p:RevitAPIPath2024="D:\Path\Lain"` dst.).
2. **Build** (semua Revit harus tertutup):
   ```
   dotnet build Apex.sln -c Release
   ```
   Output per TFM di `bin\Release\net48\` dan `bin\Release\net8.0-windows\`.
   Target `AuthenticodeSign` men-sign tiap DLL (cert lokal `CN=PrasKaa`,
   auto-create), lalu `DeployAddin` men-copy `Apex.addin` + `Apex\Apex.dll`
   ke `%AppData%\Autodesk\Revit\Addins\2024\`, `...\2025\`, dan `...\2026\`
   sekaligus (DLL net48 ke 2024, DLL net8 ke 2025 + 2026).
3. **Buka Revit** (2024, 2025, atau 2026) → tab **"Apex"** → panel
   **"Ultimate Join"**. Bila dialog keamanan muncul (build baru = hash
   baru), pilih **Always Load** — sekali per versi Revit; agar tidak
   ditanya lagi, trust cert `CN=PrasKaa` ke `Cert:\CurrentUser\Root`.

## Struktur

```
Apex/
├── Apex.sln / Apex.csproj    # net48;net8.0-windows, UseWPF, x64
├── Directory.Build.props     # RevitAPIPath2024/2025/2026
├── Apex.addin                # satu manifest untuk semua tool & versi
├── App.cs                    # tab "Apex" + panel per tool + log startup
├── IsExternalInit.cs         # shim net48 untuk record/init (§9)
├── Icons/                    # PNG 16/32 px, embed <Resource>
├── Tools/
│   └── UltimateJoin/         # tool pertama (port dari UltimateJoinElements)
│       ├── Commands/         # JoinCommand, ConfigureCommand
│       ├── Core/             # CategoryMap, JoinOrderManager, ConfigStore, RevitCompat
│       └── UI/               # JoinOrderDialog.xaml(.cs)
├── AGENTS.md                 # panduan + playbook (sumber kebenaran)
└── .kilo/skills/             # skill revit-csharp-addin
```

## Menambah tool baru

Ikuti checklist `AGENTS.md` §12.2: buat `Tools/<NamaTool>/`
(Commands/Core/UI, namespace `Apex.Tools.<NamaTool>.*`), lalu daftarkan
panel + `PushButtonData` di `App.OnStartup`. Manifest, GUID, dan deploy
tidak pernah berubah. Kode harus lolos kedua TFM: ID element lewat
`RevitCompat.GetElementIdValue(...)`, tanpa API net8-only mentah
(AGENTS.md §9).

## Config & log

- Config per tool: `%AppData%\Apex\<NamaTool>\config.json`
  (Ultimate Join: `%AppData%\Apex\UltimateJoin\config.json`).
  JSON ditulis/dibaca serializer mini internal (sama formatnya di semua
  versi Revit — `System.Text.Json` tidak in-box di net48).
- Log startup: `%AppData%\Apex\startup.log` (prefix `[<Tool>]`).

## Legacy

Project `..\UltimateJoinElements\` adalah add-in standalone lama yang
sudah dimigrasi ke sini; deploy legacy-nya di folder Addins dihapus
setelah Apex terbukti jalan (AGENTS.md §12.3 langkah 6).
