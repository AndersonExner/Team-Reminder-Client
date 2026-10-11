# Team-Reminder-Client

Cliente Windows do produto Team Reminder, com C#, WinUI 3, MVVM e LiteDB.

## Estado atual

Criar, listar, editar e excluir alarmes locais; emitir som e aviso no horário escolhido;
repetir uma vez, todos os dias ou em dias da semana selecionados.
Novo alarme abre um modal com data e hora atuais. Editar usa o mesmo modal com os dados salvos.

Abas Alarmes e Configurações organizam o aplicativo. Em Configurações > Aparência,
escolha Claro, Escuro ou Padrão do Windows. Claro é o padrão sem preferência válida; escolhas salvas são respeitadas. A preferência fica em %LOCALAPPDATA%\TeamReminder\Settings\theme.txt. O cadastro fica em um modal com rolagem.

Build x64 verificado e 99 testes aprovados. Um disparo real no executável Windows
confirmou áudio aceito, notificação aceita, persistência e reinício sem repetição.
A validação visual completa e a confirmação do áudio audível ainda estão pendentes.

## Executar

Requisitos: Windows 10 build 19041 ou superior, x64; .NET SDK 10.0.401 para desenvolver.
O Windows App SDK acompanha o build, mas em builds de desenvolvimento o .NET Desktop Runtime 10 continua necessário. A publicação self-contained inclui os runtimes.
Internet é necessária para restaurar os pacotes NuGet na primeira compilação.

```powershell
# Na raiz do repositório
 dotnet build TeamReminder.Client.sln -p:Platform=x64
 dotnet run --project src/clients/TeamReminder.Windows -p:Platform=x64
```

Ou abra TeamReminder.Client.sln no Visual Studio com suporte a .NET 10/WinUI,
selecione Debug/x64 e TeamReminder.Windows como projeto de inicialização.
Encerre pelo menu Sair da bandeja antes de compilar novamente, para evitar DLLs bloqueadas.

## Usar os alarmes

- Novo alarme: abre o modal com data e HH:mm atuais; Salvar fecha somente após sucesso. Cancelar/Esc descarta o rascunho.
- Uma vez: ajuste o horário atual preenchido para um horário futuro.
- Todos os dias: repete no mesmo horário; se o horário de hoje passou, começa amanhã.
- Dias da semana: marque ao menos um dia; por exemplo, segunda/quarta/sexta.
- Data inicial: a partir dessa data será escolhida a primeira ocorrência futura elegível.
- Editar mantém Id e criação, reprograma a próxima ocorrência e restaura a regra/dias.
- Excluir solicita confirmação; Cancelar mantém o alarme.

Fechar a janela mantém o app ativo na bandeja. Use Sair no menu do ícone para encerrar os alarmes.
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

84 testes de regras/ViewModel/calendário/motores e 15 de persistência real.
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
Adiamento e recorrências adicionais continuam fora desta entrega.

### Inspecionar o LiteDB em Debug

Builds Debug usam Connection=Shared, permitindo abrir o arquivo enquanto o app está em execução.
A ferramenta externa também deve usar Connection=Shared. Exemplo de connection string:

```text
Filename=C:\Users\anderson.exner\AppData\Local\TeamReminder\Data\team-reminder.db;Connection=Shared
```

Reinicie o aplicativo após recompilar para aplicar esse modo. Builds Release usam Connection=Direct.


[Spec 004 — Temas e layout](docs/specs/004-themes-layout/spec.md) · [Checklist visual](docs/specs/004-themes-layout/manual-checklist.md).

## Bandeja e inicialização com Windows

- Fechar a janela mantém execução na bandeja; Sair encerra o processo.
- Hover mostra resumo textual; clique abre próximos alarmes e ações Adicionar, Abrir, Configurações e Sair.
- Duplo clique ou abrir novamente o executável restaura a janela existente.
- Configurações > Inicialização permite iniciar com Windows. Padrão desativado; ao habilitar,
  registra o executável atual em HKCU/Software/Microsoft/Windows/CurrentVersion/Run com --tray.
  Em desenvolvimento, essa opção aponta para o build atual; em instalação, para o exe instalado.
- --tray carrega alarmes sem mostrar janela. Windows pode colocar o ícone em ícones ocultos.
- Windows desligado ou suspenso não executa alarmes; atrasos são recuperados ao retomar.

Para verificar a execução oculta e restauração por segunda instância:

```powershell
./scripts/Test-AlarmDelivery.ps1 -StartInTray
./scripts/Test-AlarmDelivery.ps1 -CloseToTray
```

## Gerar instalador para distribuição

Requisitos no computador de desenvolvimento: .NET SDK 10.0.401 e
[Inno Setup 6](https://jrsoftware.org/isinfo.php) instalado.

No PowerShell, execute:

```powershell
cd E:\ProjetosPessoais\Team-Reminder-Client
.\scripts\Build-Installer.ps1
```

O script compila a versão **Release/x64**, inclui os runtimes .NET e Windows App SDK
e gera o instalador:

```text
E:\ProjetosPessoais\Team-Reminder-Client\artifacts\installer\TeamReminder-Setup-0.1.0-x64.exe
```

Execute o mesmo comando após atualizar o código. Quando a compilação termina com sucesso,
o instalador anterior é substituído pela nova compilação.

O script procura o compilador em `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`.
Se o Inno Setup estiver em outra pasta, informe o caminho:

```powershell
.\scripts\Build-Installer.ps1 -CompilerPath 'C:\caminho\ISCC.exe'
```

### Instruções para quem vai testar

Envie somente `TeamReminder-Setup-0.1.0-x64.exe`. Quem receber não precisa instalar
.NET, Inno Setup ou ferramentas de desenvolvimento.
Requisito: Windows 10 build 19041 ou superior, x64, incluindo Windows 11.

1. Execute o instalador e siga as etapas.
2. Se desejar, marque **Iniciar com o Windows**, desmarcado por padrão.
3. Abra **Team Reminder** pelo atalho no menu Iniciar.
4. Crie um alarme e ajuste seu horário para um momento futuro.
5. Para encerrar, use **Sair** no menu do ícone da bandeja.
   Fechar a janela mantém os alarmes ativos em segundo plano.

A instalação é por usuário, sem exigir administrador, em
`%LOCALAPPDATA%\Programs\TeamReminder`, e inclui desinstalador.
Os alarmes ficam em `%LOCALAPPDATA%\TeamReminder\Data\team-reminder.db`
no perfil de quem testa. O instalador não inclui os alarmes ou configurações do desenvolvedor.
Desinstalar remove a inicialização automática e os arquivos instalados,
preservando o banco de alarmes e as preferências locais.

Geração do instalador validada com Inno Setup 6.7.3; instalação/desinstalação
 ainda pendentes de teste manual.

[Spec 005 — Bandeja e startup](docs/specs/005-tray-startup/spec.md) · [Checklist](docs/specs/005-tray-startup/manual-checklist.md).


[Spec 006 — Cadastro em modal](docs/specs/006-alarm-modal/spec.md) · [Checklist](docs/specs/006-alarm-modal/manual-checklist.md).

## Lembrete antecipado

Em Configurações > Lembrete antecipado, escolha Desativado, 5, 10, 15, 30 ou 60 minutos.
A preferência vale para todos os alarmes deste computador e fica em
%LOCALAPPDATA%/TeamReminder/Settings/advance-minutes.txt. Padrão: Desativado.
Exemplo: alarme 15:30 com 10 minutos de antecedência solicita aviso às 15:20.
O popup da bandeja mostra título/horário, fecha automaticamente e abre o app ao clicar.
Windows controla duração, apresentação e sons do sistema; o pré-aviso não toca o som próprio do alarme.

Recibos na coleção earlyReminderReceipts da mesma instância LiteDB evitam repetir uma ocorrência
após reiniciar. O recibo representa pedido ao componente de bandeja, não confirmação de leitura.
Retomar/criar dentro da janela de antecedência avisa uma vez enquanto o horário ainda é futuro.
No horário ou depois dele, o motor emite somente o alarme principal. Recorrentes têm aviso por ocorrência.
Essa preferência não altera horário, recorrência ou o disparo principal.

[Spec 007 — Avisos antecipados](docs/specs/007-advance-reminders/spec.md) · [Checklist](docs/specs/007-advance-reminders/manual-checklist.md).
