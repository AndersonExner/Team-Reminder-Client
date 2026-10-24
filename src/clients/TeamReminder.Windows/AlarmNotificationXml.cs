using System.Globalization;
using System.Security;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public static class AlarmNotificationXml
{
    private const string SnoozeInput = "snooze";

    // scenario='alarm' keeps the notification on screen until the person snoozes or dismisses it.
    public static string Build(AlarmDelivery delivery)
    {
        var alarm = delivery.Alarm;
        var title = SecurityElement.Escape(alarm.Title);
        var local = delivery.OccurrenceUtc.ToLocalTime();
        var day = local.Date == DateTime.Today ? "" : " · " + local.ToString("dd/MM");
        var prefix = delivery.IsSnoozed ? "Alarme adiado" : delivery.IsLate ? "Alarme atrasado" : "Hora do alarme";
        var origin = SecurityElement.Escape(alarm.GroupId is null ? "Pessoal" : $"Grupo {alarm.GroupName}");
        var snooze = SecurityElement.Escape(SnoozeArguments(alarm.Id, delivery.OccurrenceUtc));
        var options = string.Concat(SnoozeScheduler.Options.Select(minutes => $"<selection id='{minutes}' content='{minutes} minutos'/>"));
        return "<toast scenario='alarm' launch='action=open'>"
            + $"<visual><binding template='ToastGeneric'><text>{title}</text><text>{prefix} · {local:HH:mm}{day}</text><text placement='attribution'>{origin}</text></binding></visual>"
            + "<audio silent='true'/>"
            + $"<actions><input id='{SnoozeInput}' type='selection' defaultInput='{SnoozeScheduler.Options[0]}'>{options}</input>"
            + $"<action content='Adiar' arguments='{snooze}' hint-inputId='{SnoozeInput}' activationType='foreground'/>"
            + LinkAction(alarm)
            + "<action content='Dispensar' arguments='dismiss' activationType='system'/></actions></toast>";
    }

    public static string BuildEarly(Alarm alarm, int minutes)
    {
        var title = SecurityElement.Escape(alarm.Title);
        var due = (alarm.DueAtUtc ?? alarm.ScheduledAtUtc).ToLocalTime();
        var origin = SecurityElement.Escape(alarm.GroupId is null ? "Pessoal" : $"Grupo {alarm.GroupName}");
        var lead = minutes > 0 ? $"Em {minutes} min" : "Em breve";
        return "<toast launch='action=open'>"
            + $"<visual><binding template='ToastGeneric'><text>{lead}: {title}</text><text>Às {due:HH:mm} · {origin}</text></binding></visual>"
            + $"<actions>{LinkAction(alarm)}<action content='Abrir Team Reminder' arguments='action=open' activationType='foreground'/></actions></toast>";
    }

    private static string LinkAction(Alarm alarm)
    {
        if (alarm.Link is not { } link || !AlarmLink.TryGetUri(link.Url, out var uri)) return "";
        var label = SecurityElement.Escape(string.IsNullOrWhiteSpace(link.Label) ? "Abrir link" : link.Label);
        return $"<action content='{label}' arguments='{SecurityElement.Escape(uri!.AbsoluteUri)}' activationType='protocol'/>";
    }

    public static string SnoozeArguments(Guid alarmId, DateTime occurrenceUtc) =>
        $"action=snooze;id={alarmId:D};occ={occurrenceUtc.Ticks.ToString(CultureInfo.InvariantCulture)}";

    public static bool TryParseSnooze(string? arguments, IDictionary<string, string>? input, out Guid alarmId, out DateTime occurrenceUtc, out int minutes)
    {
        alarmId = Guid.Empty; occurrenceUtc = default; minutes = SnoozeScheduler.Options[0];
        if (string.IsNullOrEmpty(arguments)) return false;
        var parts = new Dictionary<string, string>();
        foreach (var pair in arguments.Split(';').Select(part => part.Split('=', 2)).Where(pair => pair.Length == 2))
            parts[pair[0]] = pair[1];
        if (!parts.TryGetValue("action", out var action) || action != "snooze") return false;
        if (!parts.TryGetValue("id", out var id) || !Guid.TryParse(id, out alarmId)) return false;
        if (!parts.TryGetValue("occ", out var occ) || !long.TryParse(occ, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks > DateTime.MaxValue.Ticks) return false;
        occurrenceUtc = new DateTime(ticks, DateTimeKind.Utc);
        // Only the offered choices are accepted; anything else falls back to the shortest one.
        if (input is not null && input.TryGetValue(SnoozeInput, out var chosen) && int.TryParse(chosen, out var value)
            && SnoozeScheduler.Options.Contains(value)) minutes = value;
        return true;
    }
}
