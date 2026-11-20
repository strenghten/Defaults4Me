using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Defaults4Me
{
    class App
    {
        public string Key;          // name under RegisteredApplications
        public string Name;         // display name
        public Dictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // .ext -> ProgId
        public bool IsNew;
        public override string ToString() { return (IsNew ? "★ " : "") + Name + "  (" + Types.Count + ")"; }
    }

    static class Apps
    {
        public static List<App> Load()
        {
            var list = new Dictionary<string, App>(StringComparer.OrdinalIgnoreCase);
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using (var baseKey = RegistryKey.OpenBaseKey(hive == Registry.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, view))
                        using (var reg = baseKey.OpenSubKey(@"SOFTWARE\RegisteredApplications"))
                        {
                            if (reg == null) continue;
                            foreach (var name in reg.GetValueNames())
                            {
                                if (list.ContainsKey(name)) continue;
                                var cap = reg.GetValue(name) as string;
                                if (string.IsNullOrEmpty(cap)) continue;
                                using (var c = baseKey.OpenSubKey(cap))
                                using (var fa = baseKey.OpenSubKey(cap + @"\FileAssociations"))
                                {
                                    if (c == null || fa == null) continue;
                                    var app = new App { Key = name };
                                    app.Name = Native.Indirect(c.GetValue("ApplicationName") as string);
                                    if (string.IsNullOrEmpty(app.Name) || app.Name.StartsWith("@")) app.Name = name;
                                    foreach (var ext in fa.GetValueNames())
                                    {
                                        var pid = fa.GetValue(ext) as string;
                                        if (ext.StartsWith(".") && !string.IsNullOrEmpty(pid)) app.Types[ext.ToLowerInvariant()] = pid;
                                    }
                                    if (app.Types.Count > 0) list[name] = app;
                                }
                            }
                        }
                    }
                    catch { }
                }
            var known = Store.KnownApps();
            bool first = known.Count == 0;
            foreach (var a in list.Values) a.IsNew = !first && !known.Contains(a.Key);
            Store.SaveKnownApps(list.Keys);
            return list.Values.OrderByDescending(a => a.IsNew).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static App Find(List<App> apps, string q)
        {
            string n = Norm(q);
            return apps.FirstOrDefault(a => a.Key.Equals(q, StringComparison.OrdinalIgnoreCase) || a.Name.Equals(q, StringComparison.OrdinalIgnoreCase))
                ?? apps.FirstOrDefault(a => Norm(a.Name) == n || Norm(a.Key) == n)
                ?? apps.FirstOrDefault(a => Norm(a.Name).Contains(n) || Norm(a.Key).Contains(n));
        }

        static string Norm(string s) { return new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()); }

        /// <summary>Display name of the app behind a ProgId (for the "now" column).</summary>
        public static string Describe(string ext, List<App> apps)
        {
            string pid = Native.EffectiveProgId(ext);
            if (pid.Length == 0) return "–";
            foreach (var a in apps) { string p; if (a.Types.TryGetValue(ext, out p) && p.Equals(pid, StringComparison.OrdinalIgnoreCase)) return a.Name; }
            string exe = Native.EffectiveExe(ext);
            return exe.Length > 0 ? System.IO.Path.GetFileNameWithoutExtension(exe) + " (" + pid + ")" : pid;
        }
    }

    static class Groups
    {
        public static readonly string[] All = { "images", "videos", "music", "documents", "archives", "text", "other" };

        static readonly HashSet<string> Docs = Set(".pdf .doc .docx .docm .dot .dotx .odt .ott .rtf .xls .xlsx .xlsm .xlt .xltx .ods .ots .csv .ppt .pptx .pptm .pps .ppsx .odp .otp .odg .odf .epub .mobi .djvu .xps .oxps .pub .vsd .vsdx .wpd .wps .one");
        static readonly HashSet<string> Arch = Set(".zip .7z .rar .tar .gz .tgz .bz2 .tbz .tbz2 .xz .txz .zst .lz .lzma .cab .arj .z .cpio .rpm .deb .wim .iso .img .vhd .vhdx .dmg .lzh .lha .apfs .squashfs .zipx .001");
        static readonly HashSet<string> Txt = Set(".txt .log .ini .cfg .conf .json .xml .yaml .yml .md .markdown .toml .csv .tsv .ps1 .psm1 .psd1 .bat .cmd .vbs .py .js .ts .jsx .tsx .c .h .cpp .hpp .cs .java .go .rs .rb .php .sh .sql .css .scss .reg .inf .nfo .srt .ass .sub .diz .lst");
        static readonly HashSet<string> Img = Set(".jpg .jpeg .jpe .jfif .png .gif .bmp .dib .tif .tiff .webp .heic .heif .avif .ico .cur .svg .psd .raw .cr2 .cr3 .nef .arw .dng .orf .rw2 .pef .srw .raf .jxl .jxr .wdp .tga .pcx .exr .hdr .dds .xcf .emf .wmf .jp2 .j2k .jpc .jpf .jpx .jif .pbm .pgm .ppm .pnm .pcd .pic .ras .sld .xbm .xpm .iff .ilbm .sgi .rgb .rle .crw .srf .sr2 .kdc .mrw .x3f .erf .3fr .mef .mos .nrw .rwl .iiq .qoi");
        static readonly HashSet<string> Vid = Set(".mp4 .m4v .mkv .avi .mov .wmv .flv .webm .mpg .mpeg .mpe .m2v .ts .mts .m2ts .vob .3gp .3g2 .ogv .divx .rm .rmvb .asf .f4v .dv .3gp2 .3gpp .m1v .m2t .mod .mp2v .mp4v .mpv2 .ogm .ogx .tod .tts .asx .wvx .dvr-ms .wtv .mxf .nsv .amv");
        static readonly HashSet<string> Aud = Set(".mp3 .wav .flac .aac .m4a .ogg .oga .opus .wma .aif .aiff .ape .mka .mid .midi .ac3 .dts .amr .au .wv .m3u .m3u8 .pls .cda .mpa .mp2 .m4b .m4p .spx .xm .it .s3m .mod2 .caf .dsf .dff .tta .mpc .wax .xspf");

        static HashSet<string> Set(string s) { return new HashSet<string>(s.Split(' '), StringComparer.OrdinalIgnoreCase); }

        public static string Of(string ext)
        {
            string perceived = "", content = "";
            try
            {
                using (var k = Registry.ClassesRoot.OpenSubKey(ext))
                    if (k != null) { perceived = (k.GetValue("PerceivedType") as string ?? "").ToLowerInvariant(); content = (k.GetValue("Content Type") as string ?? "").ToLowerInvariant(); }
            }
            catch { }
            // Fixed lists first: Windows' own PerceivedType is sometimes odd (.ts = video vs. TypeScript, .csv = text).
            if (Img.Contains(ext)) return "images";
            if (Vid.Contains(ext)) return "videos";
            if (Aud.Contains(ext)) return "music";
            if (Arch.Contains(ext)) return "archives";
            if (Docs.Contains(ext)) return "documents";
            if (Txt.Contains(ext)) return "text";
            switch (perceived)
            {
                case "image": return "images";
                case "video": return "videos";
                case "audio": return "music";
                case "compressed": return "archives";
                case "document": return "documents";
                case "text": return "text";
            }
            if (content.StartsWith("image/")) return "images";
            if (content.StartsWith("video/")) return "videos";
            if (content.StartsWith("audio/")) return "music";
            if (content.StartsWith("text/")) return "text";
            return "other";
        }
    }
}
