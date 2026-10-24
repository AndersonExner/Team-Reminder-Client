#if DEBUG
using System.Text.Json;
using Microsoft.Windows.AppNotifications;
using TeamReminder.Core;
namespace TeamReminder.Windows;

// Shows the real alarm and early-reminder notifications (without sound) against isolated test data,
// reports whether Windows accepted them and removes them again.
internal static class ToastSmokeProbe
{
    public static async Task RunAsync(string reportPath)
    {
        try
        {
            var now = DateTime.UtcNow;
            var alarm = new Alarm(Guid.NewGuid(), "Daily da equipe", now, now, TimeZoneInfo.Local.Id)
            {
                GroupId = Guid.NewGuid(), GroupName = "Desenvolvimento", NextOccurrenceUtc = now.AddMinutes(10),
                Link = AlarmLink.Create("https://meet.google.com/abc-defg-hij", "Abrir reunião")
            };
            var main = new AppNotification(AlarmNotificationXml.Build(new AlarmDelivery(alarm, now, false)));
            AppNotificationManager.Default.Show(main);
            await Task.Delay(int.TryParse(Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_TOAST_SECONDS"), out var seconds) ? seconds * 1000 : 6000);
            var early = new AppNotification(AlarmNotificationXml.BuildEarly(alarm, 10));
            AppNotificationManager.Default.Show(early);
            await Task.Delay(1500);
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { Success = main.Id != 0 && early.Id != 0, Alarm = main.Id, Early = early.Id }));
            await AppNotificationManager.Default.RemoveAllAsync();
        }
        catch (Exception ex) { await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { Success = false, Error = ex.ToString() })); }
    }
}
#endif
