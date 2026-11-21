namespace DispatchApi.Infrastructure;

public interface IClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
    bool IsSimulated { get; }
}

public class ClockOptions
{
    /// <summary>"Simulated" (default) or "Real".</summary>
    public string Mode { get; set; } = "Simulated";

    /// <summary>Time of day the simulated clock starts at. The seed data is generated as a snapshot at 11:30.</summary>
    public TimeOnly SimulatedStartTime { get; set; } = new(11, 30);

    /// <summary>How fast simulated time advances relative to wall-clock time (1 = real time, 0 = frozen).</summary>
    public double SpeedFactor { get; set; } = 1;
}

/// <summary>
/// Operations clock. In Simulated mode, "now" starts at today + SimulatedStartTime when the API boots and advances
/// with wall-clock time, which keeps the seed data's "today" storyline consistent no matter when you run the demo.
/// All times are naive operations-local time (no time zone), matching the database.
/// </summary>
public class DispatchClock(ClockOptions options) : IClock
{
    private readonly DateTime _bootReal = DateTime.Now;
    private readonly DateTime _bootSim = DateTime.Today.Add(options.SimulatedStartTime.ToTimeSpan());

    public bool IsSimulated => !string.Equals(options.Mode, "Real", StringComparison.OrdinalIgnoreCase);

    public DateTime Now
    {
        get
        {
            var t = IsSimulated
                ? _bootSim.AddTicks((long)((DateTime.Now - _bootReal).Ticks * options.SpeedFactor))
                : DateTime.Now;
            return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second); // whole seconds - keeps SQLite text compact
        }
    }

    public DateOnly Today => DateOnly.FromDateTime(Now);
}
