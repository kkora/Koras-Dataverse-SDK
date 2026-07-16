namespace Koras.Dataverse.UnitTests.TestInfrastructure;

/// <summary>
/// Deterministic <see cref="TimeProvider"/>: controllable wall clock, and timers fire
/// immediately while recording the requested due time (keeps retry tests instant).
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    public List<TimeSpan> RequestedDelays { get; } = new();

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan by) => _utcNow += by;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        RequestedDelays.Add(dueTime);
        callback(state);
        return new ImmediateTimer();
    }

    private sealed class ImmediateTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
