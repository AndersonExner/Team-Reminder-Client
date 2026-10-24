# Distribuição Windows

Builds Release e Debug usam https://team-reminder-api.onrender.com/ por padrão. Para a API local, defina TEAMREMINDER_API_URL=http://localhost:5080/.

Para gerar um instalador local: ./scripts/Build-Installer.ps1 -Version 0.2.0
Requer .NET SDK 10, ferramentas WinUI e Inno Setup 6.

Após integrar o workflow na main, crie uma tag vMAJOR.MINOR.PATCH no commit que deseja distribuir e envie ao GitHub. O workflow windows-release.yml compila o pacote self-contained e publica TeamReminder-Setup-VERSAO-x64.exe em GitHub Releases. Apenas tags sem sufixo são aceitas.

A página da API consulta a última release estável pública e oferece /download/windows. O repositório e a release precisam ser públicos. Pode levar até cinco minutos para a nova versão aparecer devido ao cache.

Instalação por usuário em Windows 10 2004 ou superior, x64. A assinatura digital do instalador não está configurada.

Erros inesperados do aplicativo são registrados em %LOCALAPPDATA%\TeamReminder\Logs\errors.log.

As releases incluem release.json com versão, data, endereço e SHA-256 do instalador, além do arquivo .sha256 para conferência (Get-FileHash). O servidor lê esse asset público pela URL releases/latest/download/release.json, evitando limites da API anônima do GitHub em IPs compartilhados.

## Atualização automática

A partir da 0.5.0, a versão instalada consulta releases/latest/download/release.json dois minutos após iniciar e a cada seis horas. Uma versão estável mais nova, baixada de releases/download deste repositório, só é aceita se o SHA-256 conferir com o publicado no manifesto. O instalador fica em %LOCALAPPDATA%\TeamReminder\Updates.

A instalação silenciosa (/VERYSILENT) acontece sozinha apenas com o app na bandeja, sem edição em andamento e sem alarme nos próximos 10 minutos; o app reabre na bandeja ao terminar. Com a janela aberta, Configurações → Sistema mostra Atualizar agora. A opção pode ser desligada na mesma seção. Builds de desenvolvimento, sem unins000.exe, não se atualizam.

O instalador não é assinado: quem publica uma release neste repositório distribui código para todos os clientes. Mantenha 2FA na conta e restrinja a criação de tags v*.
