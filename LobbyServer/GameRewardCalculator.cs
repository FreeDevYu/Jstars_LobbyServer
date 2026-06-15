using LobbyAPI.Models;
using Protocol;

namespace LobbyServer
{
    public readonly record struct MatchPlayerReward(
       long Uid,
       bool IsWin,
       int Kills,
       int Deaths,
       long ExpReward,
       long GoldReward);

    public static class GameRewardCalculator
    {
        public static MatchPlayerReward Calculate(PlayerMatchResult player, bool isWin)
        {
            long baseExp = isWin
                ? MatchRewardConstants.WinBaseExp
                : MatchRewardConstants.LoseBaseExp;

            long baseGold = isWin
                ? MatchRewardConstants.WinBaseGold
                : MatchRewardConstants.LoseBaseGold;

            int kills = Math.Max(0, player.Kills);

            long expReward = baseExp + kills * MatchRewardConstants.PerKillExp;
            long goldReward = baseGold + kills * MatchRewardConstants.PerKillGold;

            return new MatchPlayerReward(
                Uid: (long)player.Uid,
                IsWin: isWin,
                Kills: kills,
                Deaths: player.Deaths,
                ExpReward: expReward,
                GoldReward: goldReward);
        }

        public static IEnumerable<MatchPlayerReward> CalculateAll(MatchingResultDTO result)
        {
            foreach (var player in result.WinPlayers)
                yield return Calculate(player, isWin: true);

            foreach (var player in result.LosePlayers)
                yield return Calculate(player, isWin: false);
        }
    }
}