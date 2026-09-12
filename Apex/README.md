# Apex — hub add-in Revit C# (PrasKaa)

Satu add-in, satu DLL (`Apex.dll`), satu manifest (`Apex.addin`) — tab
ribbon **"Apex"** dengan **satu panel per tool**. Panduan lengkap ada di
[`AGENTS.md`](AGENTS.md).

| Tool | Panel | Tombol |
|---|---|---|
| Ultimate Join | Ultimate Join | Join, Configure |

## Build & install (VS Code + dotnet CLI, tanpa Visual Studio)

1. **Cek path Revit API.** `Directory.Build.props` defaultnya
   `C:\Program Files\Autodesk\Revit 2026`; sesuaikan bila perlu
   (override per-build: `dotnet build -p:RevitAPIPath="D:\Path\Lain"`).
2. **Build** (Revit harus tertutup):
   ```
   dotnet build Apex.sln -c Release
   ```
   Target `AuthenticodeSign` men-sign DLL (cert lokal `CN=PrasKaa`,
   auto-create), lalu `DeployAddin` men-copy `Apex.addin` +
   `Apex\Apex.dll` ke `%AppData%\Autodesk\Revit\Addins\2026\`.
3. **Buka Revit 2026** → tab **"Apex"** → panel **"Ultimate Join"**.
   Bila dialog keamanan muncul (build baru = hash baru), pilih
   **Always Load**; agar tidak ditanya lagi, trust cert `CN=PrasKaa`
   ke `Cert:\CurrentUser\Root`.

## Struktur

```
Apex/
├── Apex.sln / Apex.csproj    # net8.0-windows, UseWPF, x64
├── Directory.Build.props     # RevitAPIPath
├── Apex.addin                # satu manifest untuk semua tool
├── App.cs                    # tab "Apex" + panel per tool + log startup
├── Icons/                    # PNG 16/32 px, embed <Resource>
├── Tools/
│   └── UltimateJoin/         # tool pertama (port dari UltimateJoinElements)
│       ├── Commands/         # JoinCommand, ConfigureCommand
│       ├── Core/             # CategoryMap, JoinOrderManager, ConfigStore
│       └── UI/               # JoinOrderDialog.xaml(.cs)
├── AGENTS.md                 # panduan + playbook (sumber kebenaran)
└── .kilo/skills/             # skill revit-csharp-addin
```

## Menambah tool baru

Ikuti checklist `AGENTS.md` §12.2: buat `Tools/<NamaTool>/`
(Commands/Core/UI, namespace `Apex.Tools.<NamaTool>.*`), lalu daftarkan
panel + `PushButtonData` di `App.OnStartup`. Manifest, GUID, dan deploy
tidak pernah berubah.

## Config & log

- Config per tool: `%AppData%\Apex\<NamaTool>\config.json`
  (Ultimate Join: `%AppData%\Apex\UltimateJoin\config.json`).
- Log startup: `%AppData%\Apex\startup.log` (prefix `[<Tool>]`).

## Legacy

Project `..\UltimateJoinElements\` adalah add-in standalone lama yang
sudah dimigrasi ke sini; deploy legacy-nya di folder Addins dihapus
setelah Apex terbukti jalan (AGENTS.md §12.3 langkah 6).
