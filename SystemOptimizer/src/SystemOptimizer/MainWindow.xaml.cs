using System.Windows;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
