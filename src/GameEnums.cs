namespace Mechabellum.ObserverStats;

public static class GameEnums
{
    public static string GameMode(int value) => value switch
    {
        0 => "Normal",
        1 => "Competition",
        2 => "Guider",
        3 => "Survive",
        4 => "Rift",
        _ => $"GameMode#{value}"
    };

    public static string MatchMode(int value) => value switch
    {
        0 => "VS_1_1",
        1 => "VS_2_2",
        2 => "VS_4_Scuffle",
        3 => "VS_2_2_Scuffle",
        _ => $"MatchMode#{value}"
    };

    public static string FightResult(int value) => value switch
    {
        0 => "Win",
        1 => "Lose",
        2 => "Deuce",
        _ => $"FightResult#{value}"
    };
}

