using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace YMTEditor
{
    /// <summary>
    /// Batch renaming with a live preview. Every rule is optional and nothing touches the
    /// disk until "Rename the files" is pressed, with clashes shown in the preview first.
    /// </summary>
    public partial class BatchRenameWindow : Window
    {
        private string _folder;
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
        }

        private void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = FolderPicker.Pick(this, "Pick the folder with the files to rename", _folder);
            if (!string.IsNullOrEmpty(folder))
            {
                SetFolder(folder);
            }
        }

        private void SetFolder(string folder)
        {
            _folder = folder;
            folderBox.Text = folder;

            //offer the peds that are actually in there as the rename starting point
            List<string> peds = PedFileRenamer.PedNames(folder);
            pedNameFrom.ItemsSource = peds;
            if (peds.Count > 0)
            {
                pedNameFrom.SelectedIndex = 0;
            }
            UpdatePreview();
        }

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
            if (string.IsNullOrEmpty(_folder))
            {
                status.Text = "Pick a folder to start.";
                applyButton.IsEnabled = false;
                return;
            }

            try
            {
                _preview = PedFileRenamer.Build(_folder, ReadOptions());
            }
            catch (Exception ex)
            {
                previewList.ItemsSource = null;
                status.Text = "Can't read that folder: " + ex.Message;
                applyButton.IsEnabled = false;
                return;
            }

            previewList.ItemsSource = _preview.Changes.Select(i => i.ToString()).ToList();

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
            if (MessageBox.Show(this, "Rename " + count + " file(s) in\n" + _folder + "?\n\nThis changes the files on disk.",
                    "Batch rename", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                WriteLog(_preview);
                int done = PedFileRenamer.ApplyRenames(_preview);
                Renamed = Renamed || done > 0;
                MessageBox.Show(this, "Renamed " + done + " file(s).", "Batch rename",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Renaming stopped:\n\n" + ex.Message
                    + "\n\nSome files may be left with a .renaming extension in\n" + _folder,
                    "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdatePreview();
        }

        //what was renamed, so it can be undone by hand if needed
        private void WriteLog(PedFileRenamer.Preview preview)
        {
            string log = Path.Combine(_folder, "rename-log.txt");
            using (StreamWriter writer = new StreamWriter(log, true))
            {
                writer.WriteLine("# YMTEditor batch rename on " + DateTime.Now);
                foreach (PedFileRenamer.Item item in preview.Changes.Where(i => string.IsNullOrEmpty(i.Problem)))
                {
                    writer.WriteLine(item.OldName + "  ->  " + item.NewName);
                }
                writer.WriteLine();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
