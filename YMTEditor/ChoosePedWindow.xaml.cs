using System.Collections.Generic;
using System.Windows;

namespace YMTEditor
{
    /// <summary>
    /// Asked for when a scanned folder holds files for more than one ped,
    /// since each name^ prefix belongs to its own ymt.
    /// </summary>
    public partial class ChoosePedWindow : Window
    {
        public PedFolderScanner.ScanPed Chosen;

        public ChoosePedWindow(List<PedFolderScanner.ScanPed> peds, string actionLabel = "Build")
        {
            InitializeComponent();
            pedList.ItemsSource = peds;
            pedList.SelectedIndex = 0;
            confirmButton.Content = actionLabel;
        }

        private void ButtonBuild_Click(object sender, RoutedEventArgs e)
        {
            Chosen = pedList.SelectedItem as PedFolderScanner.ScanPed;
            DialogResult = Chosen != null;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Chosen = null;
            Close();
        }
    }
}
