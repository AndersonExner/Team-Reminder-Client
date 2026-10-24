using TeamReminder.Core;
using Forms = System.Windows.Forms;

namespace TeamReminder.Windows;

internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly System.Drawing.Icon artwork;
    private readonly AlarmsViewModel viewModel;
    private readonly Action open, add, settings, exit;

    public TrayIcon(AlarmsViewModel viewModel, Action open, Action add, Action settings, Action exit)
    {
        this.viewModel = viewModel;
        this.open = open; this.add = add; this.settings = settings; this.exit = exit;
        artwork = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        icon = new Forms.NotifyIcon { Icon = artwork, Text = "Team Reminder", ContextMenuStrip = menu };
        try
        {
            menu.Font = new System.Drawing.Font("Segoe UI", 9.5f);
            menu.ShowImageMargin = false;
            menu.Padding = new Forms.Padding(0, 4, 0, 4);
            menu.Opening += (_, _) => RebuildMenu();
            icon.MouseClick += (_, e) =>
            {
                if (e.Button != Forms.MouseButtons.Left) return;
                RebuildMenu();
                menu.Show(Forms.Cursor.Position);
            };
            icon.BalloonTipClicked += (_, _) => open();
            icon.DoubleClick += (_, _) => { menu.Close(); open(); };
            Update();
            icon.Visible = true;
        }
        catch { Dispose(); throw; }
    }

    private AlarmRow[] Upcoming() => viewModel.Alarms
        .Where(row => row.Alarm.DueAtUtc is not null)
        .OrderBy(row => row.Alarm.DueAtUtc).Take(3).ToArray();

    public void ShowEarlyReminder(Alarm alarm)
    {
        if (alarm.DueAtUtc is not { } due) return;
        icon.ShowBalloonTip(5000, "Alarme em breve",
            $"{ShortTitle(alarm.Title, 120)}\nÀs {due.ToLocalTime():dd/MM HH:mm}", Forms.ToolTipIcon.Info);
    }

    public void Update()
    {
        var next = Upcoming();
        var text = "Team Reminder\n" + (next.Length == 0 ? "Nenhum alarme agendado"
            : string.Join("\n", next.Select(row => $"{row.Alarm.DueAtUtc!.Value.ToLocalTime():dd/MM HH:mm} · {ShortTitle(row.Title, 22)}")));
        // Shell tooltips have room for 128 UTF-16 characters including terminator.
        icon.Text = text.Length > 127 ? text[..127] : text;
    }

    private static string ShortTitle(string title, int length)
    {
        var cleaned = title.Replace('\r', ' ').Replace('\n', ' ');
        return cleaned.Length <= length ? cleaned : cleaned[..(length - 1)] + "…";
    }

    private void RebuildMenu()
    {
        foreach (Forms.ToolStripItem item in menu.Items.Cast<Forms.ToolStripItem>().ToArray()) item.Dispose();
        menu.Items.Clear();
        // Follows the taskbar theme each time it opens, so a theme change needs no restart.
        menu.Renderer = new TrayMenuRenderer(TrayMenuRenderer.SystemUsesDarkTheme());
        TrayMenuRenderer.RoundCorners(menu);
        var header = new Forms.ToolStripMenuItem("Próximos alarmes") { Enabled = false };
        header.Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold);
        Add(header);
        var next = Upcoming();
        if (next.Length == 0) Add(new Forms.ToolStripMenuItem("Nenhum alarme agendado") { Enabled = false });
        foreach (var row in next)
            Add(new Forms.ToolStripMenuItem($"{row.DayText} {row.TimeText}   {ShortTitle(row.Title, 44)}", null, (_, _) => open()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        Add(new Forms.ToolStripMenuItem("Novo alarme", null, (_, _) => add()));
        Add(new Forms.ToolStripMenuItem("Abrir Team Reminder", null, (_, _) => open()));
        Add(new Forms.ToolStripMenuItem("Configurações", null, (_, _) => settings()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        Add(new Forms.ToolStripMenuItem("Sair (encerrar alarmes)", null, (_, _) => exit()));
    }

    private void Add(Forms.ToolStripMenuItem item)
    {
        item.Padding = new Forms.Padding(8, 6, 16, 6);
        menu.Items.Add(item);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose(); menu.Dispose(); artwork.Dispose();
    }
}
