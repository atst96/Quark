using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NEUTRINO_Test;

internal class SoundFileAnalyzer
{
    public static SoundAnalyzeResult Analyze(
        string f0Path, string mgcPath, string timingPath)
    {
        double[] pitches;
        double[] volumes;

        // F0(音程)の解析
        using (var fs = File.OpenRead(f0Path))
        {
            const int F0DataSize = sizeof(double);
            var temp = (Span<byte>)new byte[F0DataSize];
            int i = 0;

            pitches = new double[fs.Length / F0DataSize];

            while (fs.Read(temp) == F0DataSize)
            {
                pitches[i++] = BitConverter.ToDouble(temp);
            }
        }

        // MGCの解析
        using (var fs = File.OpenRead(mgcPath))
        {
            const int MGCDataSize = sizeof(double) * 60;
            var temp = (Span<byte>)new byte[MGCDataSize];
            int count = 0;

            volumes = new double[fs.Length / MGCDataSize];

            while (fs.Read(temp) == MGCDataSize)
            {
                volumes[count++] = BitConverter.ToDouble(temp[0..8]);
            }
        }

        // タイミングの解析
        var timings = new List<TimingInfo>();
        using (var reader = new StreamReader(timingPath, Encoding.UTF8))
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var args = line.Split(' ');

                var beginTime = long.Parse(args[0]);
                var endTime = long.Parse(args[1]);
                var lyrics = args[2];

                timings.Add(new TimingInfo(beginTime, endTime, lyrics));
            }
        }

        return new(pitches.Length, pitches, volumes, timings.ToArray());
    }
}