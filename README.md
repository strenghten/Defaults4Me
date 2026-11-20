# Defaults4Me

**Make an app the default for all its file types at once – or just for whole groups like images, videos or music.**
Windows 11 makes you click every extension separately in *Settings → Apps → Default apps*. Defaults4Me does it in one step.

*Deutsch weiter unten.*

## What it does
- Lists every installed app that registers file types (XnView MP, VLC, LibreOffice, NanaZip, …).
- Pick an app, tick groups (Images, Videos, Music, Documents, Archives, Text/Code, Other) and see a list of every
  extension with the app that opens it now. Untick what you don't want, click **Apply**.
- **Undo** restores the previous state. **Profiles** save all your associations to a text file and load them on another PC.
- **Overview** of all extensions and their current app; optional **right-click entry** “Open all of this kind with …”
  and **Start menu** entry.
- After writing, it asks Windows which app is now really used and shows anything that did not work, with a button
  to the app's page in Windows Settings.
- Runs only when you start it. Single portable `.exe`, no installer, no background service, no network access.
- German or English, following the Windows display language.

## How it works / limits
Windows protects the per-user choice with an undocumented hash (`UserChoiceLatest`, Windows 11 24H2+).
Defaults4Me computes it the same way Windows does (code adapted from PS-SFTA, MIT – see THIRD-PARTY-NOTICES.md).
Before writing anything it recomputes the hashes Windows itself wrote on your PC; if they no longer match
(e.g. after an update changed the scheme), it writes **nothing** and tells you.

`.pdf`, `http` and `https` are additionally locked by the *UserChoice Protection Driver* (UCPD). Those are reported
as “blocked”. Experts can switch the driver off under **More** (“I know what I'm doing”; admin rights and a restart).
This lowers a protection against software hijacking your browser – your decision.

Changes apply to the signed-in user only.

## Command line
```
Defaults4Me list
Defaults4Me show "XnView MP" images
Defaults4Me set VLC videos,music
Defaults4Me undo
Defaults4Me check
Defaults4Me export my-defaults.txt
Defaults4Me import my-defaults.txt [--all]
```
In PowerShell append `| Out-Host` so the prompt waits for the output.

## Build
.NET SDK 8+ (works on Linux too): `dotnet build src -c Release -o build` → `build/Defaults4Me.exe` (.NET Framework 4.8,
part of every Windows 10/11).

---

## Deutsch
Setzt alle Formate einer App auf einmal als Standard – oder nur ganze Gruppen (Bilder, Videos, Musik, Dokumente,
Archive, Text/Code). Liste vorher prüfen und abwählen, „Umstellen“, notfalls „Rückgängig“. Profile sichern/laden,
Übersicht aller Endungen, Rechtsklick-Eintrag „Alle dieser Art öffnen mit …“. Gesperrte Typen (PDF, Browser) zeigt es
an und öffnet auf Knopfdruck die Windows-Einstellungen; Profis können den Schutz unter „Mehr“ abschalten.
Läuft nur, wenn man es startet; kein Installer, kein Netzzugriff.
