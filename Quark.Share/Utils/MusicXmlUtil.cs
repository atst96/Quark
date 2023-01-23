using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml;
using Quark.Models.MusicXML;
using static Quark.Models.MusicXML.MeasureItemTypes;

namespace Quark.Utils;

/// <summary>
/// MusicXMLファイルの処理に関するUtil
/// </summary>
public static class MusicXmlUtil
{
    const double DefaultTempo = 100;

    const int Unit = 5;

    public static MusicXmlPhrase Parse(string xml)
    {
    public static PartScore Parse(string xml)
    {
        var score = ParseMusicXml(xml);

        var _notes = new LinkedList<MusicXmlPhrase.Frame>();
        var tempos = new LinkedList<TempoInfo>();
        var timeSignatures = new LinkedList<TimeSignature>();

        // second
        decimal currentTime = 0;
        MusicXmlPhrase.Frame? tiedNote = null;

        foreach (var part in score.Parts)
        {
            double tempo = DefaultTempo;
            float division = 1;
            decimal tick = (decimal)(60 / DefaultTempo);
            decimal unit = 1;

            // 4部音符あたりの時間
            decimal timePerQuarter = unit * tick * 1000;
            Dictionary<int, int>? keys = null;

            if (part.Measures.Count <= 0)
            {
                continue;
            }

            foreach (var measure in part.Measures)
            {
                var attributes = measure.Attributes;
                if (attributes is not null)
                {
                    if (attributes.Divisions is not null)
                    {
                        division = attributes.Divisions.Value;
                    unit = 1 / (decimal)division;
                    timePerQuarter = unit * tick * 1000;
                    }

                    var time = attributes.Time;
                    if (time is not null)
                    {
                        timeSignatures.AddLast(new TimeSignature(currentTime, time.Beats, time.BeatType));
                    }

                    var fifth = attributes.Key?.Fifths;
                    if (fifth is null || fifth == 0)
                    {
                        keys = null;
                    }
                    else
                    {
                        if (!FifthCodeRelations.TryGetValue(fifth.Value, out keys))
                        {
                            Debug.WriteLine(attributes.Key!.Fifths);
                            Debugger.Break();
                        }
                    }
                }

                var notes = measure.Items;
                if (notes is not null)
                {
                    foreach (var item in notes)
                    {
                        if (item is Direction direction)
                        {
                            if (direction is { Sound: not null })
                            {
                                tempo = direction.Sound.Tempo;
                                tick = 60 / (decimal)tempo;
                                timePerQuarter = unit * tick * 1000;

                                var metronome = direction.DirectionType.Metronome;
                                tempos.AddLast(new TempoInfo(true, currentTime, tempo, metronome.BeatUnit, metronome.PerMinute));
                            }
                        }
                        else if (item is Note note)
                        {
                            var duration = note.Duration * timePerQuarter;
                            var pitch = note.Pitch;

                            try
                            {
                                if (note.Rest is not null)
                                {
                                    // 休符
                                    continue;
                                }
                                else if (pitch is not null)
                                {
                                    var ties = note.Tie;
                                    if (ties is null || ties.Count == 0)
                                    {
                                        // タイ以外の音符
                                        _notes.AddLast(CreateFrameInfo(note, currentTime, currentTime + duration, keys));
                                    }
                                    else
                                    {
                                        if (ties.All(t => t.Type == StartStop.Start))
                                        {
                                            // タイ記号の始め
                                            tiedNote = CreateFrameInfo(note, currentTime, currentTime + duration, keys);
                                        }
                                        else if (ties.Any(t => t.Type == StartStop.Stop) && tiedNote is not null)
                                        {
                                            if (ties.Any(t => t.Type == StartStop.Start))
                                            {
                                                // pass
                                                // 中間のタイは stop&startのtypeが含まれるので無視する
                                            }
                                            else
                                        {
                                            // タイ記号の終わり
                                            tiedNote.SetEndFrame((int)((currentTime + duration) / Unit));
                                            tiedNote.SetBreath(GetIsBreath(note));
                                            _notes.AddLast(tiedNote);
                                        }
                                        }
                                        else
                                        {
                                            Debug.WriteLine(note);
                                            // Debugger.Break();
                                        }
                                    }
                                }
                                else
                                {
                                    // 音符じゃない場合？
                                    Debug.WriteLine(note);
                                    Debugger.Break();
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
        }

        // 先頭のテンポ情報がなければデフォルトを差し込む
        if (tempos is not { Count: > 0, First.Value.Frame: 0 })
        {
            tempos.AddFirst(new TempoInfo(false, 0, DefaultTempo, "quarter", DefaultTempo));
            }

        // 先頭の小節情報がなければデフォルトを差し込む
        if (timeSignatures is not { Count: > 0, First.Value.Frame: 0 })
        {
            timeSignatures.AddLast(new TimeSignature(currentTime, 4, 4));
        }

        return new(tempos, timeSignatures, _notes);
    }

    private static XmlSerializer _serializer = new XmlSerializer(typeof(MusicXmlObject));

    private static MusicXmlObject ParseMusicXml(string xml)
    {
        using (var sr = new StringReader(xml))
        using (var reader = XmlReader.Create(sr, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
        {

            return _serializer.Deserialize(reader) as MusicXmlObject ?? throw new NotSupportedException();
        }
    }

    static int GetCode(Dictionary<int, int>? keys, Pitch pitch)
    {
        int timble = KeyCodeForStep[pitch.Step];
        if (keys?.TryGetValue(timble, out int correction) ?? false)
        {
            timble = correction;
        }

        return (pitch.Octave * 12) + timble + 13;
    }

    private static bool GetIsBreath(Note note)
        => note.Notations?.Articulations?.BreathMark is not null;

    private static MusicXmlPhrase.Frame CreateFrameInfo(
        Note note, decimal startTime, decimal endTime, Dictionary<int, int>? keys)
        => new MusicXmlPhrase.Frame(
            (int)(startTime / Unit),
            (int)(endTime / Unit),
            note.Lyric.Text, GetCode(keys, note.Pitch), GetIsBreath(note));

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

    private static Dictionary<string, int> KeyCodeForStep = new()
    {
        ["C"] = KeyCodeC,
        ["D"] = KeyCodeD,
        ["E"] = KeyCodeE,
        ["F"] = KeyCodeF,
        ["G"] = KeyCodeG,
        ["A"] = KeyCodeA,
        ["B"] = KeyCodeB,
    };

    private static Dictionary<int, Dictionary<int, int>> FifthCodeRelations = new()
    {
        [-6] = new() // ♭6つ
        {
            [KeyCodeC] = KeyCodeB,
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeD] = KeyCodeCSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
            [KeyCodeG] = KeyCodeFSharp,
        },
        [-5] = new() // ♭5つ
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeD] = KeyCodeCSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
            [KeyCodeG] = KeyCodeFSharp,
        },
        [-4] = new() // ♭4つ
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeD] = KeyCodeCSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
        },
        [-3] = new() // ♭3つ
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeB] = KeyCodeASharp,
            [KeyCodeA] = KeyCodeGSharp,
        },
        [-2] = new() // ♭2つ
        {
            [KeyCodeE] = KeyCodeDSharp,
            [KeyCodeB] = KeyCodeASharp,
        },
        [-1] = new() // ♭1つ
        {
            [KeyCodeB] = KeyCodeASharp,
        },
        [1] = new() // ♯1つ
        {
            [KeyCodeF] = KeyCodeFSharp,
        },
        [2] = new() // ♯2つ
        {
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeC] = KeyCodeCSharp,
        },
        [3] = new()// ♯3つ
        {
            [KeyCodeG] = KeyCodeGSharp,
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeC] = KeyCodeCSharp,
        },
        [4] = new()// ♯4つ
        {
            [KeyCodeG] = KeyCodeGSharp,
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeD] = KeyCodeDSharp,
            [KeyCodeC] = KeyCodeCSharp,
        },
        [5] = new()// ♯5つ
        {
            [KeyCodeG] = KeyCodeGSharp,
            [KeyCodeF] = KeyCodeFSharp,
            [KeyCodeD] = KeyCodeDSharp,
            [KeyCodeC] = KeyCodeCSharp,
            [KeyCodeA] = KeyCodeASharp,
        },
    };
}
