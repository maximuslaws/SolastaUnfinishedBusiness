using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace SolastaUnfinishedBusiness.Api.Helpers
{
    internal sealed class CharacterFileEntry
    {
        internal string FileName { get; set; }
        internal long SizeBytes { get; set; }
        internal DateTime LastWriteTime { get; set; }
    }


    internal static class CharacterPoolHelper
    {
        internal static (string path, bool exists, CharacterFileEntry[] files) GetCharacterFiles()
        {
            var path = Environment.ExpandEnvironmentVariables("%USERPROFILE%\\AppData\\LocalLow\\Tactical Adventures\\Solasta\\Characters");

            try
            {
                var exists = Directory.Exists(path);

                if (!exists)
                {
                    return (path, false, Array.Empty<CharacterFileEntry>());
                }

                var entries = Directory.GetFiles(path, "*.chr", SearchOption.TopDirectoryOnly)
                    .Select(f =>
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            return new CharacterFileEntry
                            {
                                FileName = Path.GetFileName(f),
                                SizeBytes = fi.Length,
                                LastWriteTime = fi.LastWriteTime
                            };
                        }
                        catch
                        {
                            return new CharacterFileEntry
                            {
                                FileName = Path.GetFileName(f),
                                SizeBytes = 0,
                                LastWriteTime = DateTime.MinValue
                            };
                        }
                    })
                    .OrderBy(e => e.FileName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();

                return (path, true, entries);
            }
            catch
            {
                return (path, false, Array.Empty<CharacterFileEntry>());
            }
        }

        internal static CharacterFileEntry[] GetCharacterFileEntries()
        {
            var (path, exists, files) = GetCharacterFiles();
            return files ?? Array.Empty<CharacterFileEntry>();
        }

        internal sealed class CharacterFileMetadata
        {
            internal string FullPath { get; set; }
            internal long SizeBytes { get; set; }
            internal double SizeKB => SizeBytes / 1024.0;
            internal DateTime Created { get; set; }
            internal DateTime LastModified { get; set; }
        }

        internal static CharacterFileMetadata GetCharacterFileMetadata(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var basePath = Environment.ExpandEnvironmentVariables("%USERPROFILE%\\AppData\\LocalLow\\Tactical Adventures\\Solasta\\Characters");

            try
            {
                // ensure filename is a simple file name
                if (Path.GetFileName(fileName) != fileName)
                {
                    return null;
                }

                var full = Path.Combine(basePath, fileName);
                var fullResolved = Path.GetFullPath(full);
                var baseResolved = Path.GetFullPath(basePath) + Path.DirectorySeparatorChar;

                // ensure resolved path is inside the characters folder
                if (!fullResolved.StartsWith(baseResolved, StringComparison.CurrentCultureIgnoreCase))
                {
                    return null;
                }

                if (!File.Exists(fullResolved))
                {
                    return null;
                }

                var fi = new FileInfo(fullResolved);

                return new CharacterFileMetadata
                {
                    FullPath = fullResolved,
                    SizeBytes = fi.Length,
                    Created = fi.CreationTime,
                    LastModified = fi.LastWriteTime
                };
            }
            catch
            {
                return null;
            }
        }

        internal static byte[] ReadCharacterFilePrefix(string fileName, int maxBytes)
        {
            var meta = GetCharacterFileMetadata(fileName);
            if (meta == null)
            {
                return Array.Empty<byte>();
            }

            try
            {
                using var fs = new FileStream(meta.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var toRead = Math.Min(maxBytes, (int)Math.Min(maxBytes, fs.Length));
                var buffer = new byte[toRead];
                var read = fs.Read(buffer, 0, toRead);
                if (read == toRead)
                {
                    return buffer;
                }

                if (read <= 0)
                {
                    return Array.Empty<byte>();
                }

                var actual = new byte[read];
                Array.Copy(buffer, actual, read);
                return actual;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        internal static string BytesToHexString(byte[] bytes, int bytesPerLine = 16)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < bytes.Length; i += bytesPerLine)
            {
                var line = bytes.Skip(i).Take(bytesPerLine).ToArray();
                sb.AppendLine(string.Join(" ", line.Select(b => b.ToString("X2"))));
            }

            return sb.ToString();
        }

        internal static string BytesToSafeText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                // best-effort UTF8 decode; invalid sequences replaced
                var text = Encoding.UTF8.GetString(bytes);

                var sb = new StringBuilder(text.Length);
                foreach (var c in text)
                {
                    // allow common printable and whitespace characters
                    if (c >= 32 && c != 127)
                    {
                        sb.Append(c);
                    }
                    else if (c == '\r' || c == '\n' || c == '\t')
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append('\u00B7'); // middle dot for non-printable
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string DiscoverRuntimeCharacterPoolApis()
        {
            var exactTargets = new[]
            {
                "ICharacterPoolService",
                "CharacterPoolManager",
                "RulesetCharacterHero+Snapshot",
                "BinarySerializer",
                "Serializer",
                "IElementsSerializer"
            };

            var methodFilters = new[] { "Load", "Save", "Import", "Export", "Character", "Snapshot", "Serialize", "Deserialize", "Read", "Write", "Pool" };
            var propFilters = new[] { "Character", "Snapshot", "Pool", "Serializer", "Save" };

            var sb = new StringBuilder();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var found = new List<Type>();

            // Narrow scan: only look for exact target simple names or the nested snapshot type
            foreach (var asm in assemblies)
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    // check for nested RulesetCharacterHero+Snapshot
                    if (t.IsNested && t.DeclaringType != null && (t.DeclaringType.Name + "+" + t.Name).Equals("RulesetCharacterHero+Snapshot", StringComparison.CurrentCultureIgnoreCase))
                    {
                        found.Add(t);
                        continue;
                    }

                    if (exactTargets.Any(x => t.Name.Equals(x, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        found.Add(t);
                    }
                }

                // stop early if we found all targets
                if (found.Select(t => t.Name).Distinct(StringComparer.CurrentCultureIgnoreCase).Count() >= exactTargets.Length)
                {
                    break;
                }
            }

            if (found.Count == 0)
            {
                sb.AppendLine("No matching runtime types found in loaded assemblies.");
                return sb.ToString();
            }

            // process found types in the order of exactTargets
            var ordered = new List<Type>();
            foreach (var name in exactTargets)
            {
                var match = found.FirstOrDefault(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) || (t.IsNested && (t.DeclaringType != null && (t.DeclaringType.Name + "+" + t.Name).Equals(name, StringComparison.CurrentCultureIgnoreCase))));
                if (match != null)
                {
                    ordered.Add(match);
                }
            }

            // append any remaining found types
            ordered.AddRange(found.Where(t => !ordered.Contains(t)).OrderBy(t => t.FullName));

            foreach (var t in ordered)
            {
                try
                {
                    sb.AppendLine($"Assembly: {t.Assembly.GetName().Name}");
                    sb.AppendLine($"Type: {t.FullName}");
                    sb.AppendLine($"  BaseType: {t.BaseType?.FullName}");

                    var ifs = t.GetInterfaces();
                    if (ifs.Length > 0)
                    {
                        sb.AppendLine($"  Interfaces: {string.Join(", ", ifs.Select(i => i.FullName))}");
                    }

                    var includeAll = t.Name.Equals("ICharacterPoolService", StringComparison.CurrentCultureIgnoreCase) ||
                                     t.Name.Equals("CharacterPoolManager", StringComparison.CurrentCultureIgnoreCase);

                    MethodInfo[] methods;
                    try
                    {
                        methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    }
                    catch
                    {
                        methods = Array.Empty<MethodInfo>();
                    }

                    var matchedMethods = includeAll
                        ? methods
                        : methods.Where(m => methodFilters.Any(f => m.Name.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToArray();

                    if (matchedMethods.Length > 0)
                    {
                        sb.AppendLine("  Methods:");
                        foreach (var m in matchedMethods.OrderBy(m => m.Name))
                        {
                            var modifiers = new List<string>();
                            if (m.IsPublic) modifiers.Add("public"); else if (m.IsPrivate) modifiers.Add("private"); else modifiers.Add("internal/protected");
                            if (m.IsStatic) modifiers.Add("static");

                            var parameters = m.GetParameters();
                            var paramList = string.Join(", ", parameters.Select(p => p.ParameterType.FullName + " " + p.Name));

                            sb.AppendLine($"    {string.Join(" ", modifiers)} {m.ReturnType.FullName} {m.Name}({paramList})");
                        }
                    }

                    PropertyInfo[] props;
                    try
                    {
                        props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    }
                    catch
                    {
                        props = Array.Empty<PropertyInfo>();
                    }

                    var matchedProps = includeAll
                        ? props
                        : props.Where(p => propFilters.Any(f => p.Name.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToArray();

                    if (matchedProps.Length > 0)
                    {
                        sb.AppendLine("  Properties:");
                        foreach (var p in matchedProps.OrderBy(p => p.Name))
                        {
                            var access = (p.GetMethod ?? p.SetMethod);
                            var visibility = access != null && access.IsPublic ? "public" : (access != null && access.IsPrivate ? "private" : "internal/protected");
                            sb.AppendLine($"    {visibility} {p.PropertyType.FullName} {p.Name}");
                        }
                    }

                    FieldInfo[] fields;
                    try
                    {
                        fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    }
                    catch
                    {
                        fields = Array.Empty<FieldInfo>();
                    }

                    var matchedFields = includeAll
                        ? fields
                        : fields.Where(f => propFilters.Any(filter => f.Name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToArray();

                    if (matchedFields.Length > 0)
                    {
                        sb.AppendLine("  Fields:");
                        foreach (var f in matchedFields.OrderBy(f => f.Name))
                        {
                            var vis = f.IsPublic ? "public" : (f.IsPrivate ? "private" : "internal/protected");
                            sb.AppendLine($"    {vis} {f.FieldType.FullName} {f.Name}");
                        }
                    }

                    sb.AppendLine();
                }
                catch
                {
                    // ignore per-type errors
                }
            }

            // apply output limits
            var full = sb.ToString();
            var lines = full.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var truncated = false;

            if (lines.Length > 300)
            {
                full = string.Join(Environment.NewLine, lines.Take(300));
                truncated = true;
            }

            if (full.Length > 30000)
            {
                full = full.Substring(0, 30000);
                truncated = true;
            }

            if (truncated)
            {
                full += Environment.NewLine + "[truncated for safety]";
            }

            return full;
        }

        // Compare multiple character files read-only and produce a capped summary
        internal static string CompareCharactersReadOnly(string[] fileNames, int maxCharacters = 10)
        {
            if (fileNames == null || fileNames.Length == 0)
            {
                return "No files provided.";
            }

            var sb = new StringBuilder();
            var entries = new List<(string file, object hero, object snapshot, string loadError)>();

            var capped = fileNames.Take(maxCharacters).ToArray();

            foreach (var fn in capped)
            {
                try
                {
                    var (hero, snapshot, summary) = LoadCharacterSnapshotReadOnly(fn);
                    // summary may contain errors; if hero/snapshot null and summary has message, treat as loadError
                    var loadErr = (hero == null && snapshot == null) ? summary : null;
                    entries.Add((fn, hero, snapshot, loadErr));
                }
                catch (Exception e)
                {
                    entries.Add((fn, null, null, e.Message));
                }
            }

            // helper safe getters
            object SafeGet(object obj, string name)
            {
                if (obj == null) return null;
                try
                {
                    var t = obj.GetType();
                    var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (p != null) return p.GetValue(obj);
                    var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (f != null) return f.GetValue(obj);
                }
                catch { }
                return null;
            }

            string GetDefName(object def)
            {
                if (def == null) return "null";
                try
                {
                    var n = SafeGet(def, "Name") ?? SafeGet(def, "name");
                    if (n != null) return n.ToString();
                    var gp = SafeGet(def, "GuiPresentation") ?? SafeGet(def, "guiPresentation");
                    if (gp != null)
                    {
                        var title = SafeGet(gp, "Title") ?? SafeGet(gp, "title") ?? SafeGet(gp, "Description");
                        if (title != null) return title.ToString();
                    }
                    var s = def.ToString();
                    if (!string.IsNullOrEmpty(s) && s.Length < 200) return s;
                    return def.GetType().FullName;
                }
                catch { return "unavailable"; }
            }

            string DescribeSimple(object obj)
            {
                if (obj == null) return "null";
                if (obj is string s) return string.IsNullOrWhiteSpace(s) ? "(empty)" : s;
                var t = obj.GetType();
                if (t.IsPrimitive || obj is decimal || t.IsEnum) return obj.ToString();
                if (obj is byte[] b) return $"byte[{b.Length}]";
                if (obj is System.Collections.IEnumerable ie && !(obj is string))
                {
                    var c = 0; foreach (var _ in ie) { if (++c > 1000) break; };
                    return $"Collection(count~={c})";
                }
                return GetDefName(obj);
            }

            // collect per-character compact summaries
            var summaries = new List<Dictionary<string, string>>();

            foreach (var e in entries)
            {
                var map = new Dictionary<string, string>();
                map["FileName"] = e.file;
                if (!string.IsNullOrEmpty(e.loadError))
                {
                    map["LoadError"] = e.loadError;
                    summaries.Add(map);
                    continue;
                }

                var snap = e.snapshot;
                var hero = e.hero;

                // Snapshot fields
                string ss(string n) { return DescribeSimple(SafeGet(snap, n) ?? SafeGet(hero, n)); }

                map["Snapshot.Name"] = ss("Name");
                map["Snapshot.SurName"] = ss("SurName");
                map["Snapshot.Race"] = ss("Race");
                map["Snapshot.SubRace"] = ss("SubRace");
                map["Snapshot.Background"] = ss("Background");
                map["Snapshot.Sex"] = ss("Sex") == "null" ? ss("Gender") : ss("Sex");

                // Collections: Classes, Levels, Subclasses
                try
                {
                    var cls = SafeGet(snap, "Classes") ?? SafeGet(hero, "Classes");
                    if (cls is System.Collections.IEnumerable ic && !(cls is string))
                    {
                        var list = new List<string>(); int c = 0;
                        foreach (var it in ic)
                        {
                            list.Add(DescribeSimple(it)); if (++c >= 20) break;
                        }
                        map["Snapshot.Classes"] = list.Count == 0 ? "(empty)" : string.Join(",", list);
                    }
                    else map["Snapshot.Classes"] = DescribeSimple(cls);
                }
                catch { map["Snapshot.Classes"] = "unavailable"; }

                try { map["Snapshot.Levels"] = DescribeSimple(SafeGet(snap, "Levels") ?? SafeGet(hero, "Levels")); } catch { map["Snapshot.Levels"] = "unavailable"; }
                try { map["Snapshot.Subclasses"] = DescribeSimple(SafeGet(snap, "Subclasses") ?? SafeGet(hero, "Subclasses")); } catch { map["Snapshot.Subclasses"] = "unavailable"; }

                map["Snapshot.CurrentHitPoints"] = ss("CurrentHitPoints");
                map["Snapshot.MaxHitPoints"] = ss("MaxHitPoints");
                map["Snapshot.BuiltIn"] = ss("BuiltIn");
                map["Snapshot.EditorOnly"] = ss("EditorOnly");
                map["Snapshot.TemplateName"] = ss("TemplateName");
                map["Snapshot.Imported"] = ss("Imported");

                // portrait
                try
                {
                    var pt = SafeGet(snap, "PortraitTextureMode") ?? SafeGet(snap, "portraitTextureMode");
                    map["Snapshot.PortraitTextureMode"] = DescribeSimple(pt);
                    var photo = SafeGet(snap, "RulesetCharacterPhotoData") ?? SafeGet(snap, "RulesetCharacterPhotoData");
                    if (photo is byte[] pb) map["Snapshot.Photo"] = $"byte[{pb.Length}]"; else map["Snapshot.Photo"] = DescribeSimple(photo);
                }
                catch { map["Snapshot.PortraitTextureMode"] = "unavailable"; map["Snapshot.Photo"] = "unavailable"; }

                // Hero fields
                try
                {
                    var rd = SafeGet(hero, "RaceDefinition");
                    map["Hero.RaceDefinition"] = rd == null ? "null" : GetDefName(rd);
                }
                catch { map["Hero.RaceDefinition"] = "unavailable"; }

                try { map["Hero.SubRaceDefinition"] = SafeGet(hero, "SubRaceDefinition") == null ? "null" : GetDefName(SafeGet(hero, "SubRaceDefinition")); } catch { map["Hero.SubRaceDefinition"] = "unavailable"; }
                try { map["Hero.BackgroundDefinition"] = SafeGet(hero, "BackgroundDefinition") == null ? "null" : GetDefName(SafeGet(hero, "BackgroundDefinition")); } catch { map["Hero.BackgroundDefinition"] = "unavailable"; }

                // ClassesAndLevels count/compact
                try
                {
                    var cal = SafeGet(hero, "ClassesAndLevels");
                    if (cal is System.Collections.IDictionary dcal) map["Hero.ClassesAndLevels"] = $"dict count={dcal.Count}";
                    else if (cal is System.Collections.IEnumerable ical)
                    {
                        var list = new List<string>(); int i = 0;
                        foreach (var it in ical) { list.Add(DescribeSimple(it)); if (++i >= 10) break; }
                        map["Hero.ClassesAndLevels"] = list.Count == 0 ? "(empty)" : string.Join(",", list);
                    }
                    else map["Hero.ClassesAndLevels"] = DescribeSimple(cal);
                }
                catch { map["Hero.ClassesAndLevels"] = "unavailable"; }

                // Attributes keys and six abilities
                try
                {
                    var attrs = SafeGet(hero, "Attributes");
                    if (attrs is System.Collections.IDictionary da)
                    {
                        map["Hero.Attributes.Count"] = da.Count.ToString();
                        var keys = new List<string>(); int k = 0; foreach (var key in da.Keys) { keys.Add(key?.ToString()); if (++k >= 25) break; }
                        map["Hero.Attributes.KeysSample"] = string.Join(",", keys);
                        var abilityNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
                        foreach (var an in abilityNames)
                        {
                            try
                            {
                                var val = da[an];
                                if (val != null)
                                {
                                    var vt = val.GetType();
                                    var baseV = vt.GetProperty("BaseValue")?.GetValue(val);
                                    var cur = vt.GetProperty("CurrentValue")?.GetValue(val);
                                    map[$"Attr.{an}"] = $"Base={baseV} Cur={cur}";
                                }
                                else map[$"Attr.{an}"] = "null";
                            }
                            catch { map[$"Attr.{an}"] = "unavailable"; }
                        }
                    }
                    else map["Hero.Attributes.Count"] = DescribeSimple(attrs);
                }
                catch { map["Hero.Attributes.Count"] = "unavailable"; }

                // Proficiencies counts/samples
                var profNames = new[] { "SkillProficiencies", "ToolTypeProficiencies", "WeaponTypeProficiencies", "WeaponCategoryProficiencies", "ArmorTypeProficiencies", "ArmorCategoryProficiencies", "LanguageProficiencies" };
                foreach (var pn in profNames)
                {
                    try
                    {
                        var val = SafeGet(hero, pn) ?? SafeGet(snap, pn);
                        if (val is System.Collections.IEnumerable ive && !(val is string))
                        {
                            var items = new List<string>(); int c = 0; foreach (var it in ive) { items.Add(DescribeSimple(it)); if (++c >= 10) break; }
                            map[$"Prof.{pn}"] = items.Count == 0 ? "(empty)" : string.Join(",", items);
                        }
                        else map[$"Prof.{pn}"] = DescribeSimple(val);
                    }
                    catch { map[$"Prof.{pn}"] = "unavailable"; }
                }

                // ActiveFeatures keys/counts
                try
                {
                    var af = SafeGet(hero, "ActiveFeatures");
                    if (af is System.Collections.IDictionary daf)
                    {
                        var keys = new List<string>(); int i = 0; foreach (var k in daf.Keys) { keys.Add(k?.ToString()); if (++i >= 20) break; }
                        map["ActiveFeatures.KeysSample"] = keys.Count == 0 ? "(empty)" : string.Join(",", keys);
                        map["ActiveFeatures.Count"] = daf.Count.ToString();
                    }
                    else map["ActiveFeatures"] = DescribeSimple(af);
                }
                catch { map["ActiveFeatures"] = "unavailable"; }

                // UsablePowers and SpellRepertoires counts/samples
                try
                {
                    var up = SafeGet(hero, "UsablePowers");
                    if (up is System.Collections.IEnumerable iup)
                    {
                        var list = new List<string>(); int i = 0; foreach (var it in iup) { list.Add(DescribeSimple(it)); if (++i >= 10) break; }
                        map["UsablePowers"] = list.Count == 0 ? "(empty)" : string.Join(",", list);
                    }
                    else map["UsablePowers"] = DescribeSimple(up);
                }
                catch { map["UsablePowers"] = "unavailable"; }

                try
                {
                    var sr = SafeGet(hero, "SpellRepertoires");
                    if (sr is System.Collections.IEnumerable isr)
                    {
                        var list = new List<string>(); int i = 0; foreach (var it in isr) { list.Add(DescribeSimple(it)); if (++i >= 10) break; }
                        map["SpellRepertoires"] = list.Count == 0 ? "(empty)" : string.Join(",", list);
                    }
                    else map["SpellRepertoires"] = DescribeSimple(sr);
                }
                catch { map["SpellRepertoires"] = "unavailable"; }

                // Visual candidates (sample fields)
                var visualNames = new[] { "BodyHeight", "BodyAssetPrefix", "BodyDecorationAssetSuffix", "FaceShapeAssetPrefix", "HairShapeAssetPrefix", "BeardShapeAssetPrefix", "VoiceID", "voiceID", "bodyAssetPrefix", "faceShapeAssetPrefix", "hairShapeAssetPrefix", "beardShapeAssetPrefix" };
                foreach (var vn in visualNames)
                {
                    try { map[$"Visual.{vn}"] = DescribeSimple(SafeGet(hero, vn) ?? SafeGet(snap, vn)); } catch { map[$"Visual.{vn}"] = "unavailable"; }
                }

                summaries.Add(map);
            }

            // Output per-character summaries
            sb.AppendLine($"Comparing {summaries.Count} characters (capped at {maxCharacters})");
            sb.AppendLine();

            foreach (var s in summaries)
            {
                var fname = s.ContainsKey("FileName") ? s["FileName"] : "(unknown)";
                sb.AppendLine($"--- {fname} ---");
                if (s.ContainsKey("LoadError")) { sb.AppendLine($"LoadError: {s["LoadError"]}"); sb.AppendLine(); continue; }
                foreach (var kv in s.OrderBy(kv => kv.Key))
                {
                    if (kv.Key == "FileName") continue;
                    sb.AppendLine($"{kv.Key}: {kv.Value}");
                }
                sb.AppendLine();
            }

            // Simple comparison matrix: for a selected set of keys, show distinct values per character and mark diffs
            var compareKeys = new[] { "Snapshot.Name", "Snapshot.Race", "Snapshot.SubRace", "Snapshot.Classes", "Snapshot.Levels", "Hero.RaceDefinition", "Hero.ClassesAndLevels", "Hero.Attributes.Count", "UsablePowers", "SpellRepertoires", "ActiveFeatures.Count" };
            sb.AppendLine("=== Comparison Matrix ===");
            sb.AppendLine("Key | " + string.Join(" | ", summaries.Select(s => s.ContainsKey("FileName") ? s["FileName"] : "(unknown)")));
            foreach (var key in compareKeys)
            {
                var vals = summaries.Select(s => s.ContainsKey(key) ? s[key] : "(missing)").ToArray();
                var distinct = vals.Distinct().Count();
                sb.AppendLine($"{key} | " + string.Join(" | ", vals) + (distinct > 1 ? "  <-- DIFF" : ""));
            }

            // caps
            var full = sb.ToString();
            var outLines = full.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var truncated = false;
            if (outLines.Length > 350) { full = string.Join(Environment.NewLine, outLines.Take(350)); truncated = true; }
            if (full.Length > 40000) { full = full.Substring(0, 40000); truncated = true; }
            if (truncated) full += Environment.NewLine + "[truncated for safety]";

            return full;
        }

        internal static string ResolvePoolKey(string selectedFileName)
        {
            if (string.IsNullOrEmpty(selectedFileName))
            {
                return null;
            }

            var baseName = Path.GetFileNameWithoutExtension(selectedFileName);

            try
            {
                var svc = ServiceRepository.GetService<ICharacterPoolService>();
                // if service available, try pool keys
                if (svc != null)
                {
                    try
                    {
                        var svcType = svc.GetType();
                        object poolObj = null;
                        var poolProp = svcType.GetProperty("Pool", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (poolProp != null)
                        {
                            poolObj = poolProp.GetValue(svc);
                        }
                        else
                        {
                            var poolField = svcType.GetField("Pool", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            if (poolField != null) poolObj = poolField.GetValue(svc);
                        }

                        var keys = new List<string>();
                        if (poolObj is System.Collections.IDictionary dict)
                        {
                            foreach (var k in dict.Keys) keys.Add(k?.ToString() ?? string.Empty);
                        }
                        else if (poolObj != null)
                        {
                            var keysProp = poolObj.GetType().GetProperty("Keys", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            if (keysProp != null)
                            {
                                var keysEnum = keysProp.GetValue(poolObj) as System.Collections.IEnumerable;
                                if (keysEnum != null)
                                {
                                    foreach (var k in keysEnum) keys.Add(k?.ToString() ?? string.Empty);
                                }
                            }
                        }

                        // look for key that ends with Characters\Aldrich.chr or /Characters/Aldrich.chr or contains \Characters\ and ends with filename
                        var candidates = keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
                        var filename = selectedFileName;
                        var suffix1 = Path.Combine("Characters", filename).Replace("\\", "\\");
                        foreach (var key in candidates)
                        {
                            if (key.EndsWith("/Characters/" + filename, StringComparison.CurrentCultureIgnoreCase) || key.EndsWith("\\Characters\\" + filename, StringComparison.CurrentCultureIgnoreCase) || (key.IndexOf("\\Characters\\", StringComparison.CurrentCultureIgnoreCase) >= 0 && key.EndsWith(filename, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                return key;
                            }
                        }

                        // none found, try BuildCharacterFilename
                        var buildMethod = svc.GetType().GetMethod("BuildCharacterFilename", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (buildMethod != null)
                        {
                            try
                            {
                                var res = buildMethod.Invoke(svc, new object[] { baseName, false });
                                return res?.ToString();
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                    }
                    catch
                    {
                        // ignore and fallback
                    }
                }

                // fallback: construct local characters path
                var local = Environment.ExpandEnvironmentVariables("%USERPROFILE%\\AppData\\LocalLow\\Tactical Adventures\\Solasta\\Characters");
                var full = Path.Combine(local, baseName + ".chr");
                return full;
            }
            catch
            {
                return null;
            }
        }

        internal static (object hero, object snapshot, string summary) LoadCharacterSnapshotReadOnly(string fileName)
        {
            // validate filename
            var meta = GetCharacterFileMetadata(fileName);
            if (meta == null)
            {
                return (null, null, "Invalid filename or file not found in characters folder.");
            }

            try
            {
                var svc = ServiceRepository.GetService<ICharacterPoolService>();
                if (svc == null)
                {
                    return (null, null, "ICharacterPoolService not available at runtime.");
                }

                var svcType = svc.GetType();
                // find LoadCharacter method
                var methods = svcType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                MethodInfo loadMethod = null;

                foreach (var m in methods.Where(m => m.Name.Equals("LoadCharacter", StringComparison.CurrentCultureIgnoreCase)))
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 1 && ps[0].ParameterType == typeof(string))
                    {
                        loadMethod = m;
                        break;
                    }
                }

                if (loadMethod == null)
                {
                    return (null, null, "LoadCharacter method not found on ICharacterPoolService implementation.");
                }

                var parameters = loadMethod.GetParameters();
                var args = new object[parameters.Length];
                // resolve pool key to pass to LoadCharacter
                var resolvedKey = ResolvePoolKey(fileName);
                if (parameters.Length > 0) args[0] = resolvedKey ?? fileName;
                for (var i = 1; i < parameters.Length; i++) args[i] = null;

                object invokeResult = null;
                try
                {
                    invokeResult = loadMethod.Invoke(svc, args);
                }
                catch (TargetInvocationException tie)
                {
                    return (null, null, "LoadCharacter threw: " + tie.InnerException?.Message ?? tie.Message);
                }
                catch (Exception e)
                {
                    return (null, null, "LoadCharacter invocation failed: " + e.Message);
                }

                // after invoke, out/ref params will be in args
                object hero = null;
                object snapshot = null;

                if (args.Length > 1) hero = args[1];
                if (args.Length > 2) snapshot = args[2];

                var sb = new StringBuilder();
                sb.AppendLine($"Service: {svcType.FullName}");

                var succeeded = true;
                if (loadMethod.ReturnType == typeof(bool) && invokeResult is bool b)
                {
                    succeeded = b;
                    sb.AppendLine($"LoadCharacter returned: {b}");
                }

                sb.AppendLine($"ResolvedPoolKey: {resolvedKey}");

                // Build rich summary using reflection (read-only)
                var lines = new List<string>();

                void AddLine(string s) => lines.Add(s);

                AddLine("=== Load status ===");
                AddLine($"Selected filename: {fileName}");
                AddLine($"Resolved pool key: {resolvedKey}");
                AddLine($"Service type: {svcType.FullName}");
                AddLine($"Hero type: {(hero == null ? "null" : hero.GetType().FullName)}");
                AddLine($"Snapshot type: {(snapshot == null ? "null" : snapshot.GetType().FullName)}");
                AddLine($"Hero null: {hero == null}");
                AddLine($"Snapshot null: {snapshot == null}");

                // helper to safely read property/field
                object SafeGet(object obj, string name)
                {
                    if (obj == null) return null;
                    try
                    {
                        var t = obj.GetType();
                        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (p != null) return p.GetValue(obj);
                        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (f != null) return f.GetValue(obj);
                    }
                    catch
                    {
                        return null;
                    }
                    return null;
                }

                string Describe(object obj)
                {
                    if (obj == null) return "null";
                    try
                    {
                        var t = obj.GetType();
                        if (obj is string) return obj as string;
                        if (t.IsPrimitive || t.IsEnum || obj is decimal) return obj.ToString();
                        if (obj is System.Collections.IDictionary dict)
                        {
                            return $"Dictionary(count={dict.Count})";
                        }
                        if (obj is System.Collections.IEnumerable ie && !(obj is string))
                        {
                            var cnt = 0;
                            foreach (var _ in ie)
                            {
                                if (++cnt > 100) break;
                            }
                            return $"Collection(approxCount={cnt})";
                        }

                        // special visual types
                        var tn = t.FullName ?? t.Name;
                        if (tn.IndexOf("Texture2D", StringComparison.CurrentCultureIgnoreCase) >= 0 || tn.IndexOf("RenderTexture", StringComparison.CurrentCultureIgnoreCase) >= 0 || tn.IndexOf("Texture", StringComparison.CurrentCultureIgnoreCase) >= 0 || tn.IndexOf("Sprite", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        {
                            return t.FullName + " (visual resource present)";
                        }

                        return t.FullName;
                    }
                    catch
                    {
                        return "unavailable";
                    }
                }

                // try to extract a compact definition name/title safely
                string GetDefinitionName(object def)
                {
                    if (def == null) return "null";
                    try
                    {
                        var n = SafeGet(def, "Name") ?? SafeGet(def, "name");
                        if (n != null) return n.ToString();
                        var gp = SafeGet(def, "GuiPresentation") ?? SafeGet(def, "guiPresentation");
                        if (gp != null)
                        {
                            var title = SafeGet(gp, "Title") ?? SafeGet(gp, "title") ?? SafeGet(gp, "Description");
                            if (title != null) return Describe(title);
                        }
                        var s = def.ToString();
                        if (!string.IsNullOrEmpty(s) && s.Length < 200) return s;
                        return def.GetType().FullName;
                    }
                    catch { return "unavailable"; }
                }

                string FormatCollectionItem(object it)
                {
                    if (it == null) return "null";
                    if (it is string s)
                    {
                        if (string.IsNullOrWhiteSpace(s)) return "empty/none";
                        return s;
                    }
                    var t = it.GetType();
                    if (t.IsPrimitive || it is decimal || it is Enum) return it.ToString();
                    return GetDefinitionName(it);
                }

                string GetPowerName(object p)
                {
                    if (p == null) return "null";
                    if (p is string s) return string.IsNullOrWhiteSpace(s) ? "empty/none" : s;
                    try
                    {
                        var candidates = new[] { "PowerDefinition", "powerDefinition", "FeatureDefinition", "Definition", "definition", "Name", "name" };
                        foreach (var c in candidates)
                        {
                            var v = SafeGet(p, c);
                            if (v != null)
                            {
                                if (v is string vs) return string.IsNullOrWhiteSpace(vs) ? "empty/none" : vs;
                                return Describe(v);
                            }
                        }
                        // fallback to definition name
                        return GetDefinitionName(p);
                    }
                    catch { return "unavailable"; }
                }

                AddLine(""); 
                AddLine("=== Identity / basic metadata ==="); 
                // prefer snapshot fields for identity
                var idProps = new[] { "Name", "SurName", "BuiltIn", "Race", "SubRace", "Background", "Sex", "Gender", "Age", "Height", "Weight" };
                foreach (var prop in idProps)
                {
                    var val = SafeGet(snapshot, prop) ?? SafeGet(hero, prop);
                    AddLine($"{prop}: {Describe(val)}");
                }

                AddLine(""); 
                AddLine("=== Class / progression ==="); 
                var classProps = new[] { "Classes", "ClassesHistory", "ClassLevels", "Level", "Experience", "Deity", "LevelValue", "CharacterLevel" };
                foreach (var prop in classProps)
                {
                    var val = SafeGet(snapshot, prop) ?? SafeGet(hero, prop);
                    if (val == null) { AddLine($"{prop}: null"); continue; }
                    // if collection, show count
                    if (val is System.Collections.IEnumerable ie && !(val is string))
                    {
                        var cnt = 0;
                        foreach (var _ in ie)
                        {
                            if (++cnt > 500) break;
                        }
                        AddLine($"{prop}: collection (approxCount={cnt})");
                    }
                    else
                    {
                        AddLine($"{prop}: {Describe(val)}");
                    }
                }

                AddLine(""); 
                AddLine("=== Ability scores ==="); 
                var stats = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
                foreach (var sname in stats)
                {
                    var val = SafeGet(hero, sname) ?? SafeGet(snapshot, sname) ?? SafeGet(hero, sname + "Value") ?? SafeGet(snapshot, sname + "Value");
                    AddLine($"{sname}: {Describe(val)}");
                }

                AddLine("");
                AddLine("=== Raw member map (declared) ===");
                int rawLimit = 120;
                int rawCount = 0;
                void DumpMembers(object obj, string prefix)
                {
                    if (obj == null) { AddLine($"{prefix}: null"); return; }
                    try
                    {
                        var t = obj.GetType();
                        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(p => p.DeclaringType == t).ToArray();
                        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(f => f.DeclaringType == t).ToArray();

                        foreach (var p in props)
                        {
                            if (rawCount++ >= rawLimit) { AddLine("... (raw member limit reached)"); return; }
                            bool readable = p.GetMethod != null;
                            object val = null;
                            try { if (readable) val = p.GetValue(obj); } catch { val = "unreadable"; }
                            var cnt = "";
                            if (val is System.Collections.IEnumerable ie && !(val is string)) { var c = 0; foreach (var _ in ie) { if (++c > 1000) break; } cnt = $" count={c}"; }
                            AddLine($"{prefix}.prop {p.Name}: {p.PropertyType.FullName} | readable: {readable} | null: {(readable && val == null ? "yes" : "no")}{cnt}");
                        }

                        foreach (var f in fields)
                        {
                            if (rawCount++ >= rawLimit) { AddLine("... (raw member limit reached)"); return; }
                            object val = null; try { val = f.GetValue(obj); } catch { val = "unreadable"; }
                            var cnt = "";
                            if (val is System.Collections.IEnumerable ie && !(val is string)) { var c = 0; foreach (var _ in ie) { if (++c > 1000) break; } cnt = $" count={c}"; }
                            AddLine($"{prefix}.field {f.Name}: {f.FieldType.FullName} | null: {(val == null ? "yes" : "no")}{cnt}");
                        }
                    }
                    catch { }
                }

                DumpMembers(snapshot, "Snapshot");
                DumpMembers(hero, "Hero");

                AddLine("");
                AddLine("=== Targeted snapshot compact values ===");
                var compactNames = new[] { "Classes", "Levels", "Subclasses", "RaceSort", "ClassSort", "CurrentHitPoints", "MaxHitPoints", "Guid", "Imported", "EditorOnly", "TemplateName" };
                foreach (var cn in compactNames)
                {
                    try
                    {
                        var val = SafeGet(snapshot, cn);
                        if (val == null)
                        {
                            AddLine($"{cn}: null");
                            continue;
                        }

                        if (val is System.Collections.IEnumerable ien && !(val is string))
                        {
                            var list = new List<string>();
                            foreach (var it in ien)
                            {
                                list.Add(FormatCollectionItem(it));
                                if (list.Count > 20) break;
                            }
                            if (list.Count <= 20) AddLine($"{cn}: [{string.Join(", ", list)}]"); else AddLine($"{cn}: collection count (showing first {list.Count})");
                        }
                        else
                        {
                            AddLine($"{cn}: {Describe(val)}");
                        }
                    }
                    catch { AddLine($"{cn}: unavailable"); }
                }

                AddLine("");
                AddLine("=== Targeted class / progression details ===");
                // ClassesAndLevels
                try
                {
                    var cal = SafeGet(hero, "ClassesAndLevels");
                    if (cal == null) AddLine("ClassesAndLevels: null");
                    else if (cal is System.Collections.IDictionary dictCal)
                    {
                        AddLine($"ClassesAndLevels: dictionary count={dictCal.Count}");
                        var i = 0;
                        foreach (var k in dictCal.Keys)
                        {
                            if (++i > 10) break;
                            var v = dictCal[k];
                            var cname = GetDefinitionName(k);
                            string lvl = null;
                            try
                            {
                                if (v != null)
                                {
                                    var vt = v.GetType();
                                    var lv = vt.GetProperty("Level") ?? vt.GetProperty("LevelValue") ?? vt.GetProperty("Value");
                                    if (lv != null) lvl = lv.GetValue(v)?.ToString();
                                    else if (v is int) lvl = v.ToString();
                                }
                            }
                            catch { }
                            AddLine($"  {i}. {cname} -> level={lvl ?? Describe(v)}");
                        }
                    }
                    else if (cal is System.Collections.IEnumerable icol)
                    {
                        AddLine("ClassesAndLevels: enumerable");
                        var i = 0;
                        foreach (var item in icol)
                        {
                            if (++i > 10) break;
                            string cls = null; string lvl = null;
                            try
                            {
                                var itType = item.GetType();
                                var keyP = itType.GetProperty("Key") ?? itType.GetProperty("ClassDefinition") ?? itType.GetProperty("Class");
                                var valP = itType.GetProperty("Value") ?? itType.GetProperty("Level") ?? itType.GetProperty("LevelValue");
                                if (keyP != null) cls = GetDefinitionName(keyP.GetValue(item));
                                if (valP != null) lvl = valP.GetValue(item)?.ToString();
                            }
                            catch { }
                            AddLine($"  {i}. class={cls ?? Describe(item)} level={lvl ?? "?"}");
                        }
                    }
                    else AddLine($"ClassesAndLevels: {Describe(cal)}");
                }
                catch { AddLine("ClassesAndLevels: unavailable"); }

                // ClassesHistory
                try
                {
                    var ch = SafeGet(hero, "ClassesHistory");
                    if (ch == null) AddLine("ClassesHistory: null");
                    else if (ch is System.Collections.IEnumerable ich)
                    {
                        var items = new List<string>(); int c = 0;
                        foreach (var it in ich)
                        {
                            items.Add(FormatCollectionItem(it));
                            if (++c >= 10) break;
                        }
                        AddLine($"ClassesHistory: count approx (showing up to 10): [{string.Join(", ", items)}]");
                    }
                    else AddLine($"ClassesHistory: {Describe(ch)}");
                }
                catch { AddLine("ClassesHistory: unavailable"); }

                // ClassesAndSubclasses
                try
                {
                    var cas = SafeGet(hero, "ClassesAndSubclasses");
                    if (cas == null) AddLine("ClassesAndSubclasses: null");
                    else if (cas is System.Collections.IEnumerable ias)
                    {
                        var i = 0; AddLine("ClassesAndSubclasses:");
                        var anyCas = false;
                        foreach (var it in ias)
                        {
                            if (++i > 10) break;
                            try
                            {
                                var t = it.GetType();
                                var classP = t.GetProperty("ClassDefinition") ?? t.GetProperty("Class");
                                var subP = t.GetProperty("SubclassDefinition") ?? t.GetProperty("Subclass");
                                var cname = classP != null ? FormatCollectionItem(classP.GetValue(it)) : FormatCollectionItem(it);
                                var sname = subP != null ? FormatCollectionItem(subP.GetValue(it)) : null;
                                anyCas = true;
                                AddLine($"  {i}. {cname} -> {(string.IsNullOrEmpty(sname) ? "none" : sname)}");
                            }
                            catch { AddLine($"  {i}. unavailable"); }
                        }
                        if (!anyCas) AddLine("none / not selected / unavailable");
                    }
                    else AddLine($"ClassesAndSubclasses: {Describe(cas)}");
                }
                catch { AddLine("ClassesAndSubclasses: unavailable"); }

                AddLine("");
                AddLine("=== Targeted ability attribute details ===");
                try
                {
                    var attrs = SafeGet(hero, "Attributes");
                    if (attrs == null) AddLine("Attributes: null");
                    else if (attrs is System.Collections.IDictionary dictAttrs)
                    {
                        AddLine($"Attributes: dictionary count={dictAttrs.Count}");
                        var keys = new List<string>();
                        foreach (var k in dictAttrs.Keys) { keys.Add(k?.ToString()); if (keys.Count >= 25) break; }
                        AddLine($"First keys: [{string.Join(", ", keys)}]");
                        var abilityNames = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
                        var statKeys = dictAttrs.Keys.Cast<object>().Select(o => o?.ToString()).Where(s => !string.IsNullOrEmpty(s) && abilityNames.Any(k => s.IndexOf(k, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
                        foreach (var sk in statKeys)
                        {
                            try
                            {
                                var val = dictAttrs[sk];
                                var vt = val?.GetType();
                                var baseV = vt?.GetProperty("BaseValue")?.GetValue(val);
                                var cur = vt?.GetProperty("CurrentValue")?.GetValue(val);
                                var max = vt?.GetProperty("MaxValue")?.GetValue(val);
                                var vv = vt?.GetProperty("Value")?.GetValue(val);
                                var bonus = vt?.GetProperty("BonusValue")?.GetValue(val) ?? vt?.GetProperty("Bonus")?.GetValue(val);
                                var mod = vt?.GetProperty("Modifier")?.GetValue(val);
                                AddLine($"{sk}: type={vt?.FullName} Base={baseV} Current={cur} Max={max} Value={vv} Bonus={bonus} Modifier={mod}");
                            }
                            catch { AddLine($"{sk}: unavailable"); }
                        }
                    }
                    else if (attrs is System.Collections.IEnumerable iea)
                    {
                        var cnt = 0; var list = new List<string>();
                        foreach (var k in iea) { list.Add(Describe(k)); if (++cnt >= 25) break; }
                        AddLine($"Attributes: enumerable (showing up to 25): [{string.Join(", ", list)}]");
                    }
                    else AddLine($"Attributes: {Describe(attrs)}");
                }
                catch { AddLine("Attributes: unavailable"); }

                AddLine("");
                AddLine("=== Targeted proficiencies / trained lists ===");
                var profNames = new[] { "SkillProficiencies", "ToolTypeProficiencies", "WeaponTypeProficiencies", "WeaponCategoryProficiencies", "ArmorTypeProficiencies", "ArmorCategoryProficiencies", "LanguageProficiencies", "FeatProficiencies", "InvocationProficiencies" };
                foreach (var pn in profNames)
                {
                    try
                    {
                        var val = SafeGet(hero, pn) ?? SafeGet(snapshot, pn);
                        if (val == null) { AddLine($"{pn}: null"); continue; }
                        if (val is System.Collections.IEnumerable ien && !(val is string))
                        {
                            var items = new List<string>(); int c = 0;
                            foreach (var it in ien)
                            {
                                items.Add(it == null ? "null" : FormatCollectionItem(it));
                                if (++c > 50) break;
                            }
                            if (c <= 50) AddLine($"{pn}: [{string.Join(", ", items)}]"); else AddLine($"{pn}: collection count approx={c}");
                        }
                        else AddLine($"{pn}: {Describe(val)}");
                    }
                    catch { AddLine($"{pn}: unavailable"); }
                }

                AddLine("");
                AddLine("=== Targeted active features / powers / spells ===");
                try
                {
                    var af = SafeGet(hero, "ActiveFeatures");
                    if (af == null) AddLine("ActiveFeatures: null");
                    else if (af is System.Collections.IDictionary daf)
                    {
                        AddLine($"ActiveFeatures: dictionary count={daf.Count}");
                        var keys = new List<string>(); foreach (var k in daf.Keys) { keys.Add(k?.ToString()); if (keys.Count >= 50) break; }
                        AddLine($"Keys (first 50): [{string.Join(", ", keys)}]");
                        foreach (var k in daf.Keys)
                        {
                            try
                            {
                                var v = daf[k];
                                if (v is System.Collections.IEnumerable ive && !(v is string))
                                {
                                    var c = 0; foreach (var _ in ive) { if (++c > 500) break; }
                                    AddLine($"Feature {k}: list count approx={c}");
                                }
                                else AddLine($"Feature {k}: {Describe(v)}");
                            }
                            catch { }
                        }
                    }
                    else AddLine($"ActiveFeatures: {Describe(af)}");
                }
                catch { AddLine("ActiveFeatures: unavailable"); }

                try
                {
                    var up = SafeGet(hero, "UsablePowers");
                    if (up == null) AddLine("UsablePowers: null");
                    else if (up is System.Collections.IEnumerable iup)
                    {
                        var i = 0; var items = new List<string>();
                        foreach (var it in iup) { items.Add(it == null ? "null" : GetPowerName(it)); if (++i >= 25) break; }
                        AddLine($"UsablePowers: count approx (showing up to 25): [{string.Join(", ", items)}]");
                    }
                    else AddLine($"UsablePowers: {Describe(up)}");
                }
                catch { AddLine("UsablePowers: unavailable"); }

                try
                {
                    var sr = SafeGet(hero, "SpellRepertoires");
                    if (sr == null) AddLine("SpellRepertoires: null");
                    else if (sr is System.Collections.IEnumerable isr)
                    {
                        var i = 0; var items = new List<string>();
                        foreach (var it in isr) { items.Add(it == null ? "null" : GetPowerName(it)); if (++i >= 25) break; }
                        AddLine($"SpellRepertoires: count approx (showing up to 25): [{string.Join(", ", items)}]");
                    }
                    else AddLine($"SpellRepertoires: {Describe(sr)}");
                }
                catch { AddLine("SpellRepertoires: unavailable"); }

                AddLine("");
                AddLine("=== Visual preservation candidates ===");
                var appearanceKeywords = new[] { "Appearance", "Face", "Skin", "Hair", "Beard", "Eye", "Color", "Shape", "Voice", "Portrait", "Photo", "Head", "Body", "Decoration", "Tattoo", "Scar", "Equipment" };
                void ScanAppearance(object obj, string prefix)
                {
                    if (obj == null) return;
                    try
                    {
                        var t = obj.GetType();
                        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                        foreach (var p in props.Where(p => appearanceKeywords.Any(k => p.Name.IndexOf(k, StringComparison.CurrentCultureIgnoreCase) >= 0)))
                        {
                            object val = null; bool readable = false; string typeName = p.PropertyType.FullName;
                            try { if (p.GetMethod != null) { val = p.GetValue(obj); readable = true; } } catch { }
                            if (!readable) AddLine($"{prefix}.{p.Name}: {typeName} (not readable)");
                            else if (val == null) AddLine($"{prefix}.{p.Name}: {typeName} = null");
                            else if (val is string || val.GetType().IsPrimitive || val.GetType().IsEnum) AddLine($"{prefix}.{p.Name}: {typeName} = {val}");
                            else if (val is byte[] b) AddLine($"{prefix}.{p.Name}: {typeName} (byte[] length={b.Length})");
                            else if (val is System.Collections.IEnumerable ie && !(val is string)) { var c = 0; foreach (var _ in ie) { if (++c > 100) break; } AddLine($"{prefix}.{p.Name}: {typeName} (collection count={c})"); }
                            else AddLine($"{prefix}.{p.Name}: {typeName} (present)");
                        }

                        foreach (var f in fields.Where(f => appearanceKeywords.Any(k => f.Name.IndexOf(k, StringComparison.CurrentCultureIgnoreCase) >= 0)))
                        {
                            object val = null; string typeName = f.FieldType.FullName; try { val = f.GetValue(obj); } catch { }
                            if (val == null) AddLine($"{prefix}.{f.Name}: {typeName} = null");
                            else if (val is string || val.GetType().IsPrimitive || val.GetType().IsEnum) AddLine($"{prefix}.{f.Name}: {typeName} = {val}");
                            else if (val is byte[] b) AddLine($"{prefix}.{f.Name}: {typeName} (byte[] length={b.Length})");
                            else if (val is System.Collections.IEnumerable ie && !(val is string)) { var c = 0; foreach (var _ in ie) { if (++c > 100) break; } AddLine($"{prefix}.{f.Name}: {typeName} (collection count={c})"); }
                            else AddLine($"{prefix}.{f.Name}: {typeName} (present)");
                        }
                    }
                    catch { }
                }

                ScanAppearance(snapshot, "Snapshot");
                ScanAppearance(hero, "Hero");

                AddLine("");
                AddLine("=== Mechanics containers ===");
                var mechKeywords = new[] { "Skill", "Proficiency", "Feature", "Power", "Spell", "Invocation", "Feat", "Condition", "Attribute", "Class", "Subclass", "Background", "Race", "Ability", "Score" };
                void ScanMechanics(object obj, string prefix)
                {
                    if (obj == null) return;
                    try
                    {
                        var t = obj.GetType();
                        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                        foreach (var p in props.Where(p => mechKeywords.Any(k => p.Name.IndexOf(k, StringComparison.CurrentCultureIgnoreCase) >= 0)))
                        {
                            object val = null; bool readable = false; string typeName = p.PropertyType.FullName;
                            try { if (p.GetMethod != null) { val = p.GetValue(obj); readable = true; } } catch { }
                            if (!readable) { AddLine($"{prefix}.{p.Name}: {typeName} (not readable)"); continue; }
                            if (val == null) { AddLine($"{prefix}.{p.Name}: {typeName} = null"); continue; }
                            if (val is string || val.GetType().IsPrimitive || val.GetType().IsEnum) { AddLine($"{prefix}.{p.Name}: {typeName} = {val}"); continue; }
                            if (val is System.Collections.IEnumerable ie && !(val is string)) { var c = 0; foreach (var _ in ie) { if (++c > 500) break; } AddLine($"{prefix}.{p.Name}: {typeName} (collection count={c})"); continue; }
                            AddLine($"{prefix}.{p.Name}: {typeName} (present)");
                        }

                        foreach (var f in fields.Where(f => mechKeywords.Any(k => f.Name.IndexOf(k, StringComparison.CurrentCultureIgnoreCase) >= 0)))
                        {
                            object val = null; string typeName = f.FieldType.FullName; try { val = f.GetValue(obj); } catch { }
                            if (val == null) AddLine($"{prefix}.{f.Name}: {typeName} = null");
                            else if (val is string || val.GetType().IsPrimitive || val.GetType().IsEnum) AddLine($"{prefix}.{f.Name}: {typeName} = {val}");
                            else if (val is System.Collections.IEnumerable ie && !(val is string)) { var c = 0; foreach (var _ in ie) { if (++c > 500) break; } AddLine($"{prefix}.{f.Name}: {typeName} (collection count={c})"); }
                            else AddLine($"{prefix}.{f.Name}: {typeName} (present)");
                        }
                    }
                    catch { }
                }

                ScanMechanics(snapshot, "Snapshot");
                ScanMechanics(hero, "Hero");

                // finalize into sb with caps
                foreach (var l in lines) sb.AppendLine(l);
                var result = sb.ToString();
                var outLines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var truncatedFlag = false;
                if (outLines.Length > 350)
                {
                    result = string.Join(Environment.NewLine, outLines.Take(350));
                    truncatedFlag = true;
                }
                if (result.Length > 40000)
                {
                    result = result.Substring(0, 40000);
                    truncatedFlag = true;
                }
                if (truncatedFlag) result += Environment.NewLine + "[truncated for safety]";

                return (hero, snapshot, result);
            }
            catch (Exception e)
            {
                return (null, null, "Unexpected error: " + e.Message);
            }
        }

        internal static string InspectCharacterPoolKeys(string selectedFileName)
        {
            var sb = new StringBuilder();

            if (string.IsNullOrEmpty(selectedFileName))
            {
                return "No filename provided.";
            }

            var baseName = Path.GetFileNameWithoutExtension(selectedFileName);

            try
            {
                var svc = ServiceRepository.GetService<ICharacterPoolService>();
                if (svc == null)
                {
                    return "ICharacterPoolService not available at runtime.";
                }

                var svcType = svc.GetType();
                sb.AppendLine($"Service: {svcType.FullName}");

                // try to get Pool property/field
                object poolObj = null;
                try
                {
                    var poolProp = svcType.GetProperty("Pool", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (poolProp != null)
                    {
                        poolObj = poolProp.GetValue(svc);
                    }
                    else
                    {
                        var poolField = svcType.GetField("Pool", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (poolField != null)
                        {
                            poolObj = poolField.GetValue(svc);
                        }
                    }
                }
                catch
                {
                    poolObj = null;
                }

                if (poolObj == null)
                {
                    sb.AppendLine("Pool: not present or unavailable.");
                }
                else
                {
                    var keys = new List<string>();
                    try
                    {
                        if (poolObj is System.Collections.IDictionary dict)
                        {
                            foreach (var k in dict.Keys)
                            {
                                keys.Add(k?.ToString() ?? string.Empty);
                            }
                        }
                        else
                        {
                            var keysProp = poolObj.GetType().GetProperty("Keys", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            if (keysProp != null)
                            {
                                var keysEnum = keysProp.GetValue(poolObj) as System.Collections.IEnumerable;
                                if (keysEnum != null)
                                {
                                    foreach (var k in keysEnum)
                                    {
                                        keys.Add(k?.ToString() ?? string.Empty);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    sb.AppendLine($"Pool count: {keys.Count}");
                    var maxKeys = Math.Min(100, keys.Count);
                    sb.AppendLine($"Listing first {maxKeys} keys (of {keys.Count}):");

                    for (var i = 0; i < maxKeys; i++)
                    {
                        var key = keys[i];
                        var equalsFile = string.Equals(key, selectedFileName, StringComparison.CurrentCultureIgnoreCase);
                        var containsFile = key != null && key.IndexOf(selectedFileName, StringComparison.CurrentCultureIgnoreCase) >= 0;
                        var containsBase = key != null && key.IndexOf(baseName, StringComparison.CurrentCultureIgnoreCase) >= 0;

                        sb.AppendLine($"  {i + 1}. {key}  | equalsFile: {equalsFile} | containsFile: {containsFile} | containsBase: {containsBase}");
                    }
                }

                // BuildCharacterFilename
                try
                {
                    var buildMethod = svcType.GetMethod("BuildCharacterFilename", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (buildMethod != null)
                    {
                        string builtFalse, builtTrue;
                        try { builtFalse = buildMethod.Invoke(svc, new object[] { baseName, false })?.ToString(); }
                        catch { builtFalse = "(error)"; }
                        try { builtTrue = buildMethod.Invoke(svc, new object[] { baseName, true })?.ToString(); }
                        catch { builtTrue = "(error)"; }

                        sb.AppendLine($"BuildCharacterFilename(base,false): {builtFalse}");
                        sb.AppendLine($"BuildCharacterFilename(base,true): {builtTrue}");
                    }
                    else
                    {
                        sb.AppendLine("BuildCharacterFilename: not found");
                    }
                }
                catch
                {
                    sb.AppendLine("BuildCharacterFilename: error invoking");
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("Error: " + e.Message);
            }

            // caps: max 200 lines, max 20000 chars
            var full = sb.ToString();
            var lines = full.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var truncated = false;
            if (lines.Length > 200) { full = string.Join(Environment.NewLine, lines.Take(200)); truncated = true; }
            if (full.Length > 20000) { full = full.Substring(0, 20000); truncated = true; }
            if (truncated) full += Environment.NewLine + "[truncated for safety]";
            return full;
        }
    }
}
