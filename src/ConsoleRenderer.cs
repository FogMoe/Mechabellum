using System.Text.Json;

namespace Mechabellum.ObserverStats;

public static class ConsoleRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void WriteJson<T>(T value, bool indented = true)
    {
        var options = new JsonSerializerOptions(JsonOptions) { WriteIndented = indented };
        Console.WriteLine(JsonSerializer.Serialize(value, options));
    }

    public static void WriteRound(ObservedRound observed)
    {
        Console.WriteLine($"回放: {Path.GetFileName(observed.SourceFile)}");
        Console.WriteLine($"对局: {observed.BattleId ?? "未知"}  版本: {observed.Version?.ToString() ?? "未知"}  地图: {observed.MapId?.ToString() ?? "未知"}");
        Console.WriteLine($"模式: {observed.GameMode ?? "未知"}/{observed.MatchMode ?? "未知"}  回合: {observed.Round}");

        foreach (var player in observed.Players)
        {
            Console.WriteLine();
            Console.WriteLine($"[{player.Name}] Team={player.Team?.ToString() ?? "?"}  编队数={player.Units.Count}  补给={player.Supply?.ToString() ?? "?"}  核心={player.ReactorCore?.ToString() ?? "?"}");

            foreach (var group in player.Units.GroupBy(unit => new { unit.UnitId, unit.UnitName }).OrderBy(group => group.Key.UnitId))
            {
                var levels = string.Join(", ", group.GroupBy(unit => unit.DisplayLevel).OrderBy(level => level.Key).Select(level => $"Lv{level.Key}×{level.Count()}"));
                Console.WriteLine($"  {group.Key.UnitName} [ID {group.Key.UnitId}]: {group.Count()} 队 ({levels})");
            }

            if (player.Units.Count > 0)
            {
                Console.WriteLine("  明细:");
                Console.WriteLine("    Index  UnitId  Level  Exp       X       Y  Rotate  Equip");
                foreach (var unit in player.Units.OrderBy(unit => unit.FormationIndex))
                {
                    Console.WriteLine($"    {unit.FormationIndex,5}  {unit.UnitId,6}  {unit.DisplayLevel,5}  {unit.Experience,3}  {unit.X,6:0.#}  {unit.Y,6:0.#}  {(unit.IsRotated ? "yes" : "no"),6}  {unit.EquipmentId,5}");
                }
            }

            if (player.ActiveTechnologies.Count > 0)
            {
                Console.WriteLine("  已激活科技:");
                foreach (var tech in player.ActiveTechnologies)
                {
                    Console.WriteLine($"    {tech.UnitName}: {string.Join(", ", tech.TechnologyIds)}");
                }
            }
        }
    }
}
