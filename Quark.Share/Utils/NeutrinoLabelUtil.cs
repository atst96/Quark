using Quark.Extensions;
using Quark.Models.Neutrino;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Quark.Utils;

/// <summary>
/// NEUTRINOのラベル処理に関するUtil
/// </summary>
internal static class NeutrinoLabelUtil
{
    /// <summary>
    /// fullラベル解析用の正規表現
    /// pitch: ピッチ e.g. Gb4
    /// tempo: テンポ
    /// </summary>
    private static readonly Regex FullLabelRegex = new(
        @"^.@.+\/E:(?<pitch>[0-9a-zA-Z]+)\].+~(?<tempo>\d+)!.+$",
        RegexOptions.Compiled);

    /// <summary>
    /// fullラベルの解析を行う
    /// </summary>
    /// <param name="labelPath"></param>
    /// <returns></returns>
    public static IReadOnlyList<FullLabelInfo> ParseFullLabel(string labelPath)
    {
        return GetLabelInfo(labelPath)
            .Select(l =>
            {
                var m = FullLabelRegex.Match(l.Phenome);

                var temp = m.GetValue("tempo"); // テンポ
                var code = m.GetValue("pitch"); // ピッチ

                return new FullLabelInfo(
                    l.Begin, l.End,
                    string.IsNullOrEmpty(temp) ? -1 : int.Parse(temp),
                    ParseCode(code),
                    l.Phenome);
            })
            .ToList();
    }

    private static readonly Regex CodeRegex = new Regex(@"^(?<code>[A-G])(?<half>[b#])?(?<octove>\d+)$", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, int> Codes = new Dictionary<string, int>()
    {
        ["A"] = 0,
        ["B"] = 2,
        ["C"] = 3,
        ["D"] = 5,
        ["E"] = 7,
        ["F"] = 8,
        ["G"] = 10,
    };

    /// <summary>
    /// 1オクターブ辺りのコード数(12音階)
    /// </summary>
    private const int CodesPerOctove = 12;

    /// <summary>
    /// コードを解析する
    /// </summary>
    /// <param name="codeValue"><A-G>[<半音下げ:b>]<オクターブ></param>
    /// <returns></returns>
    private static int ParseCode(string codeValue)
    {
        if (codeValue == "xx")
        {
            return -1;
        }

        var m = CodeRegex.Match(codeValue);

        var octove = int.Parse(m.GetValue("octove")); // オクターブ
        var code = m.GetValue("code"); // コード(A-G)
        var half = m.GetValue("half"); // 半音

        return ApplyChromaticScale((CodesPerOctove * octove) + Codes[code], half);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ApplyChromaticScale(int code, string half)
    {
        if (half == "b")
        {
            return code - 1;
        }
        else if (half == "#")
        {
            return code + 1;
        }

        return code;
    }

    private const string RegexBegin = "begin";
    private const string RegexEnd = "end";
    private const string RegexPhoneme = "phoneme";

    private static readonly Regex Regex = new(@"^(?<begin>\d+)\s(?<end>\d+)\s(?<phoneme>.+)$", RegexOptions.Compiled);

    /// <summary>
    /// ラ米る情報を取得する
    /// </summary>
    /// <param name="labelPath"></param>
    /// <returns></returns>
    public static IReadOnlyList<MonoLabelInfo> GetLabelInfo(string labelPath)
    {
        using (var sr = new StreamReader(labelPath, Encoding.UTF8))
        {
            return sr.EnumerateLines(ignoreEmptyLine: true)
                .Select(i => Regex.Match(i))
                .Where(i => i.Success)
                .Select(i =>
                {
                    var m = i.Groups;
                    var begin = long.Parse(m[RegexBegin].Value);
                    var end = long.Parse(m[RegexEnd].Value);
                    var phoneme = m[RegexPhoneme].Value;

                    return new MonoLabelInfo(begin, end, phoneme);
                })
                .ToList();
        }
    }
}
