namespace Mechabellum.ObserverStats;

public sealed record ReplayMatch(
    string SourceFile,
    int? Version,
    string? BattleId,
    int? MapId,
    string? GameMode,
    string? MatchMode,
    IReadOnlyList<PlayerHistory> Players);

public sealed record PlayerHistory(
    ulong? PlayerId,
    string Name,
    int? Team,
    IReadOnlyList<PlayerRoundSnapshot> Rounds);

public sealed record PlayerRoundSnapshot(
    int Round,
    int? Supply,
    int? ReactorCore,
    string? PreviousFightResult,
    IReadOnlyList<UnitDeployment> Units,
    IReadOnlyList<UnitTechnologySet> ActiveTechnologies);

public sealed record UnitDeployment(
    int UnitId,
    string UnitName,
    int FormationIndex,
    int RawLevel,
    int DisplayLevel,
    int Experience,
    float X,
    float Y,
    bool IsRotated,
    int EquipmentId,
    int SellSupply,
    int RoundCount,
    int Durability);

public sealed record UnitTechnologySet(
    int UnitId,
    string UnitName,
    IReadOnlyList<int> TechnologyIds);

public sealed record ObservedRound(
    string SourceFile,
    int? Version,
    string? BattleId,
    int? MapId,
    string? GameMode,
    string? MatchMode,
    int Round,
    IReadOnlyList<ObservedPlayer> Players);

public sealed record ObservedPlayer(
    ulong? PlayerId,
    string Name,
    int? Team,
    string? State,
    int? Supply,
    int? ReactorCore,
    string? PreviousFightResult,
    IReadOnlyList<UnitDeployment> Units,
    IReadOnlyList<UnitTechnologySet> ActiveTechnologies);
