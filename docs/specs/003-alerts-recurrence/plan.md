# Plano técnico - 003

Status: implementado; testes e disparo no executável aprovados; validação visual completa pendente.

- Estender Alarm com RecurrenceKind, WeekDays, NextOccurrenceUtc e LastDeliveredOccurrenceUtc.
- Preservar compatibilidade com documentos existentes por valores padrão.
- Calculador puro de calendário no Core, usando TimeProvider e TimeZoneInfo.
- Serviço normaliza recorrência e calcula a primeira ocorrência elegível.
- AlarmScheduler e IAlarmNotifier independentes do WinUI para testar envio e repetição.
- Repositório conclui ocorrência sob lock, apenas se o documento ainda corresponde ao snapshot esperado.
- Registrar entrega após aceite do notificador; manter guarda em memória quando a gravação falha.
- ViewModel bloqueia comandos durante uma entrega pendente e preserva campos em edição.
  Ticks sem alarmes vencidos só atualizam apresentação, sem bloquear o formulário.
- DispatcherTimer chama o motor. Aplicativo fechado continua encerrando o processo.
- Notificador Windows: som WAV incluído no build, aviso na janela e AppNotificationManager.
  Notificação do sistema fica sem áudio próprio para não duplicar o som WAV.
- Interface: ComboBox de recorrência, sete CheckBoxes e indicação de próxima ocorrência.
- Atualizar testes anteriores que esperavam data nula após limpar o formulário.
- Testar relógio controlado, atraso, repetição, falhas, calendário e reabertura do banco real.

## Referências

- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/app-notifications-dotnet
- https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.appnotificationmanager.register


## Ajuste técnico - 2026-10-11

LiteDB usa ConnectionType.Shared em Debug para inspeção simultânea em ferramenta externa.
Em Release, usa ConnectionType.Direct. A ferramenta externa também precisa selecionar Shared.
Esse ajuste de configuração não exige uma nova spec de comportamento do produto.
