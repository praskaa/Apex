# AGENTS.md — PrasKaa Revit Add-in Workspace (pointer)

Panduan pengembangan add-in C# Revit kini hidup di **repo Apex**:

- `Apex\AGENTS.md` — panduan lengkap + playbook (single source of truth)
- `Apex\.kilo\skills\revit-csharp-addin\SKILL.md` — skill operasional

Keduanya ter-versioning di git di dalam repo Apex, jadi bisa di-update
dari mesin mana pun (`git pull` di folder `Apex\`).

Folder `UltimateJoinElements\` di sini adalah project **legacy** yang
sudah dimigrasi ke `Apex\Tools\UltimateJoin\` — biarkan sampai Apex
terbukti jalan di Revit, lalu deploy legacy di folder Addins dihapus
(detail: `Apex\AGENTS.md` §12.3).
