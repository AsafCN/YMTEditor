using System.Linq;
using System.Windows;

namespace YMTEditor
{
    /// <summary>
    /// Shows exactly which files sorting a ped folder would rename. Nothing is touched
    /// until "Rename the files" is pressed here.
    /// </summary>
    public partial class RenumberWindow : Window
    {
        public bool Confirmed;

        public RenumberWindow(PedFolderScanner.RenumberPlan plan)
        {
            InitializeComponent();

            headline.Text = plan.Renames.Count == 0
                ? "Nothing to sort - the numbering has no gaps."
                : plan.Renames.Count + " file(s) will be renamed to close the gaps:";

            folderLine.Text = "In: " + plan.Directory
                + (plan.OtherFolders > 0
                    ? "   (" + plan.OtherFolders + " other folder(s) hold files with the same names and are left alone)"
                    : "");

            if (plan.Summary.Count > 0)
            {
                folderLine.Text += "\n" + string.Join("\n", plan.Summary.ToArray());
            }

            if (plan.Problems.Count > 0)
            {
                problemsBox.Visibility = Visibility.Visible;
                problems.Text = string.Join("\n", plan.Problems.ToArray());
            }

            renameList.ItemsSource = plan.Renames.Select(r => r.ToString()).ToList();
            applyButton.IsEnabled = plan.Renames.Count > 0;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
