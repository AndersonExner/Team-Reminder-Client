# Team-Reminder-Client

Cliente Windows do produto Team Reminder, com C#, WinUI 3, MVVM e LiteDB.

## Estado atual

Criar, listar, editar e excluir alarmes locais; emitir som e aviso no horário escolhido;
repetir uma vez, todos os dias ou em dias da semana selecionados.
O cadastro começa com a data atual e horário ainda sem preenchimento.

Build x64 verificado e 82 testes aprovados. Um disparo real no executável Windows
confirmou áudio aceito, notificação aceita, persistência e reinício sem repetição.
A validação visual completa e a confirmação do áudio audível ainda estão pendentes.

## Executar

Requisitos: Windows 10 build 19041 ou superior, x64; .NET SDK 10.0.401 para desenvolver.
O Windows App SDK acompanha o build, mas o runtime .NET 10 continua necessário.
Internet é necessária para restaurar os pacotes NuGet na primeira compilação.

```powershell
# Na raiz do repositório
 dotnet build TeamReminder.Client.sln -p:Platform=x64
 dotnet run --project src/clients/TeamReminder.Windows -p:Platform=x64
```

Ou abra TeamReminder.Client.sln no Visual Studio com suporte a .NET 10/WinUI,
selecione Debug/x64 e TeamReminder.Windows como projeto de inicialização.
Feche o aplicativo antes de compilar novamente, para evitar DLLs bloqueadas.

## Usar os alarmes

- Uma vez: escolha um horário futuro.
- Todos os dias: repete no mesmo horário; se o horário de hoje passou, começa amanhã.
- Dias da semana: marque ao menos um dia; por exemplo, segunda/quarta/sexta.
- Data inicial: a partir dessa data será escolhida a primeira ocorrência futura elegível.
- Editar mantém Id e criação, reprograma a próxima ocorrência e restaura a regra/dias.
- Excluir solicita confirmação; Cancelar mantém o alarme.

O aplicativo precisa estar aberto, inclusive minimizado, para tocar.
Fechar a janela encerra o motor: System Tray e inicialização automática ainda serão implementados.
Ao reabrir ou retomar após suspensão, um alarme atrasado emite um aviso;
recorrentes avançam para a próxima ocorrência futura, sem reproduzir todos os atrasados.

O alerta inclui som curto, aviso na janela e notificação Windows.
Se o canal Windows estiver indisponível, o aplicativo informa e mantém som/aviso local.
O Windows controla volume, mudo e permissões de notificação.

## Dados locais

```text
%LOCALAPPDATA%\TeamReminder\Data\team-reminder.db
```

Uma instância de LiteDatabase atende o processo. Registros antigos são preservados e
interpretados como Uma vez. Ocorrências entregues e a próxima data ficam no banco.
Os horários são armazenados em UTC; a recorrência usa o fuso salvo e a lista exibe o fuso local atual.
LiteDB é a fonte oficial no MVP local. O servidor terá repositório próprio na fase cloud.

## Testes

```powershell
 dotnet test tests/TeamReminder.Core.Tests
 dotnet test tests/TeamReminder.Local.Tests
```

69 testes de regras/ViewModel/calendário/motor e 13 de persistência real.
Os testes usam relógio controlado e bancos temporários; não alteram alarmes do usuário.

Para verificar envio de áudio/notificação, persistência e reinício no executável Debug/x64,
com o app fechado e PowerShell 7.4 ou superior:

```powershell
 ./scripts/Test-AlarmDelivery.ps1
```

Esse teste emite um aviso real com som, usando banco isolado em artifacts/smoke.
Os dois parâmetros de ambiente de diagnóstico utilizados pelo script só existem no build Debug.
Aceitação da API de áudio não comprova que o volume do dispositivo esteja audível.

## Organização e SDD

- src/core/TeamReminder.Core: modelos, validação, MVVM, calendário e motor independente de WinUI.
- src/infrastructure/TeamReminder.Local: persistência LiteDB.
- src/clients/TeamReminder.Windows: interface, som e notificação Windows.
- tests/: regras e persistência; scripts/: verificação do executável.

[Visão do MVP](docs/product.md) · [Guia SDD](docs/development.md)

| Entrega | Especificação | Plano | Evidências |
| --- | --- | --- | --- |
| 001 - cadastro/persistência | [spec](docs/specs/001-local-alarms/spec.md) | [plan](docs/specs/001-local-alarms/plan.md) | [tasks](docs/specs/001-local-alarms/tasks.md) |
| 002 - edição/exclusão | [spec](docs/specs/002-edit-delete-alarms/spec.md) | [plan](docs/specs/002-edit-delete-alarms/plan.md) | [tasks](docs/specs/002-edit-delete-alarms/tasks.md) |
| 003 - disparo/recorrência | [spec](docs/specs/003-alerts-recurrence/spec.md) | [plan](docs/specs/003-alerts-recurrence/plan.md) | [tasks](docs/specs/003-alerts-recurrence/tasks.md) |

[Checklist manual atual](docs/specs/003-alerts-recurrence/manual-checklist.md).
Antecedência, adiamento, System Tray e recorrências adicionais continuam fora desta entrega.

### Inspecionar o LiteDB em Debug

Builds Debug usam Connection=Shared, permitindo abrir o arquivo enquanto o app está em execução.
A ferramenta externa também deve usar Connection=Shared. Exemplo de connection string:

```text
Filename=C:\Users\anderson.exner\AppData\Local\TeamReminder\Data\team-reminder.db;Connection=Shared
```

Reinicie o aplicativo após recompilar para aplicar esse modo. Builds Release usam Connection=Direct.
