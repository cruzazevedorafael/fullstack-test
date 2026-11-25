namespace Investments.Api.Time;

/// <summary>Tells which calendar day "today" is in the business time zone.</summary>
public interface IBusinessCalendar
{
    DateOnly Today { get; }
}

public sealed class BusinessCalendar(TimeProvider clock, TimeZoneInfo timeZone) : IBusinessCalendar
{
    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).DateTime);
}
