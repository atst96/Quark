using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Quark.Models;
using SkiaSharp;

namespace Quark.Controls;

public class EditorNote : TemplatedControl
{
    protected override Type StyleKeyOverride => typeof(EditorNote);

    /// <summary>要素に日も付く音符</summary>
    public ScoreNote Note { get; }

    /// <summary>歌詞</summary>
    public string? Lyrics
    {
        get => this.GetValue(LyricsProperty);
        set => this.SetValue(LyricsProperty, value);
    }

    /// <summary><see cref="Lyrics"/>のプロパティ</summary>
    public static readonly StyledProperty<string?> LyricsProperty = AvaloniaProperty.Register<EditorNote, string?>(nameof(Lyrics), defaultValue: null);

    /// <summary>ブレスマークの有無フラグ</summary>
    public bool HasBreath
    {
        get => this.GetValue(HasBreathProperty);
        set => this.SetValue(HasBreathProperty, value);
    }

    /// <summary><see cref="HasBreath"/>のプロパティ</summary>
    public static readonly StyledProperty<bool> HasBreathProperty = AvaloniaProperty.Register<EditorNote, bool>(nameof(HasBreath), defaultValue: false);

    public EditorNote(ScoreNote note) : base()
    {
        this.Note = note;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var lyrics = this.Lyrics;
        if (!string.IsNullOrEmpty(lyrics))
        {
            var typeface = new Typeface(this.FontFamily);

            // 歌詞の描画
            var text = new FormattedText(lyrics, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, this.FontSize, this.Foreground);

            context.DrawText(text, new Point(2, -text.Baseline));

            if (this.HasBreath)
            {
                const double bressMarkWidth = 11f;
                const double bressMarkHeight = 16f;

                context.DrawGeometry(null, new Pen(this.BorderBrush, 1f), CreateBreathMark(this.Width, 0, bressMarkWidth, bressMarkHeight));
            }
        }
    }

    /// <summary>
    /// ブレスマークのパスを生成する
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="width">描画幅</param>
    /// <param name="height">描画高</param>
    /// <returns></returns>
    private static Geometry CreateBreathMark(double x, double y, double width, double height)
    {
        double halfWidth = (width - 1) / 2;
        double offsetX = y - height;

        return new PathGeometry()
        {
            Figures = [
                new PathFigure()
                {
                    StartPoint = new(x - halfWidth, offsetX),
                    IsClosed = false,
                    Segments = [
                        new LineSegment{ Point = new(x, offsetX + height) },
                        new LineSegment{ Point = new(x + halfWidth, offsetX) },
                    ]
                },

            ]
        };
    }
}
