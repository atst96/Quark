using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;
using SkiaSharp;

namespace Quark.Controls;

public class PhraseRect : Rectangle
{
    public INeutrinoPhrase Phrase { get; }

    public PhraseRect(INeutrinoPhrase phrase)
    {
        this.Phrase = phrase;
    }

    public void UpdateBackground()
    {
        var background = this.Phrase.Status switch
        {
            PhraseStatus.WaitEstimate => new SolidColorBrush(Color.FromArgb(10, 0, 0, 255)),
            PhraseStatus.EstimateProcessing => new SolidColorBrush(Color.FromArgb(20, 0, 0, 255)),
            PhraseStatus.WaitAudioRender => new SolidColorBrush(Color.FromArgb(10, 255, 0, 0)),
            PhraseStatus.AudioRenderProcessing => new SolidColorBrush(Color.FromArgb(20, 255, 0, 0)),
            _ => null,
        };

        if (this.Fill != background)
            this.Fill = background;
    }
}
