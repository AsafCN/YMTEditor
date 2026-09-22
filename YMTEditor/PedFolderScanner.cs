using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace YMTEditor
{
    /// <summary>
    /// Builds a whole ped variation setup out of the NAMES of the files in a ped folder,
    /// so a ped with hundreds of drawables doesn't have to be clicked together by hand.
    ///
    /// Everything a ymt holds is already in the file names:
    ///     ig_mike^jbib_003_u.ydd            jbib, drawable 3, universal model -> propMask 1
    ///     ig_mike^jbib_diff_003_b_uni.ytd   ...its second texture (b), uni    -> texId 0
    ///     ig_mike^head_000_r.ydd            race model                        -> propMask 17
    ///     ig_mike^head_diff_000_a_whi.ytd   race texture, whi                 -> texId 1
    ///     ig_mike_p^p_head_002.ydd          prop on ANCHOR_HEAD, prop 2
    ///     ig_mike^jbib_003_u.yld            cloth                             -> ownsCloth
    ///     ig_mike^jbib_003_u_1.ydd          alternative                       -> numAlternatives
    ///
    /// A number with no files (usually 000) stays as an empty drawable, which is what
    /// gives a ped its "no mask / no vest" default look - same as adding an empty
    /// drawable in the editor by hand.
    /// </summary>
    public static class PedFolderScanner
    {
        // Texture skin suffixes; the index is the texId written to the ymt.
        public static readonly string[] Races = { "uni", "whi", "bla", "chi", "lat", "ara", "bal", "jam", "kor", "ita", "pak" };

        public const int PropMaskUniversal = 1;  // model ends with _u
        public const int PropMaskRace = 17;      // model ends with _r
        public const int MaxTextures = 26;       // a-z

        private static readonly string[] ComponentNames =
            Enum.GetNames(typeof(YMTTypes.ComponentNumbers));
        private static readonly string[] PropNames =
            Enum.GetNames(typeof(YMTTypes.PropNumbers)).OrderByDescending(n => n.Length).ToArray();

        private const string Prefix = @"(?:(?<prefix>[^\^]+)\^)?";
        private static readonly RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        private static readonly Regex CompModel = new Regex(
            Prefix + @"(?<slot>" + string.Join("|", ComponentNames) + @")_(?<num>\d{3})_(?<kind>[ur])(?:_(?<alt>\d+))?\.ydd$", Opts);
        private static readonly Regex CompTexture = new Regex(
            Prefix + @"(?<slot>" + string.Join("|", ComponentNames) + @")_diff_(?<num>\d{3})_(?<letter>[a-z])_(?<race>" + string.Join("|", Races) + @")\.ytd$", Opts);
        private static readonly Regex CompCloth = new Regex(
            Prefix + @"(?<slot>" + string.Join("|", ComponentNames) + @")_(?<num>\d{3})_[ur]\.yld$", Opts);
        private static readonly Regex PropModel = new Regex(
            Prefix + @"(?<slot>" + string.Join("|", PropNames) + @")_(?<num>\d{3})\.ydd$", Opts);
        private static readonly Regex PropTextureRx = new Regex(
            Prefix + @"(?<slot>" + string.Join("|", PropNames) + @")_diff_(?<num>\d{3})_(?<letter>[a-z])(?:_(?<race>" + string.Join("|", Races) + @"))?\.ytd$", Opts);

        //every clothing name pattern, with whether it belongs to a prop
        private static readonly Tuple<Regex, bool>[] _PATTERNS =
        {
            Tuple.Create(CompModel, false),
            Tuple.Create(CompTexture, false),
            Tuple.Create(CompCloth, false),
            Tuple.Create(PropModel, true),
            Tuple.Create(PropTextureRx, true),
        };

        #region scanning

        internal class ScanDrawable
        {
            public bool HasModel;
            public readonly HashSet<string> Kinds = new HashSet<string>();               // "u" / "r" models found
            public readonly Dictionary<int, HashSet<int>> Textures = new Dictionary<int, HashSet<int>>(); // letter -> races
            public int Alternatives;
            public bool HasCloth;
        }

        public class ScanPed
        {
            public string Name;      // the "name^" prefix these files use ("" when they have none)
            public int FileCount;
            public string MainDirectory; // the folder holding most of this ped, when full paths were scanned
            internal readonly Dictionary<string, int> DirectoryCounts = new Dictionary<string, int>();
            internal readonly SortedDictionary<int, SortedDictionary<int, ScanDrawable>> Comps =
                new SortedDictionary<int, SortedDictionary<int, ScanDrawable>>();
            internal readonly SortedDictionary<int, SortedDictionary<int, ScanDrawable>> Props =
                new SortedDictionary<int, SortedDictionary<int, ScanDrawable>>();

            public override string ToString()
            {
                return (string.IsNullOrEmpty(Name) ? "(no name^ prefix)" : Name) + "  -  " + FileCount + " files";
            }
        }

        public class ScanResult
        {
            public readonly List<ScanPed> Peds = new List<ScanPed>();
            public readonly List<string> Unrecognized = new List<string>();
            public readonly List<string> Duplicates = new List<string>();
            public int Ignored;
        }

        public class Warning
        {
            public string Level;   // "error" / "warn" / "info"
            public string Message;
            public Warning(string level, string message) { Level = level; Message = message; }
        }

        /// <summary>Everything one ped needs, ready to drop into the editor.</summary>
        public class PedLayout
        {
            public string Name;
            public string DlcName;
            public ObservableCollection<ComponentData> Components = new ObservableCollection<ComponentData>();
            public ObservableCollection<PropData> Props = new ObservableCollection<PropData>();
            public List<Warning> Warnings = new List<Warning>();
            public int DrawableCount, TextureCount, PropCount, PropTextureCount, EmptyCount;
        }

        /// <summary>Every file under <paramref name="folder"/>, sub-folders included.</summary>
        public static ScanResult ScanFolder(string folder)
        {
            return Scan(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
        }

        /// <summary>Group file names into peds. Only the file names are used, never their contents.</summary>
        public static ScanResult Scan(IEnumerable<string> paths)
        {
            ScanResult result = new ScanResult();
            Dictionary<string, ScanPed> peds = new Dictionary<string, ScanPed>();
            HashSet<string> seen = new HashSet<string>();

            foreach (string path in paths)
            {
                string name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (!seen.Add(name.ToLowerInvariant()))
                {
                    result.Duplicates.Add(path);
                    continue;
                }

                Match m;
                bool isProp = false;
                int kindOfMatch; // 0 = comp model, 1 = comp texture, 2 = cloth, 3 = prop model, 4 = prop texture

                if ((m = CompModel.Match(name)).Success) { kindOfMatch = 0; }
                else if ((m = CompTexture.Match(name)).Success) { kindOfMatch = 1; }
                else if ((m = CompCloth.Match(name)).Success) { kindOfMatch = 2; }
                else if ((m = PropModel.Match(name)).Success) { kindOfMatch = 3; isProp = true; }
                else if ((m = PropTextureRx.Match(name)).Success) { kindOfMatch = 4; isProp = true; }
                else
                {
                    string ext = Path.GetExtension(name).ToLowerInvariant();
                    if (ext == ".ydd" || ext == ".ytd" || ext == ".yld")
                    {
                        result.Unrecognized.Add(name); // looks like clothing, but isn't named the way GTA expects
                    }
                    else
                    {
                        result.Ignored++;
                    }
                    continue;
                }

                string prefix = m.Groups["prefix"].Success ? m.Groups["prefix"].Value : "";
                string pedName = PedNameFor(prefix, isProp);
                ScanPed ped;
                if (!peds.TryGetValue(pedName.ToLowerInvariant(), out ped))
                {
                    ped = new ScanPed { Name = pedName };
                    peds[pedName.ToLowerInvariant()] = ped;
                    result.Peds.Add(ped);
                }
                ped.FileCount++;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    ped.DirectoryCounts[directory] = ped.DirectoryCounts.ContainsKey(directory)
                        ? ped.DirectoryCounts[directory] + 1 : 1;
                }

                string slot = m.Groups["slot"].Value.ToLowerInvariant();
                int number = int.Parse(m.Groups["num"].Value);
                int slotId = isProp
                    ? (int)(YMTTypes.PropNumbers)Enum.Parse(typeof(YMTTypes.PropNumbers), slot)
                    : (int)(YMTTypes.ComponentNumbers)Enum.Parse(typeof(YMTTypes.ComponentNumbers), slot);

                ScanDrawable drawable = GetDrawable(isProp ? ped.Props : ped.Comps, slotId, number);

                switch (kindOfMatch)
                {
                    case 0:
                        if (m.Groups["alt"].Success)
                        {
                            drawable.Alternatives = Math.Max(drawable.Alternatives, int.Parse(m.Groups["alt"].Value));
                        }
                        else
                        {
                            drawable.HasModel = true;
                            drawable.Kinds.Add(m.Groups["kind"].Value.ToLowerInvariant());
                        }
                        break;
                    case 2:
                        drawable.HasCloth = true;
                        break;
                    case 3:
                        drawable.HasModel = true;
                        break;
                    default: // a texture; props may leave the skin suffix out
                        int race = m.Groups["race"].Success
                            ? Array.IndexOf(Races, m.Groups["race"].Value.ToLowerInvariant())
                            : 0;
                        int letter = char.ToLowerInvariant(m.Groups["letter"].Value[0]) - 'a';
                        if (!drawable.Textures.ContainsKey(letter))
                        {
                            drawable.Textures[letter] = new HashSet<int>();
                        }
                        drawable.Textures[letter].Add(race);
                        break;
                }
            }

            result.Peds.RemoveAll(p => p.Comps.Count == 0 && p.Props.Count == 0);
            result.Peds.Sort((a, b) => b.FileCount.CompareTo(a.FileCount));
            foreach (ScanPed ped in result.Peds)
            {
                if (ped.DirectoryCounts.Count > 0)
                {
                    ped.MainDirectory = ped.DirectoryCounts.OrderByDescending(kv => kv.Value).First().Key;
                }
            }
            return result;
        }

        private static ScanDrawable GetDrawable(SortedDictionary<int, SortedDictionary<int, ScanDrawable>> slots, int slotId, int number)
        {
            SortedDictionary<int, ScanDrawable> drawables;
            if (!slots.TryGetValue(slotId, out drawables))
            {
                drawables = new SortedDictionary<int, ScanDrawable>();
                slots[slotId] = drawables;
            }
            ScanDrawable drawable;
            if (!drawables.TryGetValue(number, out drawable))
            {
                drawable = new ScanDrawable();
                drawables[number] = drawable;
            }
            return drawable;
        }

        /// <summary>
        /// Which ymt a file belongs to. Props are streamed under a sibling name
        /// (ig_mike_p^... , mp_m_freemode_01_p_&lt;dlc&gt;^...), but share the ped's ymt.
        /// </summary>
        public static string PedNameFor(string prefix, bool isProp)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return "";
            }
            if (isProp)
            {
                Match freemode = Regex.Match(prefix, @"^(mp_[mf]_freemode_01)_p_(.+)$", RegexOptions.IgnoreCase);
                if (freemode.Success)
                {
                    return freemode.Groups[1].Value + "_" + freemode.Groups[2].Value;
                }
                if (prefix.EndsWith("_p", StringComparison.OrdinalIgnoreCase))
                {
                    return prefix.Substring(0, prefix.Length - 2);
                }
            }
            return prefix;
        }

        /// <summary>Freemode addons carry their dlc name; custom peds leave it empty.</summary>
        public static string DefaultDlcName(string pedName)
        {
            Match m = Regex.Match(pedName ?? "", @"^mp_[mf]_freemode_01_(.+)$", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "";
        }

        #endregion

        #region renumbering

        public class RenameEntry
        {
            public string From;      // full path
            public string To;        // full path
            public string Slot;
            public override string ToString()
            {
                return Path.GetFileName(From) + "   ->   " + Path.GetFileName(To);
            }
        }

        /// <summary>What sorting a ped folder would rename, worked out before anything is touched.</summary>
        public class RenumberPlan
        {
            public string Directory;
            public string PedName;
            public readonly List<RenameEntry> Renames = new List<RenameEntry>();
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> Summary = new List<string>();
            public int OtherFolders;
        }

        internal class ParsedFile
        {
            public string Path;
            public Match Match;
            public bool IsProp;
            public bool IsTexture;
            public int SlotId;
            public int Number;
            public int Letter = -1;
        }

        internal static ParsedFile ParseFile(string path)
        {
            string name = Path.GetFileName(path);
            foreach (var pattern in _PATTERNS)
            {
                Match m = pattern.Item1.Match(name);
                if (!m.Success)
                {
                    continue;
                }

                string slot = m.Groups["slot"].Value.ToLowerInvariant();
                ParsedFile f = new ParsedFile
                {
                    Path = path,
                    Match = m,
                    IsProp = pattern.Item2,
                    IsTexture = m.Groups["letter"].Success,
                    Number = int.Parse(m.Groups["num"].Value),
                    SlotId = pattern.Item2
                        ? (int)(YMTTypes.PropNumbers)Enum.Parse(typeof(YMTTypes.PropNumbers), slot)
                        : (int)(YMTTypes.ComponentNumbers)Enum.Parse(typeof(YMTTypes.ComponentNumbers), slot),
                };
                if (f.IsTexture)
                {
                    f.Letter = char.ToLowerInvariant(m.Groups["letter"].Value[0]) - 'a';
                }
                return f;
            }
            return null;
        }

        /// <summary>
        /// Plans the renames that close the gaps in a ped's numbering: 001 and 005 become
        /// 001 and 002, and a drawable whose textures are b, d becomes a, b.
        ///
        /// Numbers below the first one in use are left alone, so a component that
        /// deliberately starts at 001 (an empty 000 is the ped's "none" option) keeps that
        /// empty slot instead of everything sliding down onto it.
        ///
        /// Only the folder holding most of the ped's files is touched, since a resource
        /// often keeps older copies in sibling folders under the same names.
        /// </summary>
        public static RenumberPlan PlanRenumber(string folder, string pedKey)
        {
            RenumberPlan plan = new RenumberPlan();
            List<ParsedFile> parsed = new List<ParsedFile>();

            foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                ParsedFile f = ParseFile(path);
                if (f == null)
                {
                    continue;
                }
                string prefix = f.Match.Groups["prefix"].Success ? f.Match.Groups["prefix"].Value : "";
                if (PedNameFor(prefix, f.IsProp).ToLowerInvariant() != (pedKey ?? "").ToLowerInvariant())
                {
                    continue;
                }
                parsed.Add(f);
            }

            if (parsed.Count == 0)
            {
                plan.Problems.Add("No files for that ped in " + folder);
                return plan;
            }

            // one folder only: pick the one holding most of the ped
            var byDirectory = parsed.GroupBy(f => Path.GetDirectoryName(f.Path))
                                    .OrderByDescending(g => g.Count()).ToList();
            plan.Directory = byDirectory[0].Key;
            plan.OtherFolders = byDirectory.Count - 1;
            List<ParsedFile> files = byDirectory[0].ToList();

            HashSet<string> existing = new HashSet<string>(
                Directory.GetFiles(plan.Directory).Select(p => Path.GetFileName(p).ToLowerInvariant()));

            foreach (var slot in files.GroupBy(f => new { f.IsProp, f.SlotId })
                                      .OrderBy(g => g.Key.IsProp).ThenBy(g => g.Key.SlotId))
            {
                string slotName = slot.Key.IsProp
                    ? Enum.GetName(typeof(YMTTypes.PropNumbers), slot.Key.SlotId)
                    : Enum.GetName(typeof(YMTTypes.ComponentNumbers), slot.Key.SlotId);

                // numbers keep their order; the first one in use stays where it is
                List<int> numbers = slot.Select(f => f.Number).Distinct().OrderBy(n => n).ToList();
                Dictionary<int, int> numberMap = new Dictionary<int, int>();
                int next = numbers[0];
                foreach (int number in numbers)
                {
                    numberMap[number] = next++;
                }

                // texture letters always start at a
                Dictionary<int, Dictionary<int, int>> letterMap = new Dictionary<int, Dictionary<int, int>>();
                foreach (var drawable in slot.Where(f => f.IsTexture).GroupBy(f => f.Number))
                {
                    List<int> letters = drawable.Select(f => f.Letter).Distinct().OrderBy(l => l).ToList();
                    Dictionary<int, int> map = new Dictionary<int, int>();
                    for (int i = 0; i < letters.Count; i++)
                    {
                        map[letters[i]] = i;
                    }
                    letterMap[drawable.Key] = map;
                }

                int movedDrawables = numberMap.Count(kv => kv.Key != kv.Value);
                int movedTextures = letterMap.Sum(d => d.Value.Count(kv => kv.Key != kv.Value));
                if (movedDrawables > 0 || movedTextures > 0)
                {
                    plan.Summary.Add(slotName + ": " + string.Join(", ",
                        numberMap.Where(kv => kv.Key != kv.Value)
                                 .Select(kv => kv.Key.ToString("D3") + " -> " + kv.Value.ToString("D3")).ToArray())
                        + (movedTextures > 0 ? (movedDrawables > 0 ? ", " : "") + movedTextures + " texture(s) re-lettered" : ""));
                }

                foreach (ParsedFile f in slot)
                {
                    int newNumber = numberMap[f.Number];
                    int newLetter = f.IsTexture ? letterMap[f.Number][f.Letter] : -1;
                    if (newNumber == f.Number && (!f.IsTexture || newLetter == f.Letter))
                    {
                        continue;
                    }

                    string newName = Rename(f, newNumber, newLetter);
                    string target = Path.Combine(plan.Directory, newName);

                    //only a file that is itself moving may be in the way
                    if (existing.Contains(newName.ToLowerInvariant())
                        && !files.Any(o => string.Equals(Path.GetFileName(o.Path), newName, StringComparison.OrdinalIgnoreCase)
                                           && (numberMap[o.Number] != o.Number
                                               || (o.IsTexture && letterMap[o.Number][o.Letter] != o.Letter))))
                    {
                        plan.Problems.Add(newName + " already exists and is not being moved, so "
                            + Path.GetFileName(f.Path) + " was left alone.");
                        continue;
                    }

                    plan.Renames.Add(new RenameEntry { From = f.Path, To = target, Slot = slotName });
                }
            }

            return plan;
        }

        /// <summary>Same file name with a new drawable number, and texture letter.</summary>
        private static string Rename(ParsedFile f, int newNumber, int newLetter)
        {
            string name = Path.GetFileName(f.Path);
            Group num = f.Match.Groups["num"];
            Group letter = f.Match.Groups["letter"];

            //right to left, so the earlier group keeps its offset
            if (f.IsTexture && newLetter != f.Letter)
            {
                name = name.Remove(letter.Index, letter.Length)
                           .Insert(letter.Index, ((char)('a' + newLetter)).ToString());
            }
            if (newNumber != f.Number)
            {
                name = name.Remove(num.Index, num.Length).Insert(num.Index, newNumber.ToString("D3"));
            }
            return name;
        }

        /// <summary>
        /// Applies a plan. Everything moves to a temporary name first, so a file never
        /// lands on one that hasn't moved out of the way yet.
        /// </summary>
        public static void ApplyRenumber(RenumberPlan plan)
        {
            List<string> temps = new List<string>();
            foreach (RenameEntry r in plan.Renames)
            {
                string temp = r.From + ".renumbering";
                File.Move(r.From, temp);
                temps.Add(temp);
            }
            for (int i = 0; i < plan.Renames.Count; i++)
            {
                File.Move(temps[i], plan.Renames[i].To);
            }
        }

        #endregion

        #region resolving

        /// <summary>Turn scanned files into editor components/props, plus what looks wrong.</summary>
        public static PedLayout Resolve(ScanPed ped)
        {
            PedLayout layout = new PedLayout();
            layout.Name = ped.Name ?? "";
            layout.DlcName = DefaultDlcName(layout.Name);

            int pedRace = PedRace(ped);
            List<string> empties = new List<string>();

            int compIndex = 0;
            foreach (KeyValuePair<int, SortedDictionary<int, ScanDrawable>> slot in ped.Comps)
            {
                string slotName = Enum.GetName(typeof(YMTTypes.ComponentNumbers), slot.Key);
                ObservableCollection<ComponentDrawable> drawables = new ObservableCollection<ComponentDrawable>();

                foreach (ResolvedDrawable r in ResolveSlot(slotName, slot.Value, false, pedRace, layout.Warnings, empties))
                {
                    ObservableCollection<ComponentTexture> textures = new ObservableCollection<ComponentTexture>();
                    for (int i = 0; i < r.TexIds.Count; i++)
                    {
                        textures.Add(new ComponentTexture(XMLHandler.Number2String(i, false), r.TexIds[i]));
                    }
                    ObservableCollection<ComponentInfo> info = new ObservableCollection<ComponentInfo>
                    {
                        new ComponentInfo(slot.Key, r.Index)
                    };
                    drawables.Add(new ComponentDrawable(r.Index, r.TexIds.Count, r.PropMask, r.Alternatives, r.HasCloth, textures, info));
                    layout.TextureCount += r.TexIds.Count;
                }

                layout.Components.Add(new ComponentData(slotName, slot.Key, compIndex, drawables));
                layout.DrawableCount += drawables.Count;
                compIndex++;
            }

            foreach (KeyValuePair<int, SortedDictionary<int, ScanDrawable>> slot in ped.Props)
            {
                string slotName = Enum.GetName(typeof(YMTTypes.PropNumbers), slot.Key);
                ObservableCollection<PropDrawable> drawables = new ObservableCollection<PropDrawable>();

                foreach (ResolvedDrawable r in ResolveSlot(slotName, slot.Value, true, pedRace, layout.Warnings, empties))
                {
                    ObservableCollection<PropTexture> textures = new ObservableCollection<PropTexture>();
                    for (int i = 0; i < r.TexIds.Count; i++)
                    {
                        // a prop's texId is the texture's own index, not a skin tone
                        textures.Add(new PropTexture(XMLHandler.Number2String(i, false), i));
                    }
                    drawables.Add(new PropDrawable(r.Index, r.TexIds.Count, "none",
                        new string[] { "0", "0", "0", "0", "0" }, textures, "", 0, 0, slot.Key, r.Index, 0));
                    layout.PropTextureCount += r.TexIds.Count;
                }

                layout.Props.Add(new PropData(slotName, slot.Key, drawables));
                layout.PropCount += drawables.Count;
            }

            layout.EmptyCount = empties.Count;
            if (empties.Count > 0)
            {
                layout.Warnings.Add(new Warning("info", "Empty slots, kept so the numbers line up (no files, so they "
                    + "show nothing in game - the usual 'none' option): " + string.Join(", ", empties)));
            }
            layout.Warnings = layout.Warnings.OrderBy(w => LevelOrder(w.Level)).ToList();
            return layout;
        }

        private static int LevelOrder(string level)
        {
            if (level == "error") return 0;
            return level == "warn" ? 1 : 2;
        }

        private class ResolvedDrawable
        {
            public int Index;
            public int PropMask;
            public List<int> TexIds = new List<int>();
            public int Alternatives;
            public bool HasCloth;
        }

        /// <summary>The ped's skin tone: the race suffix used by most of its textures.</summary>
        private static int PedRace(ScanPed ped)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            foreach (SortedDictionary<int, ScanDrawable> slot in ped.Comps.Values)
            {
                foreach (ScanDrawable d in slot.Values)
                {
                    foreach (HashSet<int> races in d.Textures.Values)
                    {
                        foreach (int race in races)
                        {
                            if (race == 0) continue;
                            counts[race] = counts.ContainsKey(race) ? counts[race] + 1 : 1;
                        }
                    }
                }
            }
            if (counts.Count == 0)
            {
                return Array.IndexOf(Races, "whi");
            }
            return counts.OrderByDescending(c => c.Value).First().Key;
        }

        private static List<ResolvedDrawable> ResolveSlot(string label, SortedDictionary<int, ScanDrawable> drawables,
            bool isProp, int pedRace, List<Warning> warnings, List<string> empties)
        {
            List<ResolvedDrawable> rows = new List<ResolvedDrawable>();
            List<int> texturesOnly = new List<int>();
            List<int> modelsOnly = new List<int>();
            int count = drawables.Keys.Max() + 1;

            if (count > 128)
            {
                warnings.Add(new Warning("warn", label + ": " + count + " drawables - more than 128 can make players see different models."));
            }

            for (int num = 0; num < count; num++)
            {
                string tag = label + " " + num.ToString("D3");
                ScanDrawable d;
                if (!drawables.TryGetValue(num, out d))
                {
                    empties.Add(tag);
                    rows.Add(new ResolvedDrawable { Index = num, PropMask = PropMaskUniversal, TexIds = { 0 } });
                    continue;
                }

                bool hasUni = d.Textures.Values.Any(rs => rs.Contains(0));
                bool hasRace = d.Textures.Values.Any(rs => rs.Any(r => r != 0));

                string kind;
                if (isProp) { kind = "u"; }
                else if (d.Kinds.Count == 1) { kind = d.Kinds.First(); }
                else { kind = (hasRace && !hasUni) ? "r" : "u"; } // both models, or none: follow the textures
                if (d.Kinds.Count > 1)
                {
                    warnings.Add(new Warning("info", tag + ": both _u and _r models - using _" + kind
                        + ", which has matching textures. Delete the other model to use that one instead."));
                }

                // when both models exist the textures that don't fit belong to the other one
                Dictionary<int, HashSet<int>> letters = d.Textures;
                if (d.Kinds.Count > 1)
                {
                    letters = letters.Where(kv => kv.Value.Any(r => Fits(r, kind, isProp)))
                                     .ToDictionary(kv => kv.Key, kv => kv.Value);
                }

                if (!d.HasModel)
                {
                    texturesOnly.Add(num);
                    warnings.Add(new Warning("error", tag + ": textures found but no model (.ydd)."));
                }

                ResolvedDrawable row = new ResolvedDrawable
                {
                    Index = num,
                    PropMask = kind == "u" ? PropMaskUniversal : PropMaskRace,
                    Alternatives = d.Alternatives,
                    HasCloth = d.HasCloth
                };

                if (letters.Count > 0)
                {
                    int texCount = Math.Min(letters.Keys.Max() + 1, MaxTextures);
                    List<int> missing = Enumerable.Range(0, texCount).Where(i => !letters.ContainsKey(i)).ToList();
                    if (missing.Count > 0)
                    {
                        warnings.Add(new Warning("error", tag + ": missing texture " + Letters(missing)
                            + " (have " + Letters(letters.Keys) + ")."));
                    }

                    if (isProp)
                    {
                        row.TexIds = Enumerable.Range(0, texCount).ToList();
                    }
                    else
                    {
                        Dictionary<int, int> picked = letters.ToDictionary(kv => kv.Key, kv => Pick(kv.Value, kind, isProp, pedRace));
                        int fill = picked.Values.GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;
                        row.TexIds = Enumerable.Range(0, texCount)
                            .Select(i => picked.ContainsKey(i) ? picked[i] : fill).ToList();
                    }
                }
                else
                {
                    modelsOnly.Add(num);
                    int texId = (kind == "u" || isProp) ? 0 : pedRace;
                    row.TexIds = new List<int> { texId };
                    string suffix = isProp ? "" : "_" + Races[texId];
                    warnings.Add(new Warning("warn", tag + ": no texture file - fine if its textures are embedded in the .ydd, "
                        + "otherwise add " + label + "_diff_" + num.ToString("D3") + "_a" + suffix + ".ytd."));
                }

                if (!isProp && d.HasModel && d.Kinds.Count == 1 && letters.Count > 0)
                {
                    if (kind == "u" && row.TexIds.Any(t => t != 0))
                    {
                        warnings.Add(new Warning("info", tag + ": _u model with race textures - universal models normally use _uni."));
                    }
                    if (kind == "r" && row.TexIds.Any(t => t == 0))
                    {
                        warnings.Add(new Warning("info", tag + ": _r model with _uni textures - race models normally use _whi/_bla/..."));
                    }
                }

                rows.Add(row);
            }

            // a model with no texture right next to a texture with no model is usually
            // one file off by one, e.g. accs_001_u.ydd + accs_diff_000_a_uni.ytd
            foreach (int texNum in texturesOnly)
            {
                foreach (int modelNum in new[] { texNum + 1, texNum - 1 })
                {
                    if (modelsOnly.Remove(modelNum))
                    {
                        warnings.Add(new Warning("error", label + ": the texture numbered " + texNum.ToString("D3")
                            + " probably belongs to model " + modelNum.ToString("D3") + " - rename "
                            + label + "_diff_" + texNum.ToString("D3") + "_* to " + label + "_diff_" + modelNum.ToString("D3") + "_*."));
                        break;
                    }
                }
            }

            return rows;
        }

        private static bool Fits(int race, string kind, bool isProp)
        {
            return isProp || ((race == 0) == (kind == "u"));
        }

        private static int Pick(HashSet<int> races, string kind, bool isProp, int pedRace)
        {
            List<int> pool = races.Where(r => Fits(r, kind, isProp)).ToList();
            if (pool.Count == 0)
            {
                pool = races.ToList();
            }
            return pool.Contains(pedRace) ? pedRace : pool.Min();
        }

        private static string Letters(IEnumerable<int> indices)
        {
            return string.Join(", ", indices.OrderBy(i => i).Select(i => XMLHandler.Number2String(i, false)));
        }

        #endregion
    }
}
