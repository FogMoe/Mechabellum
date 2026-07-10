using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Mechabellum.ObserverStats;

public static class ReplayXmlExtractor
{
    private static readonly byte[] XmlStart = "<?xml"u8.ToArray();
    private static readonly byte[] BattleRecordEnd = "</BattleRecord>"u8.ToArray();
    private const int MaxXmlBytes = 64 * 1024 * 1024;

    public static XDocument Extract(string replayPath)
    {
        if (!File.Exists(replayPath))
        {
            throw new FileNotFoundException("找不到回放文件。", replayPath);
        }

        var bytes = File.ReadAllBytes(replayPath);
        var start = bytes.AsSpan().IndexOf(XmlStart);
        if (start < 0)
        {
            throw new InvalidDataException("回放中未找到 BattleRecord XML 起始标记。文件格式可能已随游戏版本变化。");
        }

        var tail = bytes.AsSpan(start);
        var relativeEnd = tail.IndexOf(BattleRecordEnd);
        if (relativeEnd < 0)
        {
            throw new InvalidDataException("回放中的 BattleRecord XML 不完整；文件可能仍在写入。");
        }

        var length = relativeEnd + BattleRecordEnd.Length;
        if (length > MaxXmlBytes)
        {
            throw new InvalidDataException($"回放 XML 超过安全上限 {MaxXmlBytes / 1024 / 1024} MiB。");
        }

        using var stream = new MemoryStream(bytes, start, length, writable: false);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxXmlBytes
        };

        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }
}
