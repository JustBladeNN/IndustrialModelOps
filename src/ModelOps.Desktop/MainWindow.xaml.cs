using System.Windows;

namespace ModelOps.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new ModelOpsApiClient());
    }
}
