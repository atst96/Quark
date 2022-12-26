using System.Windows;

namespace Quark.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var w = new PreferenceWindow() { Owner = this };
        w.ShowDialog();
    }
}
