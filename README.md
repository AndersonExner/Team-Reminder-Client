# Team-Reminder-Client

Cliente Windows do produto Team Reminder, com C#, WinUI 3, MVVM e LiteDB.

## Estado atual

Criar, listar, editar e excluir alarmes locais; emitir som e aviso no horário escolhido;
repetir uma vez, todos os dias ou em dias da semana selecionados.
Novo alarme abre um modal com data e hora atuais. Editar usa o mesmo modal com os dados salvos.

A partir da 0.6.0, a interface segue o estilo do Windows 11: fundo Mica, barra de título integrada e
navegação lateral com **Início** (saudação, próximo alarme em destaque, números do dia e alarmes de hoje),
**Alarmes** (cartões agrupados em Hoje, Amanhã, Próximos 7 dias, Mais tarde e Já disparados, com busca e
menu de ações por alarme), **Grupos** e **Configurações** (cartões por seção: Conta, Notificações,
Aparência, Sistema e Sobre). O editor oferece atalhos (Em 15 min, Em 30 min, Em 1 h, Amanhã 9:00), repetição
por botões de opção e dias da semana como chips.

As notificações de alarme usam o modo alarme do Windows: ficam na tela até serem adiadas ou dispensadas,
com seletor de 5, 10 ou 15 minutos e o botão do link, quando houver. Alarmes adiados são guardados em
%LOCALAPPDATA%\TeamReminder\Settings\snoozes.json e tocam novamente mesmo depois de reiniciar o app; o
alarme recebido na tela Início também pode ser adiado. Configurações > Notificações > Testar notificação
envia um exemplo.

Em Configurações > Aparência,
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

## Conta e servidor

Em **Configurações > Minha conta**, entre com e-mail e senha ou ative **Criar uma nova conta** para informar nome, e-mail, senha e confirmação. O cadastro já conecta a conta. A senha deve ter de 10 a 128 caracteres. Sem login, o aplicativo usa os alarmes locais originais; com login, usa o cache separado da conta. Ao sair, os alarmes da conta ficam preservados para o próximo login e o aplicativo volta aos alarmes locais.

A API é desenvolvida no repositório `E:\ProjetosPessoais\Team-Reminder-Server`. Inicie o Docker Compose daquele repositório antes de testar cadastro e login. Por padrão, inclusive em Debug, o aplicativo usa a API hospedada (`https://team-reminder-api.onrender.com/`). Para usar a API local (`http://localhost:5080/`) ou outra instalação, configure `TEAMREMINDER_API_URL` antes de abrir o aplicativo:

```powershell
$env:TEAMREMINDER_API_URL = 'https://seu-servidor.example.com/'
dotnet run --project src/clients/TeamReminder.Windows -p:Platform=x64
```

HTTP é aceito somente em loopback. A sessão é salva em `%LOCALAPPDATA%\TeamReminder\Account\session.bin`, protegida para o usuário atual pelo Windows e vinculada ao endereço da API. Nenhuma senha é armazenada. Ao iniciar, o aplicativo carrega o cache da sessão salva antes de consultar a rede e renova os tokens quando necessário. Se o servidor estiver indisponível, é possível tentar reconectar, e o agendamento em cache segue funcionando. Logout sem conexão limpa a sessão local e informa que não conseguiu revogá-la no servidor.

### Sincronização dos alarmes pessoais

- Alarmes criados, editados ou excluídos na conta são gravados primeiro no cache local e em uma fila persistente. O disparo não depende da rede.
- O aplicativo sincroniza após conectar e a cada minuto enquanto estiver aberto, inclusive na bandeja. **Sincronizar agora** permite antecipar a consulta. A sincronização espera o fechamento do editor antes de aplicar dados remotos.
- Cada combinação de servidor e usuário tem um banco em `%LOCALAPPDATA%\TeamReminder\Data\Accounts\`. Trocar de conta ou servidor não mistura cache, fila, cursor ou registros de disparo. Sem sessão válida, o aplicativo volta aos alarmes locais; os caches das contas permanecem no disco. Os arquivos LiteDB não são criptografados: o isolamento é funcional dentro do app, não uma barreira contra acesso direto ao disco.
- **Adicionar meus alarmes locais à conta** exige confirmação e copia os alarmes existentes sem apagar os originais. Repetir a importação não duplica IDs já presentes na conta.
- Alterações simultâneas são preservadas até uma escolha em **Revisar alterações simultâneas**: usar a versão do servidor ou manter/enviar a edição local. Conflitos não sobrescrevem silenciosamente os dados. A versão local continua agendada enquanto aguarda revisão.
- Exclusões são propagadas como registros de exclusão. As alterações têm versão e identificador de operação para permitir nova tentativa após falha de rede sem duplicar alarmes.
- Próxima ocorrência e registro de disparo são locais a cada dispositivo. Atualizar apenas título/link/som não repete uma ocorrência já concluída nesse computador. Cada computador ativo pode notificar a mesma ocorrência; não há eleição de dispositivo único.
- Um alarme recebido pela primeira vez com atraso superior a cinco minutos fica no histórico sem disparar; recorrentes recebidos com data inicial passada avançam à próxima ocorrência futura. Alarmes já em cache mantêm o comportamento de atraso existente.
- Sons personalizados e seus arquivos ficam no computador. No servidor, usam o padrão do aplicativo; outro computador recebe o som padrão. O arquivo personalizado existente permanece aplicado no dispositivo de origem.
- A API limita cada conta a 1000 alarmes ativos; exclusões mantidas para sincronização não contam para esse limite. A gestão de grupos está disponível na página Grupos; alarmes compartilhados, organizações e sincronização de arquivos de áudio ficam para as próximas etapas.

Validação desta etapa: 112 testes do núcleo e 43 testes de armazenamento local, além do fluxo entre dois caches usando a API e PostgreSQL reais. A inspeção visual completa da interface ainda está pendente.

## Grupos e convites

Abra **Grupos** depois de entrar na conta. A aba consulta seus grupos e convites ao ser aberta; **Atualizar grupos e convites** busca mudanças feitas por outros participantes. Estas operações exigem conexão e não entram na fila offline dos alarmes pessoais.

1. **Criar grupo** pede nome (até 100 caracteres) e descrição opcional (até 500). O criador se torna proprietário.
2. Selecione o grupo e informe o e-mail de uma conta já cadastrada para **Enviar convite**. O convite aparece no aplicativo do destinatário, tem validade de sete dias e exige aceite. Esta versão não envia e-mails.
3. Em **Convites recebidos**, selecione o convite para aceitar ou recusar. Quem aceita entra como membro. Aceitar novamente não duplica a participação.
4. Proprietários e administradores podem editar o grupo, convidar, cancelar convites, remover participantes e alterar membros para administradores ou administradores para membros. O proprietário não pode ser removido nem rebaixado por esse caminho.
5. O proprietário pode **Transferir propriedade** a um participante selecionado; ele próprio passa a administrador. É obrigatório transferir antes de sair. Apenas o proprietário pode excluir o grupo.
6. Remover um administrador, rebaixá-lo ou permitir sua saída cancela os convites pendentes enviados por ele. Um convite já aceito não pode ser reutilizado para voltar após remoção ou saída; é necessário novo convite.
7. **Consultar histórico** mostra as últimas 50 ações administrativas aos administradores e ao proprietário. Membros consultam os participantes, mas não o histórico nem os convites enviados.

A interface apresenta ações conforme o papel; a API valida novamente a permissão a cada requisição. Se outro usuário alterar o grupo, uma operação baseada em versão antiga é recusada e pede atualização. Trocar de conta limpa os grupos e participantes exibidos.

Limites iniciais: até 20 grupos como proprietário, 100 grupos por participante, 100 participantes por grupo e 100 convites pendentes por grupo/destinatário. Excluir um grupo o torna indisponível para todos e cancela seus convites.

Esta entrega prepara grupos, participação e permissões. Publicação e recebimento de alarmes compartilhados pelo grupo são a próxima etapa; os alarmes pessoais continuam com o fluxo de sincronização existente.

Validação: build WinUI x64 sem erros; 118 testes do núcleo e 43 de armazenamento local, além do fluxo integrado usando o cliente HTTP real com proprietário, administrador e membro. A inspeção visual completa da nova aba ainda está pendente.

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

O alerta inclui o som escolhido, aviso na janela e notificação Windows.
Se o canal Windows estiver indisponível, o aplicativo informa e mantém som/aviso local.
O Windows controla volume, mudo e permissões de notificação.

## Ação de abrir link

Ao criar ou editar um alarme, preencha **Link (opcional)** e **Texto do botão**.
Por exemplo: alarme **Bater ponto**, link do sistema da empresa e botão **Bater ponto**.
Se deixar o texto vazio, o botão será **Abrir link**. Para remover a ação, limpe os dois campos.

Aceita URLs completas HTTP/HTTPS, inclusive intranet, parâmetros e fragmentos; não aceita
arquivos, comandos, outros protocolos ou credenciais no endereço.
O link só abre ao clicar no botão, no navegador padrão: na lista de alarmes,
no aviso recebido dentro do app ou na notificação Windows.
Clicar no corpo da notificação somente restaura a janela.

A URL e o texto ficam no LiteDB. Alarmes antigos permanecem sem ação.
Abrir a página não confirma nem reagenda o alarme, e o app não preenche ou envia ponto.
Um aviso já entregue mantém o link do momento do disparo, mesmo após editar o alarme.
**Dispensar** limpa os avisos locais e seus botões.

## Som dos alarmes

No modal de criação/edição, escolha **Usar padrão do aplicativo**, **Notificação do Windows**,
**Exclamação do Windows**, **Erro crítico do Windows**, **Sem som** ou **Arquivo personalizado**.
Use **Selecionar MP3 ou WAV** para importar áudio de até 20 MiB; **Ouvir** e **Parar** controlam a prévia.
Configurações permite alterar o padrão, inclusive para um arquivo personalizado ou silêncio.
O padrão inicial é Notificação do Windows. Alarmes antigos seguem o padrão automaticamente.

Sons do Windows respeitam seu esquema de sons; um evento sem áudio permanece silencioso.
MP3/WAV são validados e copiados para `%LOCALAPPDATA%\TeamReminder\Sounds` com identificadores próprios.
Mover ou excluir o original não afeta o alarme. LiteDB guarda a escolha e a referência da cópia;
o padrão fica em `Settings\default-sound.json`. Ao fazer backup, preserve banco, Sounds e Settings.
Cópias sem referências são removidas após leitura bem-sucedida do banco, ao encerrar o editor,
excluir um alarme ou alterar o padrão. Arquivos em importação são protegidos até finalizar a edição.
Se a cópia estiver ausente ou ilegível, o app informa e tenta Notificação do Windows.
O áudio toca uma vez; para alarmes simultâneos, o último som iniciado pode substituir o anterior,
e todos os avisos visuais continuam disponíveis. Uma prévia é interrompida pelo disparo real.

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

101 testes de regras/ViewModel/calendário/motores/XML e 32 de persistência e biblioteca de sons.
Os testes usam relógio controlado e bancos temporários; não alteram alarmes do usuário.

Para verificar envio de áudio/notificação, persistência e reinício no executável Debug/x64,
com a instância Debug deste build encerrada e PowerShell 7.4 ou superior:

```powershell
 ./scripts/Test-AlarmDelivery.ps1
```

Para testar os decodificadores Windows, importação e reprodução real de WAV/MP3, fallback
e recursos XAML do modal, também com o app encerrado:

```powershell
 ./scripts/Test-AlarmSounds.ps1
 ./scripts/Test-AlarmDelivery.ps1 -StartInTray -SoundKind Custom -AudioFile C:\caminho\som.mp3
 ./scripts/Test-AlarmDelivery.ps1 -SoundKind Silent
 ./scripts/Test-AlarmDelivery.ps1 -StartInTray -LinkUrl "https://example.com/ponto" -LinkLabel "Bater ponto"
```

Esses testes usam banco, pastas de áudio, mutex e IPC isolados no build Debug. O app instalado pode permanecer aberto. Os relatórios ficam em artifacts.

Esse teste emite um aviso real com som, usando banco isolado em artifacts/smoke.
Os parâmetros de ambiente de diagnóstico utilizados pelos scripts só existem no build Debug.
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
| 010 - ações de link | [spec](docs/specs/010-alarm-link-actions/spec.md) | [plan](docs/specs/010-alarm-link-actions/plan.md) | [tasks](docs/specs/010-alarm-link-actions/tasks.md) |
| 009 - sons por alarme | [spec](docs/specs/009-alarm-sounds/spec.md) | [plan](docs/specs/009-alarm-sounds/plan.md) | [tasks](docs/specs/009-alarm-sounds/tasks.md) |
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
O aviso é uma notificação do Windows com título, horário, botão do link e Abrir Team Reminder; sem
notificações disponíveis, o balão da bandeja é usado como alternativa.
Windows controla duração, apresentação e sons do sistema; o pré-aviso não toca o som próprio do alarme.

Recibos na coleção earlyReminderReceipts da mesma instância LiteDB evitam repetir uma ocorrência
após reiniciar. O recibo representa pedido ao componente de bandeja, não confirmação de leitura.
Retomar/criar dentro da janela de antecedência avisa uma vez enquanto o horário ainda é futuro.
No horário ou depois dele, o motor emite somente o alarme principal. Recorrentes têm aviso por ocorrência.
Essa preferência não altera horário, recorrência ou o disparo principal.

[Spec 007 — Avisos antecipados](docs/specs/007-advance-reminders/spec.md) · [Checklist](docs/specs/007-advance-reminders/manual-checklist.md).
