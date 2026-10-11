namespace TeamReminder.Core;

public enum AlarmSoundKind
{
    Default = 0, Notification = 1, Exclamation = 2, Critical = 3, Silent = 4, Custom = 5
}

public sealed record AlarmSound(AlarmSoundKind Kind, Guid? FileId = null, string? FileName = null)
{
    public static AlarmSound Default { get; } = new(AlarmSoundKind.Default);
    public static AlarmSound InitialDefault { get; } = new(AlarmSoundKind.Notification);
    public bool IsValid => Enum.IsDefined(Kind) && (Kind == AlarmSoundKind.Custom
        ? FileId is { } id && id != Guid.Empty && !string.IsNullOrWhiteSpace(FileName)
        : FileId is null && FileName is null);
    public string Label => Kind switch
    {
        AlarmSoundKind.Default => "Padrão do aplicativo",
        AlarmSoundKind.Notification => "Notificação do Windows",
        AlarmSoundKind.Exclamation => "Exclamação do Windows",
        AlarmSoundKind.Critical => "Erro crítico do Windows",
        AlarmSoundKind.Silent => "Sem som",
        AlarmSoundKind.Custom => FileName ?? "Selecione um arquivo",
        _ => "Som indisponível"
    };
    public void Validate()
    {
        if (!IsValid) throw new AlarmValidationException("Sound", "Selecione um som válido ou um arquivo MP3/WAV.");
    }
}
