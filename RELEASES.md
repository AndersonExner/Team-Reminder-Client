# Distribuição Windows

Builds Release usam https://team-reminder-api.onrender.com/ por padrão; Debug usa localhost:5080. TEAMREMINDER_API_URL permite substituir o endereço.

Para gerar um instalador local: ./scripts/Build-Installer.ps1 -Version 0.2.0
Requer .NET SDK 10, ferramentas WinUI e Inno Setup 6.

Após integrar o workflow na main, crie uma tag vMAJOR.MINOR.PATCH no commit que deseja distribuir e envie ao GitHub. O workflow windows-release.yml compila o pacote self-contained e publica TeamReminder-Setup-VERSAO-x64.exe em GitHub Releases. Apenas tags sem sufixo são aceitas.

A página da API consulta a última release estável pública e oferece /download/windows. O repositório e a release precisam ser públicos. Pode levar até cinco minutos para a nova versão aparecer devido ao cache.

Instalação por usuário em Windows 10 2004 ou superior, x64. A assinatura digital do instalador não está configurada.

Erros inesperados do aplicativo são registrados em %LOCALAPPDATA%\TeamReminder\Logs\errors.log.

As releases incluem release.json com versão, data, endereço e SHA-256 do instalador, além do arquivo .sha256 para conferência (Get-FileHash). O servidor lê esse asset público pela URL releases/latest/download/release.json, evitando limites da API anônima do GitHub em IPs compartilhados.
