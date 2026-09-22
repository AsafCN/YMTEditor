using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace YMTEditor
{
    /// <summary>
    /// Batch renaming with a live preview. Files come from a folder, from the file picker,
    /// or by dropping them on the window, so it works as a plain renamer as well as a ped
    /// aware one. Every rule is optional and nothing touches the disk until "Rename the
    /// files" is pressed, with clashes shown in the preview first.
    /// </summary>
    public partial class BatchRenameWindow : Window
    {
        private string _folder;            // set when a whole folder is the source
        private List<string> _files;       // set when dropped/picked files are the source
        private PedFileRenamer.Preview _preview;
        private bool _loaded;

        /// <summary>Set when files were renamed, so the caller can reload what it shows.</summary>
        public bool Renamed;

        public BatchRenameWindow(string startFolder)
        {
            InitializeComponent();

            string[] slots = Enum.GetNames(typeof(YMTTypes.ComponentNumbers))
                .Concat(Enum.GetNames(typeof(YMTTypes.PropNumbers))).ToArray();
            slotFrom.ItemsSource = slots;
            slotTo.ItemsSource = slots;
            numberSlot.ItemsSource = new[] { "(every slot)" }.Concat(slots).ToArray();
            numberSlot.SelectedIndex = 0;
            slotFrom.SelectedIndex = 11;  // jbib, the usual one to move
            slotTo.SelectedIndex = 9;     // task

            _loaded = true;
            if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder))
            {
                SetFolder(startFolder);
            }
            else
            {
                UpdatePreview();
            }
        }

        #region where the files come from

        private void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = FolderPicker.Pick(this, "Pick the folder with the files to rename", CurrentFolder());
            if (!string.IsNullOrEmpty(folder))
            {
                SetFolder(folder);
            }
        }

        private void PickFiles_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pick the files to rename",
                Multiselect = true,
                Filter = "All files (*.*)|*.*",
                InitialDirectory = CurrentFolder() ?? "",
            };
            if (dialog.ShowDialog(this) == true)
            {
                SetFiles(dialog.FileNames);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }
            e.Handled = true;

            string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            //one folder on its own behaves like picking it, so "include sub-folders" keeps working
            if (dropped.Length == 1 && Directory.Exists(dropped[0]))
            {
                SetFolder(dropped[0]);
                return;
            }

            List<string> files = PedFileRenamer.ExpandDrop(dropped, recursiveCheck.IsChecked == true);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "Nothing to rename in what was dropped.", "Batch rename",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetFiles(files);
        }

        private void SetFolder(string folder)
        {
            _folder = folder;
            _files = null;
            folderBox.Text = folder;
            RefreshPedNames();
            UpdatePreview();
        }

        private void SetFiles(IEnumerable<string> files)
        {
            _files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _folder = null;

            int folders = _files.Select(Path.GetDirectoryName)
                                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            folderBox.Text = _files.Count + " file(s)"
                + (folders == 1 ? " in " + Path.GetDirectoryName(_files[0]) : " from " + folders + " folders");

            RefreshPedNames();
            UpdatePreview();
        }

        /// <summary>The folder to start dialogs in.</summary>
        private string CurrentFolder()
        {
            if (!string.IsNullOrEmpty(_folder))
            {
                return _folder;
            }
            return _files != null && _files.Count > 0 ? Path.GetDirectoryName(_files[0]) : null;
        }

        //offer the peds that are actually in the set as the rename starting point
        private void RefreshPedNames()
        {
            List<string> paths;
            try
            {
                paths = _files ?? Directory.GetFiles(_folder, "*",
                    recursiveCheck.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception)
            {
                paths = new List<string>();
            }

            string previous = pedNameFrom.Text;
            List<string> peds = PedFileRenamer.PedNames(paths);
            pedNameFrom.ItemsSource = peds;
            if (peds.Contains(previous))
            {
                pedNameFrom.Text = previous;
            }
            else if (peds.Count > 0)
            {
                pedNameFrom.SelectedIndex = 0;
            }
        }

        #endregion

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                UpdatePreview();
            }
        }

        private PedFileRenamer.Options ReadOptions()
        {
            int offset;
            int.TryParse(numberOffset.Text, out offset);

            return new PedFileRenamer.Options
            {
                Recursive = recursiveCheck.IsChecked == true,
                ClothingOnly = clothingOnlyCheck.IsChecked == true,

                PedNameEnabled = pedNameCheck.IsChecked == true,
                PedNameFrom = pedNameFrom.Text,
                PedNameTo = pedNameTo.Text,

                SlotEnabled = slotCheck.IsChecked == true,
                SlotFrom = Convert.ToString(slotFrom.SelectedItem),
                SlotTo = Convert.ToString(slotTo.SelectedItem),

                NumberEnabled = numberCheck.IsChecked == true,
                NumberOffset = offset,
                NumberSlot = numberSlot.SelectedIndex <= 0 ? "" : Convert.ToString(numberSlot.SelectedItem),

                ReplaceEnabled = replaceCheck.IsChecked == true,
                ReplaceFind = replaceFind.Text,
                ReplaceWith = replaceWith.Text,

                RemoveEnabled = removeCheck.IsChecked == true,
                RemoveText = removeText.Text,

                AddEnabled = addCheck.IsChecked == true,
                AddPrefix = addPrefix.Text,
                AddSuffix = addSuffix.Text,

                LowerCaseEnabled = lowerCheck.IsChecked == true,
            };
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(_folder) && (_files == null || _files.Count == 0))
            {
                previewList.ItemsSource = null;
                dropHint.Visibility = Visibility.Visible;
                status.Text = "Drop files or a folder here, or use the buttons above.";
                applyButton.IsEnabled = false;
                return;
            }

            try
            {
                PedFileRenamer.Options options = ReadOptions();
                _preview = _files != null
                    ? PedFileRenamer.Build(_files, options)
                    : PedFileRenamer.Build(_folder, options);
            }
            catch (Exception ex)
            {
                previewList.ItemsSource = null;
                status.Text = "Can't read that: " + ex.Message;
                applyButton.IsEnabled = false;
                return;
            }

            previewList.ItemsSource = _preview.Changes.Select(i => i.ToString()).ToList();
            dropHint.Visibility = _preview.ChangedCount == 0 ? Visibility.Visible : Visibility.Collapsed;

            int changed = _preview.ChangedCount;
            int problems = _preview.ProblemCount;
            status.Text = changed == 0
                ? _preview.Items.Count + " file(s) looked at, nothing to rename yet."
                : changed + " of " + _preview.Items.Count + " file(s) would be renamed"
                  + (problems > 0 ? ", " + problems + " with a problem - those are skipped." : ".");

            applyButton.IsEnabled = _preview.Changes.Any(i => string.IsNullOrEmpty(i.Problem));
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_preview == null)
            {
                return;
            }

            int count = _preview.Changes.Count(i => string.IsNullOrEmpty(i.Problem));
            if (MessageBox.Show(this, "Rename " + count + " file(s)?\n\nThis changes the files on disk.",
                    "Batch rename", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                WriteLogs(_preview);
                int done = PedFileRenamer.ApplyRenames(_preview);
                Renamed = Renamed || done > 0;

                //the set was named before the rename, so follow the files to their new names
                if (_files != null)
                {
                    Dictionary<string, string> moved = _preview.Changes
                        .Where(i => string.IsNullOrEmpty(i.Problem))
                        .ToDictionary(i => i.Path,
                                      i => Path.Combine(Path.GetDirectoryName(i.Path), i.NewName),
                                      StringComparer.OrdinalIgnoreCase);
                    _files = _files.Select(f => moved.ContainsKey(f) ? moved[f] : f).ToList();
                }

                MessageBox.Show(this, "Renamed " + done + " file(s).", "Batch rename",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Renaming stopped:\n\n" + ex.Message
                    + "\n\nSome files may be left with a .renaming extension.",
                    "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RefreshPedNames();
            UpdatePreview();
        }

        //what was renamed, written into each folder that changed, so it can be undone by hand
        private void WriteLogs(PedFileRenamer.Preview preview)
        {
            foreach (var folder in preview.Changes.Where(i => string.IsNullOrEmpty(i.Problem))
                                                  .GroupBy(i => Path.GetDirectoryName(i.Path)))
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(Path.Combine(folder.Key, "rename-log.txt"), true))
                    {
                        writer.WriteLine("# YMTEditor batch rename on " + DateTime.Now);
                        foreach (PedFileRenamer.Item item in folder)
                        {
                            writer.WriteLine(item.OldName + "  ->  " + item.NewName);
                        }
                        writer.WriteLine();
                    }
                }
                catch (Exception)
                {
                    //a read-only folder shouldn't stop the renaming itself
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
