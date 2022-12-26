using Quark.Models.Neutrino;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Quark.Extensions;
using System.Text.RegularExpressions;

namespace Quark.Utils;

/// <summary>
/// NEUTRINOの推論モデルに関するUtilクラス
/// </summary>
public static class NeutrinoModelUtil
{
    /// <summary>
    /// モデル名抽出用の正規表現
    /// </summary>
    private static Regex ModelNameRegex = new(@"#\s*.+-\s*(?:(.+)(?:\s*（.+）)\s*|(.+)\s*)$", RegexOptions.Compiled);

    /// <summary>
    /// モデル情報を取得する
    /// </summary>
    /// <param name="path">モデル格納先のディレクトリ</param>
    /// <returns>モデル情報</returns>
    public static IList<ModelInfo> GetModels(string path)
        => GetModelDirectories(path)
            .Select(i => new ModelInfo(GetModelName(i), i))
            .ToList();

    /// <summary>
    /// バージョン情報のファイル名
    /// </summary>
    private const string VersionInfoFileName = "VERSION_INFO.txt";

    /// <summary>
    /// モデルのパスからモデル名を取得する
    /// </summary>
    /// <param name="modelPath"></param>
    /// <returns></returns>
    private static string GetModelName(string modelPath)
    {
        var file = new FileInfo(Path.Combine(modelPath, VersionInfoFileName));
        if (file.Exists)
        {
            using var reader = new StreamReader(file.OpenRead(), Encoding.UTF8);
            foreach (var line in reader.EnumerateLines())
            {
                // 正規表現に一致する文字列があれば抽出して返却する
                var m = ModelNameRegex.Match(line);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
        }

        // バージョン情報のファイルがない、もしくはパターンに
        // 一致する行がなければモデルのフォルダ名を返す
        return Path.GetFileName(modelPath);
    }

    /// <summary>
    /// モデルがあるディレクトリを取得する
    /// </summary>
    /// <param name="path">ディレクトリのパス</param>
    /// <returns></returns>
    private static string[] GetModelDirectories(string path)
        => Directory.GetDirectories(path);
}
