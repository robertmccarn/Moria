using System.IO;

namespace Moria;

public sealed partial class Game
{
    private void SetMessage(string text)
    {
        message = text;
        chatLog.Add(text);
        if (chatLog.Count > 250)
            chatLog.RemoveAt(0);
    }

    private void EndRun()
    {
        if (runOver || victory)
            return;

        runOver = true;
        player.RunsCompleted++;
        lastRunReward = Math.Max(10, player.Gold / 3 + player.DungeonLevel * 10);
        player.PermanentGold += lastRunReward;
        SaveLegacyGold();
        SetMessage($"You fell in dungeon level {player.DungeonLevel}. +{lastRunReward} Legacy Gold.");
    }

    private void WinRun()
    {
        if (victory || runOver)
            return;

        victory = true;
        player.RunsCompleted++;
        lastRunReward = Math.Max(250, player.Gold + player.DungeonLevel * 25);
        player.PermanentGold += lastRunReward;
        SaveLegacyGold();
        SetMessage("The Balrog falls. Moria has been conquered!");
    }

    private void StartFreshRun()
    {
        int legacy = player.PermanentGold;
        player = new Player(player.Name, new Core.Position(1, 1), legacy);
        StartRun(player);
        SetMessage($"New run begins. Legacy Gold: {legacy}.");
        Focus();
        Invalidate();
    }

    private long LoadLegacyGold()
    {
        try
        {
            if (!File.Exists(MetaFile))
                return 0;

            string value = File.ReadAllText(MetaFile).Split('|')[0];
            return long.TryParse(value, out long gold) ? Math.Max(0, gold) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void SaveLegacyGold() => File.WriteAllText(MetaFile, $"{player.PermanentGold}|{player.RunsCompleted}");

    private void SaveProgress()
    {
        File.WriteAllText(
            "moria.sav",
            string.Join('|', player.Name, player.Level, player.Experience, player.Hp, player.TotalMaxHp, player.Gold, player.DungeonLevel, player.PermanentGold));
        SaveLegacyGold();
    }
}
