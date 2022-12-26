using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Quark.Views;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void OnButtonClicked(object sender, RoutedEventArgs e)
    {
        new PreferenceWindow()
        {
            // DataContext = new PreferenceWindowViewModel();
        }.ShowDialog(this);
    }
}
