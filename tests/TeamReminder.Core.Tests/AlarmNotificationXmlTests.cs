using System.Xml.Linq;
using TeamReminder.Core;
using TeamReminder.Windows;

namespace TeamReminder.Core.Tests;

public class AlarmNotificationXmlTests
{
    [Fact]
    public void ProtocolButtonPreservesQueryFragmentAndEscapesXmlWithoutChangingBodyActivation()
    {
        var now = DateTime.UtcNow;
        var link = AlarmLink.Create("https://example.com/ponto?a=1&b=2#fim", "Bater 'ponto' & <abrir>")!;
        var alarm = new Alarm(Guid.NewGuid(), "Ponto <empresa> & amigos", now, now, "UTC") { Link = link };
        var xml = XDocument.Parse(AlarmNotificationXml.Build(new AlarmDelivery(alarm, now, false)));
        var button = Assert.Single(xml.Descendants("action"));
        Assert.Equal("protocol", button.Attribute("activationType")!.Value);
        Assert.Equal(link.Url, button.Attribute("arguments")!.Value);
        Assert.Equal(link.Label, button.Attribute("content")!.Value);
        Assert.Null(xml.Root!.Attribute("activationType"));
        Assert.Equal(alarm.Title, xml.Descendants("text").ElementAt(1).Value);
        Assert.Equal("true", xml.Descendants("audio").Single().Attribute("silent")!.Value);
    }

    [Fact]
    public void MissingAndInvalidStoredLinksNeverCreateProtocolButtons()
    {
        var now = DateTime.UtcNow;
        var alarm = new Alarm(Guid.NewGuid(), "Ponto", now, now, "UTC");
        foreach (var link in new AlarmLink?[] { null, new("javascript:bad", "Bad"), new("file:///C:/bad", "Bad") })
            Assert.Empty(XDocument.Parse(AlarmNotificationXml.Build(new AlarmDelivery(alarm with { Link = link }, now, false))).Descendants("action"));
    }
}
