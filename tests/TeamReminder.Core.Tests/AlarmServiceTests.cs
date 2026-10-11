using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class AlarmServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidTitleDoesNotWrite(string? title)
    {
        var repo = new MemoryRepository();
        var service = new AlarmService(repo, new FixedClock(Now));
        var error = await Assert.ThrowsAsync<AlarmValidationException>(() =>
            service.CreateAsync(title, Now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc));
        Assert.Equal("Title", error.Field);
        Assert.Empty(repo.Alarms);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public async Task TitleLengthBoundaries(int length, bool valid)
    {
        var repo = new MemoryRepository();
        var service = new AlarmService(repo, new FixedClock(Now));
        var operation = () => service.CreateAsync(new string('a', length), Now.Date,
            TimeSpan.FromHours(13), TimeZoneInfo.Utc);
        if (valid) Assert.Equal(length, (await operation()).Title.Length);
        else await Assert.ThrowsAsync<AlarmValidationException>(operation);
        Assert.Equal(valid ? 1 : 0, repo.Alarms.Count);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    public async Task PastOrCurrentInstantDoesNotWrite(int hour)
    {
        var repo = new MemoryRepository();
        var service = new AlarmService(repo, new FixedClock(Now));
        await Assert.ThrowsAsync<AlarmValidationException>(() =>
            service.CreateAsync("Consulta", Now.Date, TimeSpan.FromHours(hour), TimeZoneInfo.Utc));
        Assert.Empty(repo.Alarms);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task MissingDateOrTimeDoesNotWrite(bool datePresent, bool timePresent)
    {
        var repo = new MemoryRepository();
        var service = new AlarmService(repo, new FixedClock(Now));
        await Assert.ThrowsAsync<AlarmValidationException>(() => service.CreateAsync("Consulta",
            datePresent ? Now.Date : null, timePresent ? TimeSpan.FromHours(13) : null, TimeZoneInfo.Utc));
        Assert.Empty(repo.Alarms);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public async Task InvalidTimeOfDayDoesNotWrite(int hour)
    {
        var repo = new MemoryRepository();
        await Assert.ThrowsAsync<AlarmValidationException>(() => new AlarmService(repo, new FixedClock(Now))
            .CreateAsync("Consulta", Now.Date, TimeSpan.FromHours(hour), TimeZoneInfo.Utc));
        Assert.Empty(repo.Alarms);
    }

    [Fact]
    public async Task NormalizesTitleAndConvertsLocalTimeToUtc()
    {
        var repo = new MemoryRepository();
        var zone = TimeZoneInfo.CreateCustomTimeZone("TestMinus3", TimeSpan.FromHours(-3), "Test", "Test");
        var alarm = await new AlarmService(repo, new FixedClock(Now))
            .CreateAsync("  Consulta  ", Now.Date, TimeSpan.FromHours(14), zone);
        Assert.Equal("Consulta", alarm.Title);
        Assert.Equal(Now.Date.AddHours(17), alarm.ScheduledAtUtc);
        Assert.Equal(DateTimeKind.Utc, alarm.ScheduledAtUtc.Kind);
        Assert.Equal(Now, alarm.CreatedAtUtc);
        Assert.Equal(zone.Id, alarm.TimeZoneId);
        Assert.NotEqual(Guid.Empty, alarm.Id);
    }

    [Theory]
    [InlineData(3, 8, 2, 30)]
    [InlineData(11, 1, 1, 30)]
    public async Task RejectsInvalidAndAmbiguousDaylightSavingTimes(int month, int day, int hour, int minute)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var repo = new MemoryRepository();
        await Assert.ThrowsAsync<AlarmValidationException>(() =>
            new AlarmService(repo, new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)))
                .CreateAsync("Consulta", new DateTime(2026, month, day), new TimeSpan(hour, minute, 0), zone));
        Assert.Empty(repo.Alarms);
    }

    [Fact]
    public async Task SameTitleAndScheduleAreAllowedAsSeparateAlarms()
    {
        var repo = new MemoryRepository();
        var service = new AlarmService(repo, new FixedClock(Now));
        var first = await service.CreateAsync("Consulta", Now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc);
        var second = await service.CreateAsync("Consulta", Now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, repo.Alarms.Count);
    }
}

internal sealed class FixedClock(DateTime utc) : TimeProvider
{
    public DateTime Utc { get; set; } = utc;
    public override DateTimeOffset GetUtcNow() => new(Utc);
}

internal sealed class MemoryRepository : IAlarmRepository
{
    public List<Alarm> Alarms { get; } = [];
    public bool FailRead { get; set; }
    public bool FailWrite { get; set; }
    public bool FailUpdate { get; set; }
    public bool FailDelete { get; set; }
    public bool FailComplete { get; set; }
    public int UpdateAttempts { get; private set; }
    public int DeleteAttempts { get; private set; }
    public TaskCompletionSource DeleteStarted { get; } = new();
    public TaskCompletionSource? UpdateGate { get; set; }
    public TaskCompletionSource? DeleteGate { get; set; }
    public TaskCompletionSource? WriteGate { get; set; }
    public int WriteAttempts { get; private set; }
    public async Task InsertAsync(Alarm alarm)
    {
        WriteAttempts++;
        if (WriteGate is not null) await WriteGate.Task;
        if (FailWrite) throw new IOException("Simulated write failure");
        Alarms.Add(alarm);
    }
    public async Task<bool> UpdateAsync(Alarm alarm)
    {
        UpdateAttempts++;
        if (UpdateGate is not null) await UpdateGate.Task;
        if (FailUpdate) throw new IOException("Simulated update failure");
        var index = Alarms.FindIndex(a => a.Id == alarm.Id);
        if (index < 0) return false;
        Alarms[index] = alarm;
        return true;
    }
    public async Task<bool> DeleteAsync(Guid id)
    {
        DeleteAttempts++;
        DeleteStarted.TrySetResult();
        if (DeleteGate is not null) await DeleteGate.Task;
        if (FailDelete) throw new IOException("Simulated delete failure");
        return Alarms.RemoveAll(a => a.Id == id) != 0;
    }
    public Task<bool> CompleteOccurrenceAsync(Alarm expected, DateTime? nextOccurrenceUtc)
    {
        if (FailComplete) throw new IOException("Simulated completion failure");
        var index = Alarms.FindIndex(a => a == expected);
        if (index < 0) return Task.FromResult(false);
        Alarms[index] = expected with { LastDeliveredOccurrenceUtc = expected.DueAtUtc, NextOccurrenceUtc = nextOccurrenceUtc };
        return Task.FromResult(true);
    }
    public Task<IReadOnlyList<Alarm>> ListAsync() => FailRead
        ? Task.FromException<IReadOnlyList<Alarm>>(new IOException("Simulated read failure"))
        : Task.FromResult<IReadOnlyList<Alarm>>(Alarms.ToArray());
}
