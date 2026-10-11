# Tarefas - 002

Status: implementação concluída; validação visual pendente.

- [x] Escrever especificação e plano técnico.
- [x] Implementar atualização/exclusão e validação compartilhada.
- [x] Implementar persistência sem recriar Id ausente.
- [x] Implementar seleção, edição, cancelamento e exclusão no ViewModel.
- [x] Implementar confirmação e controles WinUI.
- [x] Testar regras, falhas, cancelamento e operações simultâneas.
- [x] Testar edição/exclusão e reabertura com banco real temporário.
- [x] Compilar solução x64 e verificar inicialização.
- [ ] Validar visualmente os fluxos no Windows.
- [x] Atualizar README e registrar resultados.

## Evidências - 2026-10-11

| Verificação | Resultado |
| --- | --- |
| dotnet build TeamReminder.Client.sln -p:Platform=x64 | Aprovado, 0 avisos e 0 erros |
| dotnet test tests/TeamReminder.Core.Tests --verbosity minimal | 46 testes aprovados |
| dotnet test tests/TeamReminder.Local.Tests --verbosity minimal | 8 testes aprovados |
| Inicialização do executável atualizado | Janela Team Reminder criada |
| Fechamento da janela | Encerramento normal com código 0 |

54 testes no total, incluindo os 28 testes da entrega 001.
Os testes da entrega 002 cobrem edição do mesmo Id, validação, cancelamento,
confirmação recusada, falhas, reordenação, seleção independente da edição,
proteção contra repetição, Id ausente e persistência após reabrir o arquivo.
O callback de confirmação é testado; o ContentDialog real ainda requer verificação visual.
Os testes não alteram os alarmes do usuário.

O primeiro build encontrou DLLs bloqueadas por uma instância anterior aberta.
O build final foi concluído após a instância encerrar.

Verificação visual e operação sem rede da entrega 001 continuam pendentes.
Disparo e recorrência foram implementados posteriormente na spec 003. Antecedência, adiamento e System Tray continuam pendentes.
