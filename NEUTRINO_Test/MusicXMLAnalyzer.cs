using MusicXml;
using MusicXml.Domain;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace NEUTRINO_Test;

internal class MusicXMLAnalyzer
{
    public static MusicXmlPhrase Analyzer(string path)
    {
        // var score = MusicXml.MusicXmlParser.GetScore(path);

        var items = new List<MusicXmlPhrase.Frame>(/* 要素数を指定する */);

        // var xdoc = XDocument.Load(path);

        MusicXmlObject score;

        using (var fs = File.OpenRead(path))
        {
            var xml = new XmlSerializer(typeof(MusicXmlObject));

            using var reader = XmlReader.Create(fs, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
            });

            score = xml.Deserialize(reader) as MusicXmlObject ?? throw new NotSupportedException();
        };

        // second
        decimal currentTime = 0;
        const int Unit = 5;
        MusicXmlPhrase.Frame tempFrame = null;

        foreach (var part in score.Parts)
        {
            int tempo = -1;
            float division = -1;
            decimal tick = -1;
            decimal unit = -1;
            // 4部音符あたりの時間
            decimal timePerQuarter = -1;
            Dictionary<int, int>? keys = null;

            static int GetCode(Dictionary<int, int>? keys, int octave, string step)
            {
                int timble = Map[step];
                if (keys?.TryGetValue(timble, out int correction) ?? false)
                {
                    timble = correction;
                }

                return (octave * 12) + timble + 13;
            }

            foreach (var measure in part.Measures)
            {

                var direction = measure.Direction;
                if (direction is { Sound: not null })
                {
                    tempo = direction.Sound.Tempo;
                    tick = 60 / (decimal)tempo;
                }

                var attributes = measure.Attributes;
                if (attributes is not null)
                {
                    division = attributes.Divisions;
                    unit = 1 / (decimal)division;
                    timePerQuarter = unit * tick * 1000;

                    var fifth = attributes.Key.Fifths;
                    if (attributes.Key.Fifths == 0)
                    {
                        keys = null;
                    }
                    else
                    {
                        if (!Keys.TryGetValue(fifth, out keys))
                        {
                            Debug.WriteLine(attributes.Key.Fifths);
                        }
                    }
                }

                var notes = measure.Notes;
                if (notes is not null)
                {
                    foreach (var note in notes)
                    {
                        var duration = note.Duration * timePerQuarter;
                        var pitch = note.Pitch;

                        try
                        {
                            if (note.Rest is not null)
                            {
                                continue;
                            }
                            else if (pitch is not null)
                            {
                                var code = GetCode(keys, pitch.Octave, pitch.Step);

                                var tie = note.Tie;
                                if (tie is not null)
                                {
                                    if (tie.Type == "start")
                                    {
                                        tempFrame = new MusicXmlPhrase.Frame(
                                            (int)(currentTime / Unit),
                                            (int)((currentTime + duration) / Unit),
                                            note.Lyric.Text, code);
                                    }
                                    else if (tie.Type == "stop" && tempFrame is not null)
                                    {
                                        tempFrame.EndFrame = (int)((currentTime + duration) / Unit);
                                        items.Add(tempFrame);
                                        //tempFrame = new MusicXmlPhrase.Frame(
                                        //    (int)(currentTime / Unit),
                                        //    (int)((currentTime + duration) / Unit), null, code);
                                    }
                                    else
                                    {
                                        Debug.WriteLine(note);
                                    }
                                }
                                else
                                {
                                    items.Add(new MusicXmlPhrase.Frame(
                                        (int)(currentTime / Unit),
                                        (int)((currentTime + duration) / Unit),
                                        note.Lyric.Text, code));
                                }
                            }
                            else
                            {
                                Debug.WriteLine(note);
                            }
                        }
                        finally
                        {
                            currentTime += duration;
                        }
                    }
                }
            }
        }

        return new MusicXmlPhrase(items);
    }

    private const int KeyCodeC = 0;
    private const int KeyCodeCSharp = 1;
    private const int KeyCodeD = 2;
    private const int KeyCodeDSharp = 3;
    private const int KeyCodeE = 4;
    private const int KeyCodeF = 5;
    private const int KeyCodeFSharp = 6;
    private const int KeyCodeG = 7;
    private const int KeyCodeGSharp = 8;
    private const int KeyCodeA = 9;
    private const int KeyCodeASharp = 10;
    private const int KeyCodeB = 11;

    private static Dictionary<string, int> Map = new()
    {
        ["C"] = KeyCodeC,
        ["D"] = KeyCodeD,
        ["E"] = KeyCodeE,
        ["F"] = KeyCodeF,
        ["G"] = KeyCodeG,
        ["A"] = KeyCodeA,
        ["B"] = KeyCodeB,
    };

    private static Dictionary<int, Dictionary<int, int>> Keys = new()
    {
        [-6] = new()
        {
            [KeyCodeC] = KeyCodeB,
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeD] = KeyCodeCSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
            [KeyCodeG] = KeyCodeFSharp,
        },
        [-5] = new()
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeD] = KeyCodeCSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
            [KeyCodeG] = KeyCodeFSharp,
        },
        [-4] = new()
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeD] = KeyCodeCSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
        },
        [-3] = new()
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
        },
        [-2] = new()
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeB] = KeyCodeASharp,
        },
        [-1] = new()
        {
            [KeyCodeB] = KeyCodeASharp,
        },
        [1] = new()
        {
            [KeyCodeF] = KeyCodeFSharp,
        },
        [2] = new()
        {
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeC] = KeyCodeCSharp,
        },
        [3] = new()
        {
            [KeyCodeG] = KeyCodeGSharp,
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeC] = KeyCodeCSharp,
        },
        [4] = new()
        {
            [KeyCodeG] = KeyCodeGSharp,
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeD] = KeyCodeDSharp,
            [KeyCodeC] = KeyCodeCSharp,
        },
        [5] = new()
        {
            [KeyCodeG] = KeyCodeGSharp,
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeD] = KeyCodeDSharp,
            [KeyCodeC] = KeyCodeCSharp,
            [KeyCodeA] = KeyCodeASharp,
        },
    };
}
