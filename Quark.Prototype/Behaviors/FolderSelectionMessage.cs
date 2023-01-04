using Livet.Messaging;
using System.Windows;

namespace Quark.Behaviors;

internal class FolderSelectionMessage : ResponsiveInteractionMessage<string?>
{
    /// <summary>
    /// ダイアログの説明文
    /// </summary>
    public string? Description
    {
        get => this.GetValue(DescriptionProperty) as string;
        set => this.SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// <see cref="Description"/>の依存関係プロパティ
    /// </summary>
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(FolderSelectionMessage), new PropertyMetadata(null));


    /// <summary>
    /// 初期ディレクトリ
    /// </summary>
    public string? InitalDirectory
    {
        get => this.GetValue(InitalDirectoryProperty) as string;
        set => this.SetValue(InitalDirectoryProperty, value);
    }

    /// <summary>
    /// <see cref="InitalDirectory"/>の依存関係プロパティ
    /// </summary>
    public static readonly DependencyProperty InitalDirectoryProperty =
        DependencyProperty.Register(nameof(InitalDirectory), typeof(string), typeof(FolderSelectionMessage), new PropertyMetadata(null));

    protected override Freezable CreateInstanceCore() => new FolderSelectionMessage { MessageKey = this.MessageKey };
}
