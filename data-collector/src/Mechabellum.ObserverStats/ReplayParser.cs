using System.Globalization;
using System.Xml.Linq;

namespace Mechabellum.ObserverStats;

public sealed class ReplayParser
{
    public ReplayMatch Parse(string replayPath)
    {
        var document = ReplayXmlExtractor.Extract(replayPath);
        var root = document.Root ?? throw new InvalidDataException("BattleRecord XML 没有根节点。");
        if (root.Name.LocalName != "BattleRecord")
        {
            throw new InvalidDataException($"不支持的 XML 根节点：{root.Name.LocalName}");
        }

        var battleInfo = root.Element("BattleInfo");
        var players = root
            .Element("playerRecords")?
            .Elements("PlayerRecord")
            .Select(ParsePlayer)
            .ToArray() ?? [];

        if (players.Length == 0)
        {
            throw new InvalidDataException("回放中没有找到玩家回合记录。");
        }

        return new ReplayMatch(
            Path.GetFullPath(replayPath),
            ReadNullableInt(root.Element("Version")),
            ReadString(battleInfo?.Element("BattleID")),
            ReadNullableInt(battleInfo?.Element("MapID")),
            ReadString(battleInfo?.Element("GameMode")),
            ReadString(battleInfo?.Element("MatchMode")),
            players);
    }

    private static PlayerHistory ParsePlayer(XElement player)
    {
        var rounds = player
            .Element("playerRoundRecords")?
            .Elements("PlayerRoundRecord")
            .Select(ParseRound)
            .OrderBy(round => round.Round)
            .ToArray() ?? [];

        return new PlayerHistory(
            ReadNullableUlong(player.Element("id")),
            ReadString(player.Element("name")) ?? "Unknown player",
            ReadNullableInt(player.Element("data")?.Element("team")),
            rounds);
    }

    private static PlayerRoundSnapshot ParseRound(XElement roundElement)
    {
        var playerData = roundElement.Element("playerData");
        var units = playerData?
            .Element("units")?
            .Elements("NewUnitData")
            .Select(ParseUnit)
            .OrderBy(unit => unit.FormationIndex)
            .ToArray() ?? [];

        var technologies = playerData?
            .Element("activeTechnologies")?
            .Elements("UnitData")
            .Select(ParseTechnologies)
            .ToArray() ?? [];

        return new PlayerRoundSnapshot(
            ReadInt(roundElement.Element("round")),
            ReadNullableInt(playerData?.Element("supply")),
            ReadNullableInt(playerData?.Element("reactorCore")),
            ReadString(playerData?.Element("preRoundFightResult")),
            units,
            technologies);
    }

    private static UnitDeployment ParseUnit(XElement unit)
    {
        var unitId = ReadInt(unit.Element("id"));
        var rawLevel = ReadInt(unit.Element("Level"));
        var position = unit.Element("Position");

        return new UnitDeployment(
            unitId,
            UnitCatalog.GetName(unitId),
            ReadInt(unit.Element("Index")),
            rawLevel,
            rawLevel + 1,
            ReadInt(unit.Element("Exp")),
            ReadFloat(position?.Element("x")),
            ReadFloat(position?.Element("y")),
            ReadBool(unit.Element("IsRotate")),
            ReadInt(unit.Element("EquipmentID")),
            ReadInt(unit.Element("SellSupply")),
            ReadInt(unit.Element("RoundCount")),
            ReadInt(unit.Element("Durability")));
    }

    private static UnitTechnologySet ParseTechnologies(XElement unitData)
    {
        var unitId = ReadInt(unitData.Element("id"));
        var technologyIds = unitData
            .Element("techs")?
            .Elements("tech")
            .Select(element => ReadAttributeInt(element, "data"))
            .Where(id => id != 0)
            .ToArray() ?? [];

        return new UnitTechnologySet(unitId, UnitCatalog.GetName(unitId), technologyIds);
    }

    private static int ReadInt(XElement? element) =>
        int.TryParse(element?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static int? ReadNullableInt(XElement? element) =>
        int.TryParse(element?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static ulong? ReadNullableUlong(XElement? element) =>
        ulong.TryParse(element?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static float ReadFloat(XElement? element) =>
        float.TryParse(element?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static bool ReadBool(XElement? element) =>
        bool.TryParse(element?.Value, out var value) && value;

    private static int ReadAttributeInt(XElement element, string name) =>
        int.TryParse(element.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static string? ReadString(XElement? element) =>
        string.IsNullOrWhiteSpace(element?.Value) ? null : element.Value.Trim();
}
