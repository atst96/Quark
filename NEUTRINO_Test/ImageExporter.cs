using System.Drawing.Imaging;

namespace NEUTRINO_Test;

internal class ImageExporter
{
    public static void Generate(string f0File, string mgcFile, string labFile, string scoreFile, string output)
    {
        var info = SoundFileAnalyzer.Analyze(f0File, mgcFile, labFile);

        long keyCount = 88;
        long lowFrequency = 0;
        double highVolume = 30;
        double lowVolume = -30;

        int keyHeight = 9;
        float frequencyWidth = 2;
        float lineOffset = keyHeight / 2;

        int width = (int)(info.FramesCount * frequencyWidth);
        int height = (int)(keyCount * keyHeight);

        using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);

        var timinguPen = new Pen(Brushes.Blue, 1.0f);
        var splitPen = new Pen(new SolidBrush(Color.FromArgb(230, 230, 230)), 1f);
        var pitchPen = new Pen(Brushes.Red, 1.75f);
        var volumeBrush = new Pen(Brushes.LightSkyBlue, 1.75f);

        using (var g = Graphics.FromImage(bmp))
        {
            // 背景色の描画
            g.FillRectangle(Brushes.White, 0, 0, width, height);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            for (int line = 1; line < keyCount; ++line)
            {
                int y = height - (line * keyHeight);

                // 黒鍵の行を別色で塗りつぶす
                switch (line % 12)
                {
                    case 1:
                    case 3:
                    case 6:
                    case 8:
                    case 10:
                        g.FillRectangle(new SolidBrush(Color.FromArgb(244, 244, 244)), 1, y, width, keyHeight);
                        break;

                    case 11:
                    case 4:
                        g.DrawLine(splitPen, 0, y, width, y);
                        break;
                }
            }

            // タイミングを描画
            var timings = info.Timings;
            for (int idx = 0; idx < timings.Length; ++idx)
            {
                var timing = timings[idx];
                // MEMO: タイミングファイルは1/10,000,000秒単位
                float frame = (float)timing.BeginTime / 50000;

                g.DrawLine(timinguPen, frame * frequencyWidth, 0, frame * frequencyWidth, height);
                g.DrawString(timing.Lyrics, new Font("MS UI Gothic", 14), Brushes.Blue, frame * frequencyWidth + 4, 4f);
            }

            // 楽譜を描画
            var scores = MusicXMLAnalyzer.Analyzer(scoreFile).Frames;
            foreach (var score in scores)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(233, 255, 254)),
                    (int)(score.BeginFrame * frequencyWidth), height - (int)(score.Pitch * keyHeight),
                    (int)((score.EndFrame - score.BeginFrame) * frequencyWidth), keyHeight);

                g.DrawRectangle(new Pen(new SolidBrush(Color.FromArgb(155, 231, 255)), 1.0f),
                    (int)score.BeginFrame * frequencyWidth, height - (int)(score.Pitch * keyHeight),
                    (int)((score.EndFrame - score.BeginFrame) * frequencyWidth), keyHeight - 1);
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            // 音階を描画
            foreach (var item in Parse(info.Pitches, 0.0d))
            {
                int baseIdx = item.Index;
                var values = item.Values;
                var points = new PointF[values.Length];
                for (int idx = 0; idx < values.Length; ++idx)
                {
                    points[idx] = new PointF((idx + baseIdx) * frequencyWidth, height - (float)(FrequencyToScale(values[idx]) * keyHeight) - lineOffset);
                }

                g.DrawLines(pitchPen, points);
            }

            // 音量を描画
            foreach (var item in Parse(info.Volumes, -30.0d))
            {
                double diff = highVolume - lowVolume;
                double half = diff / 2;
                double per = height / diff;

                int baseIdx = item.Index;
                var values = item.Values;
                var points = new PointF[values.Length];
                for (int idx = 0; idx < values.Length; ++idx)
                {
                    points[idx] = new PointF((baseIdx + idx) * frequencyWidth, height - (float)((values[idx] + half) * per) - lineOffset);
                }

                g.DrawLines(volumeBrush, points);
            }
        }

        bmp.Save(output);
    }

    private static double FrequencyToScale(double freqency)
    {
        // http://signalprocess.binarized.work/2019/03/26/convert_frequency_to_cent/
        return 12 * Math.Log2(freqency / 440) + 69;
    }

    public static List<Class1> Parse(IReadOnlyCollection<double> values, double min)
    {
        var items = new List<Class1>(values.Count());

        int idx = 0;

        int tempIdx = 0;
        List<double>? tempItems = null;

        foreach (var value in values)
        {
            try
            {
                if (value <= min)
                {
                    if (tempItems is not null)
                    {
                        if (tempItems.Count > 1)
                        {
                            items.Add(new Class1(tempIdx, tempItems.ToArray()));
                        }
                        tempItems = null;
                    }
                    continue;
                }

                if (tempItems == null)
                {
                    tempItems = new List<double>();
                    tempIdx = idx;
                }
                tempItems.Add(value);
            }
            finally
            {
                ++idx;
            }
        }

        if (tempItems != null)
        {
            items.Add(new Class1(tempIdx, tempItems.ToArray()));
        }

        return items;
    }
}
