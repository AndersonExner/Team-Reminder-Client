# Tarefas - 001

Status: implementação concluída; build/testes aprovados; validação visual pendente.

## Preparação

- [x] Registrar visão do MVP e escopo da primeira entrega.
- [x] Escrever especificação, critérios de aceitação e plano.
- [x] Verificar ferramentas e escolher versões compatíveis.
- [x] Definir execução unpackaged x64.
- [x] Criar solução e projetos.

## Implementação

- [x] Implementar Alarm, provedor de tempo e validação (R01-R03).
- [x] Implementar IAlarmRepository e persistência LiteDB (R04, R08).
- [x] Configurar banco compartilhado, caminho local e descarte.
- [x] Impedir abertura de segunda instância.
- [x] Implementar formulário e submissão protegida (R07, R09-R10).
- [x] Implementar listagem, situação temporal e estado vazio (R05-R06, R11).
- [x] Implementar erro de carregamento e nova tentativa (R12).

## Verificação

- [x] Testar validação de título e horário (AC02, AC03, AC11).
- [x] Testar gravação e reabertura de banco temporário real (AC01, AC04).
- [x] Testar falhas com repositório controlado e preservar banco inválido (AC06, AC12).
- [x] Testar submissões simultâneas e limpeza após gravação (AC07).
- [x] Testar ordenação, estado vazio e atualização temporal (AC05, AC08, AC09).
- [x] Executar build e testes.
- [x] Verificar criação de janela, instância única e encerramento.
- [ ] Verificar visualmente o formulário e os bindings no Windows.
- [ ] Confirmar operação com rede indisponível (AC10); implementação não utiliza rede.
- [x] Atualizar README com instruções reais de execução.

## Evidências - 2026-10-11

| Comando / verificação | Resultado |
| --- | --- |
| dotnet build TeamReminder.Client.sln -p:Platform=x64 | Aprovado, 0 avisos e 0 erros |
| dotnet test tests/TeamReminder.Core.Tests | 24 testes aprovados |
| dotnet test tests/TeamReminder.Local.Tests | 4 testes aprovados |
| dotnet run --project src/clients/TeamReminder.Windows -p:Platform=x64 --no-build | Janela criada e encerramento com código 0 |
| Executável: inicialização | Janela Team Reminder criada |
| Executável: segunda instância | Encerramento com código 0 |
| Executável: fechar janela | Encerramento com código 0 |

Os testes verificam regras e persistência, mas não comprovam os bindings da interface.
AC04 possui teste de reabertura do banco; ainda falta testar fechar/reabrir pela interface.
Veja manual-checklist.md para os passos restantes.
