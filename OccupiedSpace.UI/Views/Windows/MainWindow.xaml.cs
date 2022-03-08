using OccupiedSpace.Program.Services;
using OccupiedSpace.UI.ViewModels;
using System.Windows;

namespace OccupiedSpace
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new FileSystemItemViewModel(new FileSystemItemService());
        }
    }
}