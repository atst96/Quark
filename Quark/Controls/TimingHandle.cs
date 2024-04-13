using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Quark.Controls.Editing;
using Quark.Models.Neutrino;

namespace Quark.Controls;

public class TimingHandler : TemplatedControl
{
    public TimingInfo Timing { get; }

    public string Phoneme
    {
        get => this.GetValue(PhonemeProperty);
        set => this.SetValue(PhonemeProperty, value);
    }

    public static readonly StyledProperty<string> PhonemeProperty = AvaloniaProperty.Register<TimingHandler, string>(nameof(Phoneme), defaultValue: string.Empty);

    public int AvoidLevel
    {
        get => this.GetValue(AvoidLevelProperty);
        set => this.SetValue(AvoidLevelProperty, value);
    }

    public static readonly StyledProperty<int> AvoidLevelProperty = AvaloniaProperty.Register<TimingHandler, int>(nameof(AvoidLevel), defaultValue: 0);

    public TimingHandler(TimingInfo timing)
    {
        this.Timing = timing;
    }
}
