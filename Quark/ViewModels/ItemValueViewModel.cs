using CommunityToolkit.Mvvm.ComponentModel;

namespace Quark.ViewModels;

public class ItemValueViewModel<TValue>(TValue Value, string Description) : ObservableObject
{
    /// <summary>値</summary>
    public TValue Value { get; } = Value;

    /// <summary>表示名</summary>
    public string Description { get; } = Description;

    /// <inheritdoc/>
    public override string ToString()
        => $"Value={this.Value}, Description={this.Description}";
}
