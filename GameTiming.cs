using System.Diagnostics;

namespace Moria;

public sealed partial class Game
{
    private const int MonsterTickMilliseconds = 750;

    private readonly Stopwatch runClock = new();
    private long lastMonsterTickMilliseconds;

    public TimeSpan RunElapsed => runClock.Elapsed;
    public static TimeSpan CurrentRunElapsed { get; private set; }

    private void ResetRealTimeClock()
    {
        runClock.Restart();
        lastMonsterTickMilliseconds = 0;
        CurrentRunElapsed = TimeSpan.Zero;
    }

    private void ProcessRealTime()
    {
        if (!started || runOver || victory || !player.Alive)
            return;

        CurrentRunElapsed = runClock.Elapsed;
        battleOverlay?.Invalidate();

        if (CurrentBattle != null)
            return;

        ProcessPlayerMovement();

        long elapsed = CurrentRunElapsed.Ticks / TimeSpan.TicksPerMillisecond;
        if (elapsed - lastMonsterTickMilliseconds < MonsterTickMilliseconds)
            return;

        lastMonsterTickMilliseconds = elapsed;
        ProcessMonsterTurn();
        if (!player.Alive && CurrentBattle == null)
            EndRun();
    }
}
