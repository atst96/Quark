using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Quark.Views;

public partial class MusicXMLImportWindow : Window
{
    public MusicXMLImportWindow()
    {
        this.InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // HACK: コードビハインドにフォーカス処理を記述しないようにしたい
        this.PART_ProjectNameTextBox.Focus();
    }
}
