# AGENTS.md — PrasKaa Revit Add-in Workspace (pointer)

Panduan pengembangan add-in C# Revit kini hidup di **repo Apex**:

- `Apex\AGENTS.md` — panduan lengkap + playbook (single source of truth)
- `Apex\.kilo\skills\revit-csharp-addin\SKILL.md` — skill operasional

Keduanya ter-versioning di git di dalam repo Apex, jadi bisa di-update
dari mesin mana pun (`git pull` di folder `Apex\`).

Project legacy `UltimateJoinElements\` sudah dihapus dari repo karena
dimigrasi penuh ke `Apex\Tools\UltimateJoin\`. Jika masih ada deploy
lama `UltimateJoinElements.addin` di folder Addins Revit, hapus manual
(detail: `Apex\AGENTS.md` §12.3).
