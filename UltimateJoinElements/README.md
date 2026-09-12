# Ultimate Join Elements — native C# add-in (v1, Revit 2026)

> **[2026-09-12] Tool ini sudah dimigrasi ke repo `..\Apex\`**
> (`Tools/UltimateJoin/`). Project ini legacy — dibiarkan buildable
> sampai Apex terbukti jalan di Revit, lalu deploy legacy-nya dihapus
> (lihat AGENTS.md §12.3). Jangan tambahkan fitur baru di sini.

Port dari script pyRevit "Ultimate Join Elements" + `join_order_manager.py`
ke Revit add-in native (compiled, .NET 8, tanpa pyRevit). Target: Revit
2026 saja untuk versi pertama ini.

## Build & install (VS Code + dotnet CLI, tanpa Visual Studio)

1. **Cek path Revit API.** Buka `Directory.Build.props` — defaultnya
   `C:\Program Files\Autodesk\Revit 2026`. Kalau instalasi Revit-mu beda
   lokasi, edit baris `RevitAPIPath` di situ.

2. **Build:**
   ```
   cd UltimateJoinElements
   dotnet build -c Release
   ```
   Build ini otomatis meng-copy `UltimateJoinElements.addin` ke
   `%AppData%\Autodesk\Revit\Addins\2026\` dan DLL-nya ke
   `%AppData%\Autodesk\Revit\Addins\2026\UltimateJoinElements\` (lihat
   target `DeployAddin` di `.csproj`) — jadi tidak perlu copy manual tiap
   kali build. **DLL wajib di subfolder**: Revit 2026 tidak memuat
   `<Assembly>` yang berupa nama file polos (lihat AGENTS.md §4).

3. **Buka Revit 2026.** Tab ribbon baru **"Apex"** muncul dengan panel
   **"Ultimate Join"** berisi 2 tombol: **Join** dan **Configure**.

4. Kalau add-in tidak muncul, ikuti playbook di `AGENTS.md` §6. Urutan
   penyebab yang sudah terbukti:
   - `<Assembly>` di `.addin` memakai nama file polos (harus ber-subfolder
     atau absolut) → Revit men-skip tanpa error;
   - dialog keamanan "publisher could not be verified" → DLL kini
     **di-sign** sertifikat code-signing lokal **CN=PrasKaa** (target
     `AuthenticodeSign` di `.csproj`, cert auto-create saat build pertama),
     jadi dialog menampilkan Publisher/Issuer: **PrasKaa**. Karena
     self-signed, Revit tetap bertanya sekali per build baru → pilih
     **Always Load** (kalau pernah salah pilih "Do Not Load", reset key
     registry `CodeSigning`); agar tidak ditanya sama sekali, trust
     cert-nya ke `Cert:\CurrentUser\Root`;
   - Revit masih terbuka saat build (DLL terkunci);
   - `RevitAPIPath` salah sehingga build gagal lebih dulu;
   - `AddInId` bentrok dengan add-in lain (regenerate dengan
     `[System.Guid]::NewGuid()`).
   Bukti utama ada di Revit journal (`Jrn.AddInManifest`,
   `Starting External Application`) dan `%AppData%\UltimateJoinElements\startup.log`.

## Struktur project

```
UltimateJoinElements/
├── UltimateJoinElements.csproj    # net8.0-windows, referensi RevitAPI(UI).dll
├── Directory.Build.props          # path instalasi Revit — edit di sini
├── UltimateJoinElements.addin     # manifest, dibaca Revit saat startup
├── App.cs                         # IExternalApplication — bikin ribbon tab+panel+2 tombol
├── Commands/
│   ├── JoinCommand.cs             # tombol "Join" — port logic normal-click
│   └── ConfigureCommand.cs        # tombol "Configure" — buka dialog WPF
├── Core/
│   ├── CategoryMap.cs             # port CATEGORY_DEFS/DEFAULT_ORDER/CAT_MAP/JOIN_PRIORITY
│   ├── JoinOrderManager.cs        # port penuh join_order_manager.py
│   └── ConfigStore.cs             # pengganti pyrevit script.get_config() → JSON di %AppData%
└── UI/
    ├── JoinOrderDialog.xaml       # shell dialog (borderless, custom titlebar)
    └── JoinOrderDialog.xaml.cs    # theme palette, drag-drop re-order, help popup, dark/light toggle
```

## Beda desain dari versi pyRevit — perlu kamu tahu

1. **Shift+Click → 2 tombol terpisah.** Sesuai keputusanmu, "Join" dan
   "Configure" sekarang 2 tombol ribbon berbeda, bukan satu tombol yang
   dibedakan lewat status keyboard saat klik.

2. **Reorder pakai drag-and-drop grip `⋮⋮` (paritas penuh).** Sudah di-port
   sama seperti script pyRevit: `DragDrop.DoDragDrop` dari grip di kanan
   row + `AllowDrop`/`Drop` di tiap row, dengan pop+insert (bukan swap)
   jadi row bisa pindah ke posisi mana pun.

3. **Config disimpan di JSON, bukan pyRevit config store.** Native
   add-in tidak akses `script.get_config()` pyRevit, jadi `ConfigStore.cs`
   simpan `priority_order`, `excluded_categories`, `dark_mode` ke
   `%AppData%\UltimateJoinElements\config.json`. Kalau kamu mau setting
   ini dibagi/sync antar mesin (kamu kerja di 2 komputer — laptop &
   PC rumah), bilang saja, bisa diarahkan ke path OneDrive/network share.

4. **Single-version (Revit 2026) dulu.** Kalau nanti mau multi-target
   2024/2025/2026 kayak toolkit pyRevit-mu, `.csproj` perlu diubah ke
   multi-`TargetFrameworks` (net48 untuk 2024, net8.0-windows untuk
   2025/2026) dengan conditional reference per-versi — pola yang sama
   kayak `praskaa/PyPrasKaa` tapi di level compile, bukan runtime check.

## Yang belum di-cover / cek manual

- **`element.Id.Value`** dipakai apa adanya (bukan `IntegerValue`) karena
  target Revit 2026 — kalau nanti multi-version ke 2024, ini perlu
  fallback yang sama seperti pattern `compat.get_element_id_value()` di
  toolkit pyRevit-mu.
- Icon ribbon: PNG sumber di root project (`joinelements.png` 65x41,
  `Configure.png` 96x96) di-resize aspect-fit jadi 16/32px ke `Icons\`
  (`join.small/large.png`, `configure.small/large.png`), di-embed sebagai
  `<Resource>`, dan dipasang lewat `Image`/`LargeImage` di App.cs (pack
  URI `component/Icons/...`).
- Sudah di-build dan berjalan di Revit 2026 (2026-09-12): tab "Apex"
  beserta tombol Join dan Configure aktif.
- Log startup (diagnostik) ditulis ke `%AppData%\UltimateJoinElements\startup.log`
  oleh `App.OnStartup`; aman untuk dibiarkan.
