using System.Collections.Generic;
using System.Globalization;

namespace Defaults4Me
{
    /// <summary>German or English, following the Windows display language.</summary>
    static class Lang
    {
        static readonly bool De = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

        public static string T(string key)
        {
            string[] v;
            return S.TryGetValue(key, out v) ? (De ? v[0] : v[1]) : key;
        }

        static readonly Dictionary<string, string[]> S = new Dictionary<string, string[]>
        {
            { "group.images", new[] { "Bilder", "Images" } },
            { "group.videos", new[] { "Videos", "Videos" } },
            { "group.music", new[] { "Musik", "Music" } },
            { "group.documents", new[] { "Dokumente", "Documents" } },
            { "group.archives", new[] { "Archive", "Archives" } },
            { "group.text", new[] { "Text/Code", "Text/Code" } },
            { "group.other", new[] { "Sonstige", "Other" } },

            { "result.ok", new[] { "umgestellt", "changed" } },
            { "result.blocked", new[] { "von Windows gesperrt", "blocked by Windows" } },
            { "result.rejected", new[] { "von Windows abgelehnt", "rejected by Windows" } },
            { "result.failed", new[] { "Fehler", "error" } },
            { "state.same", new[] { "schon so", "already set" } },

            { "problem.scheme", new[] { "Windows nutzt ein neueres Schutzverfahren, das Defaults4Me noch nicht kennt. Bitte nach einer neuen Version schauen – bis dahin über die Windows-Einstellungen umstellen.", "Windows uses a newer protection scheme that Defaults4Me does not know yet. Please look for a new version – until then use Windows Settings." } },
            { "problem.salt", new[] { "Die nötigen Windows-Daten für die Prüfsumme wurden nicht gefunden. Bitte über die Windows-Einstellungen umstellen.", "The Windows data needed for the hash was not found. Please use Windows Settings." } },
            { "problem.mismatch", new[] { "Windows hat sein Prüfverfahren geändert (vermutlich durch ein Update). Defaults4Me schreibt deshalb nichts. Bitte nach einer neuen Version schauen.", "Windows changed its hash scheme (probably an update). Defaults4Me writes nothing. Please look for a new version." } },

            { "cli.help", new[] {
@"Defaults4Me {v} – alle Formate einer App auf einmal als Standard setzen

Ohne Angaben startet das Fenster. Befehle (in PowerShell mit | Out-Host anhängen, damit es wartet):
  Defaults4Me list                       Apps mit ihrer Anzahl Formate
  Defaults4Me show <App> [Gruppen]       Vorschau: was würde umgestellt
  Defaults4Me set  <App> [Gruppen]       umstellen (alter Stand wird für undo gemerkt)
  Defaults4Me undo                       letzten Schritt rückgängig
  Defaults4Me check                      prüft, ob Umstellen auf diesem PC geht
  Defaults4Me export <Datei>             eigene Zuordnungen als Profil speichern
  Defaults4Me import <Datei>             Profil laden und umstellen (--alle: auch schon passende neu schreiben)
Gruppen (mit Komma): bilder, videos, musik, dokumente, archive, text, sonstige, alle
Beispiel: Defaults4Me set ""XnView MP"" bilder",
@"Defaults4Me {v} – make an app the default for all its file types at once

Without arguments the window opens. Commands (in PowerShell append | Out-Host so it waits):
  Defaults4Me list                       apps and how many types they handle
  Defaults4Me show <app> [groups]        preview of what would change
  Defaults4Me set  <app> [groups]        change (previous state kept for undo)
  Defaults4Me undo                       undo the last step
  Defaults4Me check                      checks whether changing works on this PC
  Defaults4Me export <file>              save your associations as a profile
  Defaults4Me import <file>              load a profile and apply it (--all: rewrite matching ones too)
Groups (comma separated): images, videos, music, documents, archives, text, other, all
Example: Defaults4Me set VLC videos,music" } },
            { "cli.summary", new[] { "{0} umgestellt, {1} nicht.", "{0} changed, {1} not." } },
            { "cli.blocked", new[] { "Gesperrte Typen (z. B. .pdf) in den Windows-Einstellungen umstellen oder im Fenster unter „Mehr“ den Schutz abschalten.", "Change blocked types (e.g. .pdf) in Windows Settings, or switch off the protection under “More” in the window." } },
            { "cli.scheme", new[] { "Schutzverfahren: Version {0}", "Protection scheme: version {0}" } },
            { "cli.selfcheck", new[] { "Selbsttest: {0} von {1} vorhandenen Windows-Zuordnungen nachgerechnet", "Self-test: {0} of {1} existing Windows associations reproduced" } },
            { "cli.ucpd", new[] { "Schutztreiber (PDF/Browser): {0}", "Protection driver (PDF/browser): {0}" } },
            { "ucpd.on", new[] { "an", "on" } },
            { "ucpd.off", new[] { "aus", "off" } },
            { "cli.ready", new[] { "Bereit: Umstellen funktioniert auf diesem PC.", "Ready: changing defaults works on this PC." } },
            { "cli.noapp", new[] { "App „{0}“ nicht gefunden. „list“ zeigt alle.", "App “{0}” not found. “list” shows all." } },
            { "cli.nogroup", new[] { "Unbekannte Gruppe „{0}“.", "Unknown group “{0}”." } },
            { "cli.types", new[] { "Formate", "types" } },
            { "cli.exported", new[] { "{0} Zuordnungen gespeichert in {1}", "{0} associations saved to {1}" } },

            { "ui.app", new[] { "App:", "App:" } },
            { "ui.groups", new[] { "Gruppen:", "Groups:" } },
            { "col.ext", new[] { "Endung", "Extension" } },
            { "col.group", new[] { "Gruppe", "Group" } },
            { "col.now", new[] { "Öffnet jetzt mit", "Opens now with" } },
            { "col.new", new[] { "Neu (ProgId)", "New (ProgId)" } },
            { "col.status", new[] { "Status", "Status" } },
            { "ui.apply", new[] { "Umstellen", "Apply" } },
            { "ui.undo", new[] { "Rückgängig", "Undo" } },
            { "ui.settings", new[] { "Windows-Einstellungen", "Windows Settings" } },
            { "ui.more", new[] { "Mehr", "More" } },
            { "ui.fortype", new[] { "alle {0} wie {1}", "all {0} like {1}" } },
            { "ui.newapps", new[] { "★ = neu seit dem letzten Start", "★ = new since last start" } },
            { "ui.count", new[] { "{0} Formate, {1} zum Umstellen markiert", "{0} types, {1} marked to change" } },
            { "ui.done", new[] { "Fertig: {0} umgestellt, {1} nicht.", "Done: {0} changed, {1} not." } },
            { "ui.failed", new[] { "Diese Typen konnten nicht umgestellt werden:\n{0}\n\nWindows-Einstellungen für diese App öffnen, um sie dort von Hand zu setzen?", "These types could not be changed:\n{0}\n\nOpen Windows Settings for this app to set them by hand?" } },
            { "ui.blockedhint", new[] { "Gesperrt sind bei Windows 11 nur PDF und Browser-Links. Profis können den Schutz unter „Mehr“ abschalten.", "Windows 11 only blocks PDF and browser links. Experts can switch off the protection under “More”." } },
            { "ui.importhint", new[] { "Profil geladen – prüfen und „Umstellen“ klicken.", "Profile loaded – check and click “Apply”." } },
            { "undo.ask", new[] { "Letzten Schritt rückgängig machen?\n\n{0}", "Undo the last step?\n\n{0}" } },
            { "undo.done", new[] { "Rückgängig gemacht.", "Undone." } },
            { "undo.none", new[] { "Nichts zum Rückgängigmachen.", "Nothing to undo." } },

            { "more.overview", new[] { "Übersicht aller Endungen", "Overview of all extensions" } },
            { "more.export", new[] { "Profil sichern …", "Save profile …" } },
            { "more.import", new[] { "Profil laden …", "Load profile …" } },
            { "more.menu", new[] { "Im Rechtsklick-Menü anzeigen", "Show in right-click menu" } },
            { "more.startmenu", new[] { "Im Startmenü anzeigen", "Show in Start menu" } },
            { "more.ucpdoff", new[] { "Profi: Schutz für PDF/Browser abschalten …", "Expert: switch off PDF/browser protection …" } },
            { "more.ucpdon", new[] { "Schutz für PDF/Browser wieder einschalten", "Switch PDF/browser protection back on" } },
            { "more.about", new[] { "Über Defaults4Me", "About Defaults4Me" } },
            { "about", new[] { "Defaults4Me {v}\nSetzt alle Formate einer App auf einmal als Standard.\n\nPrüfsummen-Teil nach PS-SFTA (MIT, Danysys, cssxn, ComoEstaisAmigos).\nLäuft nur, wenn du es startest.", "Defaults4Me {v}\nMakes an app the default for all its file types at once.\n\nHash code based on PS-SFTA (MIT, Danysys, cssxn, ComoEstaisAmigos).\nRuns only when you start it." } },
            { "menu.verb", new[] { "Alle dieser Art öffnen mit … (Defaults4Me)", "Open all of this kind with … (Defaults4Me)" } },
            { "app.desc", new[] { "Standard-Apps für ganze Formatgruppen setzen", "Set default apps for whole groups of file types" } },
            { "file.filter", new[] { "Defaults4Me-Profil (*.txt)|*.txt|Alle Dateien|*.*", "Defaults4Me profile (*.txt)|*.txt|All files|*.*" } },

            { "expert.title", new[] { "Ich weiß, was ich tue", "I know what I'm doing" } },
            { "expert.text", new[] { "Windows schützt PDF und Browser-Links mit einem eigenen Treiber (UCPD), damit Programme den Standard-Browser nicht heimlich ändern.\n\nAbschalten braucht Administratorrechte und einen Neustart. Danach kann jedes Programm diese Zuordnungen ändern – auch unerwünschte. Ein Windows-Update kann den Schutz wieder einschalten. Zurück geht es über dasselbe Menü.", "Windows protects PDF and browser links with its own driver (UCPD), so programs cannot silently change your default browser.\n\nSwitching it off needs administrator rights and a restart. Afterwards any program can change these associations – unwanted ones too. A Windows update may switch it back on. Undo via the same menu." } },
            { "expert.check", new[] { "Ich habe das verstanden und will den Schutz abschalten.", "I understand and want to switch off the protection." } },
            { "expert.ok", new[] { "Abschalten", "Switch off" } },
            { "expert.cancel", new[] { "Abbrechen", "Cancel" } },
            { "ucpd.restart", new[] { "Erledigt. Wirkt nach einem Neustart von Windows.", "Done. Takes effect after restarting Windows." } },
            { "ucpd.fail", new[] { "Hat nicht geklappt (keine Administratorrechte?).", "Did not work (no administrator rights?)." } },
        };
    }
}
