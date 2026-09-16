using System.Diagnostics;

namespace Moria;

public sealed partial class Game
{
    private const int MonsterTickMilliseconds = 750;

    private readonly Stopwatch runClock = new();
    private long lastMonsterTickMilliseconds;

    public TimeSpan RunElapsed => runClock.Elapsed;

    private void ResetRealTimeClock()
    {
        runClock.Restart();
        lastMonsterTickMilliseconds = 0;
    }

    private void ProcessRealTime()
    {
        if (!started || runOver || victory || !player.Alive)
            return;

        long elapsed = runClock.ElapsedMilliseconds;
        if (elapsed - lastMonsterTickMilliseconds < MonsterTickMilliseconds)
            return;

        lastMonsterTickMilliseconds = elapsed;
        MonstersAct();
        if (!player.Alive)
            EndRun();
    }
}
