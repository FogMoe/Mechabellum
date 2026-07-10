namespace Mechabellum.ObserverStats;

public static class RoundSelector
{
    public static ObservedRound Select(ReplayMatch match, int? requestedRound = null)
    {
        var roundsPresentForEveryPlayer = match.Players
            .Select(player => player.Rounds.Select(round => round.Round).ToHashSet())
            .Aggregate((left, right) =>
            {
                left.IntersectWith(right);
                return left;
            });

        var selectedRound = requestedRound ??
            (roundsPresentForEveryPlayer.Count > 0
                ? roundsPresentForEveryPlayer.Max()
                : match.Players.SelectMany(player => player.Rounds).Max(round => round.Round));

        var players = match.Players.Select(player =>
        {
            var snapshot = player.Rounds.FirstOrDefault(round => round.Round == selectedRound)
                ?? throw new InvalidOperationException($"玩家 {player.Name} 没有第 {selectedRound} 回合的快照。");

            return new ObservedPlayer(
                player.PlayerId,
                player.Name,
                player.Team,
                "BattleStart",
                snapshot.Supply,
                snapshot.ReactorCore,
                snapshot.PreviousFightResult,
                snapshot.Units,
                snapshot.ActiveTechnologies);
        }).ToArray();

        return new ObservedRound(
            match.SourceFile,
            match.Version,
            match.BattleId,
            match.MapId,
            match.GameMode,
            match.MatchMode,
            selectedRound,
            players);
    }
}
