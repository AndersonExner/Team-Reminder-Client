namespace TeamReminder.Core;

public sealed record AlarmLink(string Url, string Label)
{
    public static AlarmLink? Create(string? url, string? label)
    {
        url = url?.Trim(); label = label?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            if (!string.IsNullOrEmpty(label)) throw new AlarmValidationException("Action", "Informe o link ou remova o texto do botão.");
            return null;
        }
        if (!TryGetUri(url, out var uri))
            throw new AlarmValidationException("Action", "Informe um link HTTP/HTTPS válido, com até 2048 caracteres e sem credenciais.");
        if (string.IsNullOrEmpty(label)) label = "Abrir link";
        if (label.Length > 40 || label.Any(char.IsControl))
            throw new AlarmValidationException("Action", "O texto do botão deve ter até 40 caracteres, em uma linha.");
        return new AlarmLink(uri!.AbsoluteUri, label);
    }
    public static bool TryGetUri(string? url, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048 || url.Any(char.IsControl) ||
            !(url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(parsed.Host) || !string.IsNullOrEmpty(parsed.UserInfo) || parsed.AbsoluteUri.Length > 2048) return false;
        uri = parsed; return true;
    }
}
