namespace BlackFlowSpire.Combat;

public enum BlackFlowSpireActionSide { Player, Enemy }

// Virtual time advances only when a turn ends. Playing a card never advances
// either side's clock, and taking longer in the UI gives no speed advantage.
public sealed class BlackFlowSpireActionTimeline
{
    public const decimal BaseSpeed = 100;
    public const decimal ActionDistance = 10_000;
    private const decimal TieTolerance = 0.000000001m;
    private decimal _playerReady;
    private decimal _enemyReady;
    private BlackFlowSpireActionSide _lastScheduled = BlackFlowSpireActionSide.Enemy;

    public decimal PlayerSpeed { get; private set; } = BaseSpeed;
    public decimal EnemySpeed { get; private set; } = BaseSpeed;
    public decimal Time { get; private set; }
    public BlackFlowSpireActionSide CurrentSide { get; private set; } = BlackFlowSpireActionSide.Player;
    public bool IsExtraPlayerTurn { get; private set; }
    public bool Started { get; private set; }
    public int Revision { get; private set; }

    public void SetSpeed(BlackFlowSpireActionSide side, decimal speed)
    {
        if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed), "Action speed must be positive.");
        var oldSpeed = side == BlackFlowSpireActionSide.Player ? PlayerSpeed : EnemySpeed;
        if (speed == oldSpeed) return;
        if (Started && (side != CurrentSide || IsExtraPlayerTurn))
        {
            var ready = side == BlackFlowSpireActionSide.Player ? _playerReady : _enemyReady;
            var remainingWork = Math.Max(0, ready - Time) * oldSpeed;
            ready = Time + remainingWork / speed;
            if (side == BlackFlowSpireActionSide.Player) _playerReady = ready;
            else _enemyReady = ready;
        }
        if (side == BlackFlowSpireActionSide.Player) PlayerSpeed = speed;
        else EnemySpeed = speed;
        Revision++;
    }

    public BlackFlowSpireActionSide Start()
    {
        if (Started) return CurrentSide;
        _playerReady = ActionDistance / PlayerSpeed;
        _enemyReady = ActionDistance / EnemySpeed;
        Started = true;
        return SelectNext();
    }

    public BlackFlowSpireActionSide CompleteTurn(bool extraPlayerTurn = false)
    {
        if (!Started) throw new InvalidOperationException("The action timeline has not started.");
        if (!IsExtraPlayerTurn)
        {
            if (CurrentSide == BlackFlowSpireActionSide.Player) _playerReady = Time + ActionDistance / PlayerSpeed;
            else _enemyReady = Time + ActionDistance / EnemySpeed;
        }
        if (extraPlayerTurn)
        {
            // Native extra turns are inserted ahead of scheduled actions. They
            // do not consume another normal action or erase the enemy's progress.
            CurrentSide = BlackFlowSpireActionSide.Player;
            IsExtraPlayerTurn = true;
            Revision++;
            return CurrentSide;
        }
        IsExtraPlayerTurn = false;
        return SelectNext();
    }

    public IReadOnlyList<BlackFlowSpireActionSide> Preview(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var copy = (BlackFlowSpireActionTimeline)MemberwiseClone();
        if (!copy.Started) copy.Start();
        var result = new List<BlackFlowSpireActionSide>(count);
        for (var index = 0; index < count; index++) result.Add(copy.CompleteTurn());
        return result;
    }

    private BlackFlowSpireActionSide SelectNext()
    {
        var tie = Math.Abs(_playerReady - _enemyReady) <= TieTolerance;
        CurrentSide = tie ? (_lastScheduled == BlackFlowSpireActionSide.Player ? BlackFlowSpireActionSide.Enemy : BlackFlowSpireActionSide.Player)
            : _playerReady < _enemyReady ? BlackFlowSpireActionSide.Player : BlackFlowSpireActionSide.Enemy;
        Time = Math.Max(Time, CurrentSide == BlackFlowSpireActionSide.Player ? _playerReady : _enemyReady);
        _lastScheduled = CurrentSide;
        Revision++;
        return CurrentSide;
    }
}
