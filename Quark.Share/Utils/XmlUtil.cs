using System.Collections.Concurrent;
using System.Xml.Serialization;

namespace Quark.Utils;

/// <summary>
/// XMLデータの関するUtilクラス
/// </summary>
public static class XmlUtil
{
    /// <summary>XMLシリアライザのキャッシュ</summary>
    private static ConcurrentDictionary<Type, XmlSerializer> _xmlSerializers { get; } = new(1, 2);

    /// <summary>
    /// XMLシリアライザを取得する。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static XmlSerializer GetXmlSerializer<T>()
        => _xmlSerializers.GetOrAdd(typeof(T), static _ => new XmlSerializer(typeof(T)));
}
