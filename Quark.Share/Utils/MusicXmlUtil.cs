using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml;
using Quark.Models.MusicXML;
using static Quark.Models.MusicXML.MeasureItemTypes;
using Quark.Models.Scores;
using System.Runtime.CompilerServices;

namespace Quark.Utils;

/// <summary>
/// MusicXMLファイルの処理に関するUtil
/// </summary>
public static class MusicXmlUtil
{
    const double DefaultTempo = 100;

    const int Unit = 5;

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
                                tempos.AddLast(new TempoInfo(true, currentTime, tempo, metronome.BeatUnit, metronome.BeatUnitDot is not null, metronome.PerMinute));
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
                                        _notes.AddLast(CreateFrameInfo(note, currentTime, currentTime + duration));
                                    }
                                    else
                                    {
                                        if (ties.All(t => t.Type == StartStop.Start))
                                        {
                                            // タイ記号の始め
                                            tiedNote = CreateFrameInfo(note, currentTime, currentTime + duration);
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
                                                tiedNote.SetEndFrame((int)(currentTime + duration));
                                                tiedNote.SetBreath(GetIsBreath(note));
                                                _notes.AddLast(tiedNote);
                                            }
                                        }
                                        else
                                        {
                                            Debug.WriteLine(note);
                                            Debugger.Break();
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
        if (tempos is not { Count: > 0, First.Value.Time: 0 })
        {
            tempos.AddFirst(new TempoInfo(false, 0, DefaultTempo, "quarter", false, DefaultTempo));
        }

        // 先頭の小節情報がなければデフォルトを差し込む
        if (timeSignatures is not { Count: > 0, First.Value.Time: 0 })
        {
            timeSignatures.AddFirst(new TimeSignature(0, 4, 4));
        }

        return new(0, tempos, timeSignatures, _notes);
    }

    private static XmlSerializer _serializer = new XmlSerializer(typeof(MusicXmlObject));

    public static MusicXmlObject ParseMusicXml(string xml)
    {
        using (var sr = new StringReader(xml))
        using (var reader = XmlReader.Create(sr, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
        {
            return _serializer.Deserialize(reader) as MusicXmlObject ?? throw new NotSupportedException();
        }
    }

    static int GetCode(Pitch pitch)
    {
        int timble = KeyCodeForStep[pitch.Step];

        return (int)((pitch.Octave * 12) + (pitch.Alter ?? 0) + timble + 13);
    }

    private static bool GetIsBreath(Note note)
        => note.Notations?.Articulations?.BreathMark is not null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFrameIndex(decimal time) => (int)(time / Unit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int, int) GetFrameIndices(decimal startTime, decimal endTime) => (GetFrameIndex(startTime), GetFrameIndex(endTime));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRange(int beginFrame, int endFrame, int elementBeginFrame, int elementEndFrame)
        => beginFrame < elementEndFrame || endFrame >= elementBeginFrame;

    private static MusicXmlPhrase.Frame CreateFrameInfo(
        Note note, decimal startTime, decimal endTime)
        => new MusicXmlPhrase.Frame(
            (int)startTime, (int)endTime,
            note.Lyric.Text, GetCode(note.Pitch), GetIsBreath(note));

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
        [-7] = new() // ♭7つ
        {
            [KeyCodeF] = KeyCodeF - 1,
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeC] = KeyCodeC - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
            [KeyCodeG] = KeyCodeG - 1,
        },
        [-6] = new() // ♭6つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeC] = KeyCodeC - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
            [KeyCodeG] = KeyCodeG - 1,
        },
        [-5] = new() // ♭5つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
            [KeyCodeG] = KeyCodeG - 1,
        },
        [-4] = new() // ♭4つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
        },
        [-3] = new() // ♭3つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
        },
        [-2] = new() // ♭2つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeB] = KeyCodeB - 1,
        },
        [-1] = new() // ♭1つ
        {
            [KeyCodeB] = KeyCodeB - 1,
        },
        [1] = new() // ♯1つ
        {
            [KeyCodeF] = KeyCodeF + 1,
        },
        [2] = new() // ♯2つ
        {
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeC] = KeyCodeC + 1,
        },
        [3] = new()// ♯3つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeC] = KeyCodeC + 1,
        },
        [4] = new()// ♯4つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeD] = KeyCodeD + 1,
            [KeyCodeC] = KeyCodeC + 1,
        },
        [5] = new()// ♯5つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeD] = KeyCodeD + 1,
            [KeyCodeC] = KeyCodeC + 1,
            [KeyCodeA] = KeyCodeA + 1,
        },
        [6] = new()// ♯6つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeD] = KeyCodeD + 1,
            [KeyCodeC] = KeyCodeC + 1,
            [KeyCodeB] = KeyCodeB + 1,
            [KeyCodeA] = KeyCodeA + 1,
        },
    };
}
