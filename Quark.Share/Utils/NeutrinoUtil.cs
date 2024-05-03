using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Diagnostics;
using Quark.Components;
using Quark.Constants;
using Quark.Data.Projects;
using Quark.Extensions;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects.Tracks;
using DoubleRange = (double Upper, double Lower, double Range);
using FloatRange = (float Upper, float Lower, float Range);

namespace Quark.Utils;

/// <summary>
/// NEUTRINOに関するUtilクラス
/// </summary>
public static partial class NeutrinoUtil
{
    /// <summary>フレーズ情報パース用の正規表現</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"^(?<beginTime>\d+)\s(?<endTime>\d+)\s(?<phoneme>.+)$", RegexOptions.Compiled)]
    private static partial Regex GetTimingRegex();

    /// <summary>文字列を改行で分割して正規表現で解析する</summary>
    /// <param name="input">入力</param>
    /// <param name="regex">正規表現</param>
    /// <returns>正規表現に一致した要素</returns>
    private static IEnumerable<Match> SplitWithRegex(string input, Regex regex)
        => input
            .Split('\n', '\r')
            .Select(s => regex.Match(s)).Where(r => r.Success);

    /// <summary>
    /// </summary>
    /// <typeparam name="T">出力の型</typeparam>
    /// <param name="input">入力</param>
    /// <param name="regex">正規表現</param>
    /// <param name="conv">変換用関数</param>
    /// <returns></returns>
    private static IEnumerable<T> ParseWithRegex<T>(string input, Regex regex, Func<Match, T> conv)
        => SplitWithRegex(input, regex).Select(conv);

    /// <summary>
    /// 発声タイミングの情報をパースする
    /// </summary>
    /// <param name="phrasees">フレーズ情報</param>
    /// <param name="timing">タイミング情報(ファイル内容)</param>
    /// <returns></returns>
    public static List<PhonemeTiming> ParseTiming(IReadOnlyList<PhraseInfo> phrasees, string timing)
    {
        int phraseIdx = -1;
        int phrasePhonemeCount = 0;
        int phonemeCount = 0;

        var timings = new List<PhonemeTiming>(phrasees.Sum(p => p.Phoneme.Sum(p2 => p2.Length)));

        foreach (var r in SplitWithRegex(timing, GetTimingRegex()))
        {
            if (phrasePhonemeCount <= phonemeCount)
            {
                // 音素が次のフレーズに移った場合
                phrasePhonemeCount += phrasees[++phraseIdx].Phoneme.Sum(p => p.Length);
            }

            int beginTime = TimingTimeToMs(r.GetValue<long>("beginTime"));
            timings.Add(new(beginTime, beginTime, r.GetValue("phoneme"), phraseIdx));

            ++phonemeCount;
        }

        return timings;
    }

    /// <summary>フレーズ情報パース用の正規表現</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"^(?<no>\d+)\s(?<time>\d+)\s(?<flag>\d)\s(?<label>.+)$", RegexOptions.Compiled)]
    private static partial Regex GetPhraseRegex();

    /// <summary>
    /// フレーズ情報をパースする
    /// </summary>
    /// <param name="content"フレーズ情報></param>
    /// <returns></returns>
    public static PhraseInfo[] ParseRawPhrase(string content)
        => ParseWithRegex<PhraseInfo>(content, GetPhraseRegex(),
            r => new(
                r.GetValue<int>("no"),
                r.GetValue<int>("time"),
                r.GetValue<int>("flag") != 0,
                ParsePhrasePhonemes(r.GetValue("label"))))
            .ToArray();

    /// <summary>
    /// TODO: メソッドを分解する
    /// フレーズ情報をパースする
    /// </summary>
    /// <param name="rawPhrases">全フレーズ情報</param>
    /// <param name="timings">全タイミング情報</param>
    /// <returns></returns>
    public static T[] ParsePhrases<T>(
        IReadOnlyList<PhraseInfo> rawPhrases, IReadOnlyList<PhonemeTiming> timings,
        Func<int, int, int, string[][], PhraseStatus, T> func)
        where T : INeutrinoPhrase
    {
        var phrases = new T[rawPhrases.Count(p => p.IsVoiced)];

        for (int idx = 0, destIdx = 0; idx < rawPhrases.Count; ++idx)
        {
            // idx: parsedPhrasesの要素インデックス
            // destIdx: phrasesの要素インデックス

            (int no, int beginTime, bool isVoiced, string[][] phonemes) = rawPhrases[idx];

            // TODO: 有声でない場合は省く(今後の実装で変わるかも)
            if (!isVoiced)
                continue;

            // 終了するフレーム番号を取得する
            // 最後の要素の場合はタイミング情報の最後から終了位置を取得する。
            int endFrameIdx = rawPhrases.Count > (idx + 1)
                ? (rawPhrases[idx + 1].Time - 1)
                : timings[^1].EditedTimeMs;

            phrases[destIdx++] = func(no, beginTime, endFrameIdx, phonemes, PhraseStatus.WaitEstimate);
        }

        return phrases;
    }

    /// <summary>
    /// フレーズ情報の音素情報をパースする
    /// </summary>
    /// <param name="label">ラベル</param>
    /// <returns></returns>
    private static string[][] ParsePhrasePhonemes(string label)
    {
        var option = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;

        return label.Split(',', option).Select(s => s.Split(' ', option)).ToArray();
    }

    /// <summary>タイミング情報の区切り文字</summary>
    private const string TimingSeparator = "\n";

    /// <summary>
    /// タイミング情報のテキストデータを取得する
    /// </summary>
    /// <param name="timings">タイミング情報</param>
    /// <returns></returns>
    public static byte[] GetTimingContent(TimingInfo[] timings)
    {
        if (timings.Length == 0)
            return Array.Empty<byte>();

        var sb = new StringBuilder();

        // 1件目のみ改行なしで書き込む
        WriteTimingInfo(sb, timings[0]);

        // 2件目以降は改行ありで書き込む
        for (int idx = 1; idx < timings.Length; ++idx)
        {
            _ = sb.Append(TimingSeparator);
            WriteTimingInfo(sb, timings[idx]);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// タイミング情報のテキストデータを取得する
    /// </summary>
    /// <param name="timings">タイミング情報</param>
    /// <returns></returns>
    public static byte[] GetTimingContent(IReadOnlyList<PhonemeTiming> timings)
    {
        if (!timings.Any())
            return [];

        var sb = new StringBuilder();

        for (int idx = 0, l = timings.Count - 1; idx < l; ++idx)
        {
            var curTiming = timings[idx];
            var nextTiming = timings[idx + 1];

            WriteTimingInfo(sb, curTiming.EditedTimeMs, nextTiming.EditedTimeMs, curTiming.Phoneme);
            _ = sb.Append(TimingSeparator);
        }

        var lastTiming = timings.Last();
        WriteTimingInfo(sb, lastTiming.EditedTimeMs, lastTiming.EditedTimeMs, lastTiming.Phoneme);

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// タイミング情報に書き込む
    /// </summary>
    /// <param name="sb">書込み先の<see cref="StringBuilder"/></param>
    /// <param name="timing">タイミング情報</param>
    private static void WriteTimingInfo(StringBuilder sb, TimingInfo timing)
        => sb.Append($"{timing.EditedBeginTime100Ns} {timing.EditedEndTime100Ns} {timing.Phoneme}");
    private static void WriteTimingInfo(StringBuilder sb, int beginTime, int endTIme, string phoneme)
        => sb.Append($"{GetTimingTimeFromMs(beginTime)} {GetTimingTimeFromMs(endTIme)} {phoneme}");

    /// <summary>フレーズ情報の区切り文字(改行)</summary>
    private const string PhraseSeparator = "\n";
    /// <summary>フレーズ情報おける音素グループ?の区切り文字</summary>
    private const string PhonemesGroupSeparator = ", ";
    /// <summary>フレーズ情報における音素の区切り文字</summary>
    private const string PhonemeSeparator = " ";

    /// <summary>
    /// フレーズ情報のテキストデータを作成する
    /// </summary>
    /// <param name="phrases">フレーズ情報</param>
    /// <returns>バイトデータ</returns>
    public static byte[] GetPhraseContent(PhraseInfo[] phrases)
    {
        if (phrases.Length == 0)
            return Array.Empty<byte>();

        var sb = new StringBuilder();

        // 1見目を書き込み
        WritePhraseInfo(sb, phrases[0]);

        // 2見目意向を区切り文字とともに書込み
        for (int idx = 1; idx < phrases.Length; ++idx)
        {
            _ = sb.Append(PhraseSeparator);
            WritePhraseInfo(sb, phrases[idx]);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// フレーズ情報を書き込む
    /// </summary>
    /// <param name="sb">書込み対象<seealso cref="StringBuilder"/></param>
    /// <param name="info">対象フレーズ情報</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WritePhraseInfo(StringBuilder sb, PhraseInfo info)
    {
        // 音素情報までの部分を書き込む
        sb.Append($"{info.No} {info.Time} {(info.IsVoiced ? 0 : 1)} ");

        // 音素情報を書き込む
        var phonemes = info.Phoneme;
        if (phonemes.Length == 0)
            return;

        // 1見目を書き込み
        sb.AppendJoin(PhonemeSeparator, phonemes[0]);

        // 2件目以降を区切り文字とともに書込み
        for (int idx = 1; idx < phonemes.Length; ++idx)
        {
            sb.Append(PhonemesGroupSeparator);
            sb.AppendJoin(PhonemeSeparator, phonemes[idx]);
        }
    }

    /// <summary>標準出力される進捗情報をパースするための正規表現</summary>
    private static readonly Regex ProgressRegex = new(@"^.+Progress\s*=\s*(?<progress>\d+)\s*%.+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// NEUTRINOを実行する
    /// </summary>
    /// <param name="command">実行ファイル</param>
    /// <param name="args">コマンドライン引数</param>
    /// <param name="workdir">作業ディレクトリ</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns><see cref="Task"/></returns>
    /// <exception cref="NeutrinoExecuteException">実行失敗情報</exception>
    public static Task Execute(string command, string? args = null, string? workdir = null, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
        => Execute(Guid.NewGuid(), command, args, workdir, progress, cancellationToken);

    /// <summary>
    /// NEUTRINOを実行する
    /// </summary>
    /// <param name="executionId">実行時識別ID</param>
    /// <param name="command">実行ファイル</param>
    /// <param name="args">コマンドライン引数</param>
    /// <param name="workdir">作業ディレクトリ</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns><see cref="Task"/></returns>
    /// <exception cref="NeutrinoExecuteException">実行失敗情報</exception>
    public static async Task Execute(Guid executionId, string command, string? args = null, string? workdir = null, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        // 実行開始から終了までの一連の流れを特定するための識別子
        var guid = Guid.NewGuid().ToString("N");

        // 実行開始ログ
        Trace.WriteLine($"{guid}: === START NEUTRINO ===");
        Trace.WriteLine($"{guid}: Pwd: {workdir}");
        Trace.WriteLine($"{guid}: Execute: {command} {args}");

        bool isInitializing = true;

        // 出力情報を保持しておく
        StringBuilder output = new();

        try
        {
            var (_, stdout, stderr) = ProcessX.GetDualAsyncEnumerable(command, args, workdir);

            var stdoutTask = Task.Run(async () =>
            {
                await foreach (var line in stdout.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    Trace.WriteLine($"{guid}: {line}");
                    output.AppendLine(line);

                    double? progressValue = null;

                    var m = ProgressRegex.Match(line);
                    if (m.Success)
                    {
                        isInitializing = false;
                        progressValue = double.Parse(m.Groups["progress"].Value);
                    }

                    progress?.Report(new(isInitializing ? ProgressReportType.Idertimate : ProgressReportType.InProgress, line, progressValue));
                }
            });

            var stderrTask = Task.Run(async () =>
            {
                await foreach (var line in stderr.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    Trace.TraceError($"{guid}: {line}");
                    output.AppendLine(line);
                }
            });

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch (ProcessErrorException pee)
        {
            // 実行失敗時
            progress?.Report(new(ProgressReportType.Error, null, 100));

            throw new NeutrinoExecuteException(
                command, workdir, args, pee.ExitCode, output.ToString(), pee);
        }
        finally
        {
            // 実行終了ログ
            Trace.WriteLine($"{guid}: === END NEUTRINO ===");
        }
    }

    /// <summary>
    /// ミリ秒をタイミング用の時間(100ns)に変換する
    /// </summary>
    /// <param name="timeMs">ミリ秒数</param>
    /// <returns>100ns換算した時間</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetTimingTimeFromMs(int timeMs) => (long)timeMs * 10000;

    /// <summary>
    /// タイミング用の時間(100ns)をミリ秒に変換する
    /// </summary>
    /// <param name="time">時間(100ns)</param>
    /// <returns>ミリ秒</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TimingTimeToMs(long time) => (int)(time / 10000);

    /// <summary>
    /// ミリ秒をフレーム位置に変換する
    /// </summary>
    /// <param name="ms">時間(ミリ秒)</param>
    /// <returns>フレーム位置</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MsToFrameIndex(int ms) => ms / NeutrinoConfig.FramePeriod;

    /// <summary>
    /// フレーム位置をミリ秒に変換する
    /// </summary>
    /// <param name="frameIndex">フレーム位置</param>
    /// <returns>時間(ミリ秒)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FrameIndexToMs(int frameIndex) => frameIndex * NeutrinoConfig.FramePeriod;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (T Lower, T Upper, T Range) ToRangeInfo<T>(T upper, T lower)
        where T : struct, INumber<T>
        => new(upper, lower, upper - lower);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (T, T, T) ToLinearScale<T>(ref (T Upper, T Lower, T) range)
        where T : struct, IBinaryFloatingPointIeee754<T>
        => ToRangeInfo(SignalUtil.ToLinearScale(range.Upper - range.Lower), T.Zero);

    /// <summary>MGC(対数スケール)の範囲</summary>
    public static readonly DoubleRange MgcValue = ToRangeInfo(NeutrinoConfig.MgcUpper, NeutrinoConfig.MgcLower);

    /// <summary>MGC(リニアスケール)の範囲</summary>
    public static readonly DoubleRange MgcLinearValue = ToLinearScale(ref MgcValue);

    /// <summary>MGC(対数スケール)の範囲</summary>
    public static readonly FloatRange MgcValueF = ToRangeInfo(NeutrinoConfig.MgcUpperF, NeutrinoConfig.MgcLowerF);

    /// <summary>MGC(理にあるケース)の範囲</summary>
    public static readonly FloatRange MgcLinearValueF = ToLinearScale(ref MgcValueF);

    /// <summary>メルスペクトログラム(対数スケール)の範囲</summary>
    public static readonly FloatRange MspecValueF = ToRangeInfo(NeutrinoConfig.MspecUpper, NeutrinoConfig.MspecLower);

    /// <summary>メルスペクトログラム(リニアスケール)の範囲</summary>
    public static readonly FloatRange MspecLinearValueF = ToLinearScale(ref MspecValueF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T LinearCoeToLogValue<T>((T Upper, T Lower, T Range) range, T coe, T lower)
        where T : IBinaryFloatingPointIeee754<T>
        => SignalUtil.ToLogScale((coe * range.Range) + range.Lower) + lower;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T LogValueToCoe<T>((T Upper, T Lower, T Range) range, T value, T lower)
        where T : IBinaryFloatingPointIeee754<T>
        => (SignalUtil.ToLinearScale(value - lower) - range.Lower) / range.Range;



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearMgcCoeToLogValue(float coe)
        => LinearCoeToLogValue(MgcLinearValueF, coe, MgcValueF.Lower);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double LinearMgcCoeToLogValue(double coe)
        => LinearCoeToLogValue(MgcLinearValue, coe, MgcValue.Lower);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearMspecCoeToLogValue(float coe)
        => LinearCoeToLogValue(MspecLinearValueF, coe, MspecValueF.Lower);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MgcToCoe(double value)
        => LogValueToCoe(MgcLinearValue, value, MgcValue.Lower);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MgcToCoe(float value)
        => LogValueToCoe(MgcLinearValueF, value, MgcValueF.Lower);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MspecToCoe(float value)
        => LogValueToCoe(MspecLinearValueF, value, MspecValueF.Lower);

    /// <summary>
    /// MGC(対数スケール)の値をリニアスケールに変換する。
    /// </summary>
    /// <param name="value">MGC</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T MgcToLinear<T>(T value) where T : IFloatingPointIeee754<T>
        => SignalUtil.ToLinearScale(value);

    /// <summary>
    /// メルスペクトログラム(対数スケール)の値をリニアスケールに変換する。
    /// </summary>
    /// <param name="value">メルスペクトログラム</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T MspecToLinear<T>(T value) where T : IFloatingPointIeee754<T>
        => SignalUtil.ToLinearScale(value);

    /// <summary>
    /// 保存用のタイミング情報から内部処理用に変換する
    /// </summary>
    /// <param name="timings"></param>
    /// <param name="rawPhrases"></param>
    /// <returns></returns>
    public static IReadOnlyList<PhonemeTiming> ConvertTimingFromConfig(IReadOnlyList<TimingInfo> timings, IReadOnlyList<PhraseInfo> rawPhrases)
    {
        if (timings is null || timings.Count == 0)
            return [];

        var list = new List<PhonemeTiming>(timings.Count);

        int phraseIdx = -1;
        int totalPhonemeCount = 0;

        for (int idx = 0; idx < timings.Count; ++idx)
        {
            if (totalPhonemeCount <= idx)
            {
                ++phraseIdx;
                totalPhonemeCount += rawPhrases[phraseIdx].GetTotalPhonemeCount();
            }

            var timing = timings[idx];
            list.Add(new PhonemeTiming(
                TimingTimeToMs(timing.OriginBeginTime100Ns),
                TimingTimeToMs(timing.EditedBeginTime100Ns),
                timing.Phoneme, phraseIdx));
        }

        return list;
    }

    /// <summary>
    /// 内部処理用のタイミング情報を保存用に変換する
    /// </summary>
    /// <param name="timings"></param>
    /// <returns></returns>
    public static TimingInfo[] ConvertTimingToConfig(IReadOnlyList<PhonemeTiming> timings)
    {
        if (timings.Count == 0)
            return [];

        var result = new TimingInfo[timings.Count];

        int lastIdx = timings.Count - 1;
        for (int idx = 0; idx < lastIdx; ++idx)
        {
            result[idx] = new(GetTimingTimeFromMs(timings[idx].EditedTimeMs), GetTimingTimeFromMs(timings[idx + 1].EditedTimeMs), timings[idx].Phoneme);
        }

        var lastTiming = timings[lastIdx];
        long lastTimingTimeMs = GetTimingTimeFromMs(lastTiming.EditedTimeMs);
        result[lastIdx] = new(lastTimingTimeMs, lastTimingTimeMs, lastTiming.Phoneme);

        return result;
    }
}
