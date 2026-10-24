using System.Xml.Linq;
using TeamReminder.Core;
using TeamReminder.Windows;

namespace TeamReminder.Core.Tests;

public class AlarmNotificationXmlTests
{
    private static XElement? Protocol(XDocument xml) =>
        xml.Descendants("action").SingleOrDefault(action => action.Attribute("activationType")?.Value == "protocol");

    [Fact]
    public void ProtocolButtonPreservesQueryFragmentAndEscapesXmlWithoutChangingBodyActivation()
    {
        var now = DateTime.UtcNow;
        var link = AlarmLink.Create("https://example.com/ponto?a=1&b=2#fim", "Bater 'ponto' & <abrir>")!;
        var alarm = new Alarm(Guid.NewGuid(), "Ponto <empresa> & amigos", now, now, "UTC") { Link = link };
        var xml = XDocument.Parse(AlarmNotificationXml.Build(new AlarmDelivery(alarm, now, false)));
        var button = Protocol(xml)!;
        Assert.Equal(link.Url, button.Attribute("arguments")!.Value);
        Assert.Equal(link.Label, button.Attribute("content")!.Value);
        Assert.Null(xml.Root!.Attribute("activationType"));
        Assert.Equal(alarm.Title, xml.Descendants("text").First().Value);
        Assert.Equal("true", xml.Descendants("audio").Single().Attribute("silent")!.Value);
    }

    [Fact]
    public void MissingAndInvalidStoredLinksNeverCreateProtocolButtons()
    {
        var now = DateTime.UtcNow;
        var alarm = new Alarm(Guid.NewGuid(), "Ponto", now, now, "UTC");
        foreach (var link in new AlarmLink?[] { null, new("javascript:bad", "Bad"), new("file:///C:/bad", "Bad") })
        {
            Assert.Null(Protocol(XDocument.Parse(AlarmNotificationXml.Build(new AlarmDelivery(alarm with { Link = link }, now, false)))));
            Assert.Null(Protocol(XDocument.Parse(AlarmNotificationXml.BuildEarly(alarm with { Link = link }, 10))));
        }
    }

    [Fact]
    public void AlarmStaysOnScreenWithSnoozeChoicesAndSystemDismiss()
    {
        var now = DateTime.UtcNow;
        var alarm = new Alarm(Guid.NewGuid(), "Daily", now, now, "UTC") { GroupId = Guid.NewGuid(), GroupName = "Dev" };
        var xml = XDocument.Parse(AlarmNotificationXml.Build(new AlarmDelivery(alarm, now, false, IsSnoozed: true)));
        Assert.Equal("alarm", xml.Root!.Attribute("scenario")!.Value);
        Assert.Equal(["5", "10", "15"], xml.Descendants("selection").Select(s => s.Attribute("id")!.Value));
        var snooze = xml.Descendants("action").Single(a => a.Attribute("content")!.Value == "Adiar");
        Assert.Equal("snooze", snooze.Attribute("hint-inputId")!.Value);
        Assert.True(AlarmNotificationXml.TryParseSnooze(snooze.Attribute("arguments")!.Value, new Dictionary<string, string> { ["snooze"] = "10" },
            out var id, out var occurrence, out var minutes));
        Assert.Equal((alarm.Id, now, 10), (id, occurrence, minutes));
        Assert.Contains(xml.Descendants("action"), a => a.Attribute("activationType")!.Value == "system" && a.Attribute("arguments")!.Value == "dismiss");
        Assert.StartsWith("Alarme adiado", xml.Descendants("text").ElementAt(1).Value);
        Assert.Equal("Grupo Dev", xml.Descendants("text").Single(t => t.Attribute("placement")?.Value == "attribution").Value);
    }

    [Fact]
    public void SnoozeArgumentsRejectForeignOrTamperedInput()
    {
        var id = Guid.NewGuid();
        var arguments = AlarmNotificationXml.SnoozeArguments(id, new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc));
        Assert.True(AlarmNotificationXml.TryParseSnooze(arguments, new Dictionary<string, string> { ["snooze"] = "999" }, out _, out _, out var minutes));
        Assert.Equal(5, minutes);
        Assert.False(AlarmNotificationXml.TryParseSnooze("action=open", null, out _, out _, out _));
        Assert.False(AlarmNotificationXml.TryParseSnooze("dismiss", null, out _, out _, out _));
        Assert.False(AlarmNotificationXml.TryParseSnooze($"action=snooze;id={id};occ=-5", null, out _, out _, out _));
        Assert.False(AlarmNotificationXml.TryParseSnooze("action=snooze;id=nope;occ=1", null, out _, out _, out _));
        Assert.False(AlarmNotificationXml.TryParseSnooze(null, null, out _, out _, out _));
    }

    [Fact]
    public void EarlyReminderIsARegularNotificationWithOpenAction()
    {
        var now = DateTime.UtcNow;
        var alarm = new Alarm(Guid.NewGuid(), "Reunião <geral>", now.AddMinutes(10), now, "UTC") { NextOccurrenceUtc = now.AddMinutes(10) };
        var xml = XDocument.Parse(AlarmNotificationXml.BuildEarly(alarm, 10));
        Assert.Null(xml.Root!.Attribute("scenario"));
        Assert.Equal("Em 10 min: Reunião <geral>", xml.Descendants("text").First().Value);
        Assert.Contains(xml.Descendants("action"), a => a.Attribute("arguments")!.Value == "action=open");
    }
}
