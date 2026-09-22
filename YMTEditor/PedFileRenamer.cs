using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace YMTEditor
{
    /// <summary>
    /// Batch renaming for a ped folder. Beside the plain text operations there are ped
    /// aware ones, which is where the time goes: renaming the ped itself (every
    /// name^..., name_p^..., name.ymt and name.yft at once), moving a component to
    /// another slot, and shifting drawable numbers to merge two peds without collisions.
    ///
    /// Nothing is renamed until a preview has been built and handed back to Apply.
    /// </summary>
    public static class PedFileRenamer
    {
        public class Options
        {
            public bool Recursive = true;
            public bool ClothingOnly;              // .ydd/.ytd/.yld only, so .ymt/.yft/.meta are left alone

            public bool PedNameEnabled;            // ig_old^jbib_000_u.ydd -> ig_new^jbib_000_u.ydd
            public string PedNameFrom = "";
            public string PedNameTo = "";

            public bool SlotEnabled;               // jbib_004_u.ydd -> task_004_u.ydd
            public string SlotFrom = "";
            public string SlotTo = "";

            public bool NumberEnabled;             // 004 -> 009 with an offset of 5
            public int NumberOffset;
            public string NumberSlot = "";         // empty: every slot

            public bool ReplaceEnabled;
            public string ReplaceFind = "";
            public string ReplaceWith = "";
            public bool ReplaceIgnoreCase = true;

            public bool RemoveEnabled;
            public string RemoveText = "";

            public bool AddEnabled;
            public string AddPrefix = "";
            public string AddSuffix = "";          // goes before the extension

            public bool LowerCaseEnabled;
        }

        public class Item
        {
            public string Path;
            public string OldName;
            public string NewName;
            public string Problem;
            public bool Changed { get { return !string.Equals(OldName, NewName, StringComparison.Ordinal); } }
            public override string ToString()
            {
                string line = OldName + "   ->   " + NewName;
                return string.IsNullOrEmpty(Problem) ? line : line + "      [" + Problem + "]";
            }
        }

        public class Preview
        {
            public readonly List<Item> Items = new List<Item>();
            public IEnumerable<Item> Changes { get { return Items.Where(i => i.Changed); } }
            public int ChangedCount { get { return Items.Count(i => i.Changed); } }
            public int ProblemCount { get { return Items.Count(i => !string.IsNullOrEmpty(i.Problem)); } }
        }

        /// <summary>Everything in a folder.</summary>
        public static Preview Build(string folder, Options options)
        {
            return Build(Directory.GetFiles(folder, "*",
                options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly), options);
        }

        /// <summary>A given set of files, which may sit in different folders.</summary>
        public static Preview Build(IEnumerable<string> paths, Options options)
        {
            Preview preview = new Preview();

            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase)
                                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                string name = Path.GetFileName(path);
                string ext = Path.GetExtension(name).ToLowerInvariant();
                if (options.ClothingOnly && ext != ".ydd" && ext != ".ytd" && ext != ".yld")
                {
                    continue;
                }

                preview.Items.Add(new Item { Path = path, OldName = name, NewName = Apply(path, name, options) });
            }

            FlagProblems(preview);
            return preview;
        }

        /// <summary>Dropped paths, with folders expanded into the files inside them.</summary>
        public static List<string> ExpandDrop(IEnumerable<string> dropped, bool recursive)
        {
            List<string> files = new List<string>();
            foreach (string path in dropped)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        files.AddRange(Directory.GetFiles(path, "*",
                            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));
                    }
                    else if (File.Exists(path))
                    {
                        files.Add(path);
                    }
                }
                catch (Exception)
                {
                    //an unreadable folder just contributes nothing
                }
            }
            return files;
        }

        private static string Apply(string path, string name, Options o)
        {
            //the ped aware ones first, since they read the name's structure
            if (o.SlotEnabled && !string.IsNullOrEmpty(o.SlotFrom) && !string.IsNullOrEmpty(o.SlotTo))
            {
                name = ReplaceSlot(path, name, o.SlotFrom, o.SlotTo);
            }
            if (o.NumberEnabled && o.NumberOffset != 0)
            {
                name = ShiftNumber(path, name, o.NumberOffset, o.NumberSlot);
            }
            if (o.PedNameEnabled && !string.IsNullOrEmpty(o.PedNameFrom))
            {
                name = Regex.Replace(name, Regex.Escape(o.PedNameFrom), (o.PedNameTo ?? "").Replace("$", "$$"),
                    RegexOptions.IgnoreCase);
            }

            //then the plain text ones
            if (o.ReplaceEnabled && !string.IsNullOrEmpty(o.ReplaceFind))
            {
                name = Regex.Replace(name, Regex.Escape(o.ReplaceFind), (o.ReplaceWith ?? "").Replace("$", "$$"),
                    o.ReplaceIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            }
            if (o.RemoveEnabled && !string.IsNullOrEmpty(o.RemoveText))
            {
                name = Regex.Replace(name, Regex.Escape(o.RemoveText), "", RegexOptions.IgnoreCase);
            }
            if (o.AddEnabled)
            {
                string stem = Path.GetFileNameWithoutExtension(name);
                string ext = Path.GetExtension(name);
                name = (o.AddPrefix ?? "") + stem + (o.AddSuffix ?? "") + ext;
            }
            if (o.LowerCaseEnabled)
            {
                name = name.ToLowerInvariant();
            }
            return name;
        }

        /// <summary>jbib_004_u.ydd -> task_004_u.ydd, only when the slot is the one asked for.</summary>
        private static string ReplaceSlot(string path, string name, string from, string to)
        {
            PedFolderScanner.ParsedFile parsed = PedFolderScanner.ParseFile(path);
            if (parsed == null)
            {
                return name;
            }
            Group slot = parsed.Match.Groups["slot"];
            if (!string.Equals(slot.Value, from, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
            return name.Remove(slot.Index, slot.Length).Insert(slot.Index, to);
        }

        /// <summary>Moves the drawable number, e.g. +5 turns 004 into 009.</summary>
        private static string ShiftNumber(string path, string name, int offset, string slotFilter)
        {
            PedFolderScanner.ParsedFile parsed = PedFolderScanner.ParseFile(path);
            if (parsed == null)
            {
                return name;
            }
            Group slot = parsed.Match.Groups["slot"];
            if (!string.IsNullOrEmpty(slotFilter)
                && !string.Equals(slot.Value, slotFilter, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            int number = parsed.Number + offset;
            if (number < 0 || number > 999)
            {
                return name; //flagged as a problem by the caller through the unchanged name
            }
            Group num = parsed.Match.Groups["num"];
            return name.Remove(num.Index, num.Length).Insert(num.Index, number.ToString("D3"));
        }

        private static readonly char[] Invalid = Path.GetInvalidFileNameChars();

        private static void FlagProblems(Preview preview)
        {
            foreach (Item item in preview.Items)
            {
                if (string.IsNullOrWhiteSpace(item.NewName) || item.NewName.IndexOfAny(Invalid) >= 0)
                {
                    item.Problem = "not a usable file name";
                }
            }

            //two files landing on one name, per folder
            foreach (var folder in preview.Items.GroupBy(i => Path.GetDirectoryName(i.Path)))
            {
                foreach (var clash in folder.GroupBy(i => i.NewName, StringComparer.OrdinalIgnoreCase)
                                            .Where(g => g.Count() > 1))
                {
                    foreach (Item item in clash)
                    {
                        item.Problem = "two files would get this name";
                    }
                }

                //or landing on a file that is staying put
                HashSet<string> staying = new HashSet<string>(
                    folder.Where(i => !i.Changed).Select(i => i.OldName), StringComparer.OrdinalIgnoreCase);
                HashSet<string> renaming = new HashSet<string>(
                    folder.Where(i => i.Changed).Select(i => i.OldName), StringComparer.OrdinalIgnoreCase);
                HashSet<string> onDisk = new HashSet<string>(
                    Directory.GetFiles(folder.Key).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);

                foreach (Item item in folder.Where(i => i.Changed && string.IsNullOrEmpty(i.Problem)))
                {
                    if (staying.Contains(item.NewName) || (onDisk.Contains(item.NewName) && !renaming.Contains(item.NewName)))
                    {
                        item.Problem = "a file with that name is already there";
                    }
                }
            }
        }

        /// <summary>
        /// Renames everything in the preview that changed and has no problem. Every file
        /// goes through a temporary name first, so names can be swapped or shifted down
        /// without a rename landing on a file that hasn't moved yet.
        /// </summary>
        public static int ApplyRenames(Preview preview)
        {
            List<Item> items = preview.Changes.Where(i => string.IsNullOrEmpty(i.Problem)).ToList();
            List<string> temps = new List<string>();

            foreach (Item item in items)
            {
                string temp = item.Path + ".renaming";
                File.Move(item.Path, temp);
                temps.Add(temp);
            }
            for (int i = 0; i < items.Count; i++)
            {
                File.Move(temps[i], Path.Combine(Path.GetDirectoryName(items[i].Path), items[i].NewName));
            }
            return items.Count;
        }

        /// <summary>The ped names in these files, to offer as the "rename ped" starting point.</summary>
        public static List<string> PedNames(IEnumerable<string> paths)
        {
            try
            {
                return PedFolderScanner.Scan(paths).Peds
                    .Where(p => !string.IsNullOrEmpty(p.Name))
                    .Select(p => p.Name).ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }
}
