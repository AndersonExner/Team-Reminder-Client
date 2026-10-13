using System.Security;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public static class AlarmNotificationXml
{
    public static string Build(AlarmDelivery delivery)
    {
        var title = SecurityElement.Escape(delivery.Alarm.Title);
        var schedule = delivery.OccurrenceUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        var prefix = delivery.IsLate ? "Alarme atrasado" : "Hora do alarme";
        var actions = "";
        if (delivery.Alarm.Link is { } link && AlarmLink.TryGetUri(link.Url, out var uri))
        {
            var label = SecurityElement.Escape(string.IsNullOrWhiteSpace(link.Label) ? "Abrir link" : link.Label);
            var url = SecurityElement.Escape(uri!.AbsoluteUri);
            actions = $"<actions><action content='{label}' arguments='{url}' activationType='protocol'/></actions>";
        }
        return $"<toast><visual><binding template='ToastGeneric'><text>Team Reminder</text><text>{title}</text><text>{prefix} — {schedule}</text></binding></visual><audio silent='true'/>{actions}</toast>";
    }
}
