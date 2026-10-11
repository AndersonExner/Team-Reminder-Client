# Plano técnico - 001

Status: implementado; build e testes aprovados; validação visual pendente.

## Tecnologias

C#, .NET, WinUI 3, MVVM e LiteDB.
Antes de criar o projeto, verificar SDK, ferramentas Windows e compatibilidade
das versões de Windows App SDK, LiteDB e biblioteca MVVM. Registrar versões
escolhidas na solução; não escolher versões apenas por suposição.

## Estrutura inicial

src/clients/TeamReminder.Windows      - janela, ViewModels e composição
src/core/TeamReminder.Core            - modelo, validação, contratos e MVVM sem dependência WinUI
src/infrastructure/TeamReminder.Local - implementação LiteDB
tests/TeamReminder.Core.Tests         - regras
tests/TeamReminder.Local.Tests        - persistência

Sem projetos de backend nesta fase.

## Modelo inicial

Alarm:
- Id: Guid criado antes da gravação.
- Title: string normalizada.
- ScheduledAtUtc: DateTime em UTC.
- CreatedAtUtc: DateTime em UTC.
- TimeZoneId: identificador Windows do fuso usado no cadastro.

A situação temporal é derivada do horário, sem armazenar um estado de entrega fictício.
A UI converte o instante salvo para o fuso local atual para exibição.
Não há política de recorrência nesta entrega.

## Serviços e fluxo

View -> ViewModel -> serviço de cadastro -> IAlarmRepository -> LiteDB.

IAlarmRepository oferece inserção e listagem.
Um provedor de tempo permite verificar horários sem depender do relógio real em testes.
O serviço normaliza e valida os campos; o ViewModel traduz resultados para a UI.

Instância única de LiteDatabase, compartilhada por injeção de dependência
e descartada no encerramento. Não criar uma conexão por ViewModel.

Arquivo adotado: %LOCALAPPDATA%\TeamReminder\Data\team-reminder.db.
Usar coleção alarms e índice para ScheduledAtUtc.
Operações de disco não devem bloquear a thread da interface.
A lista só indica sucesso após persistência confirmada.

## Ciclo de vida

Impedir execução de duas instâncias do aplicativo.
Nesta entrega, fechar a janela encerra o processo e libera o banco.
System Tray será implementado em uma especificação posterior.
Erros de abertura ou leitura não devem recriar ou apagar automaticamente o banco.

## Validação

- Regras: limites de título, horário futuro e conversão de fuso.
- Persistência: inserir, fechar e reabrir banco temporário; verificar identidade e valores.
- Interface: validar campos, salvar, conferir ordenação e reiniciar.
- Falhas: simular erros por implementação controlada do contrato do repositório.
- Confirmar fluxo sem rede.

## Decisões técnicas verificadas



- SDK .NET 10.0.401, projetos base net10.0.
- Cliente net10.0-windows10.0.19041.0, x64.
- Windows App SDK 1.8.260921001, linha estável 1.8.
- LiteDB 5.0.21; datas lidas em UTC pela propriedade LiteDatabase.UtcDate.
- MVVM implementado com INotifyPropertyChanged e ICommand, sem pacote adicional.
- Injeção por construtores na composição App, sem contêiner de DI.
- Aplicativo unpackaged; Windows App SDK self-contained, .NET framework-dependent.
- Mutex global por SID do usuário para impedir segunda instância.
- Banco aberto sob demanda para permitir nova tentativa após falha inicial.
- Operações LiteDB serializadas fora da thread da interface.
- DispatcherTimer atualiza horário/situação enquanto a janela está aberta.

Fontes consultadas:
- https://github.com/microsoft/WindowsAppSDK/releases
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps
- https://www.nuget.org/packages/LiteDB/5.0.21

Build: aprovado sem avisos em 2026-10-11.
Inicialização: janela criada, segunda instância encerrada e fechamento normal verificados.
Interface: validação visual ainda pendente, conforme manual-checklist.md.
