# Tarefas e evidências

- [x] Spec/plano antes da implementação, com configuração global explícita e limites do canal nativo.
- [x] Remover frase solicitada; opções desativado/5/10/15/30/60 minutos, persistência local.
- [x] Motor separado de antecipação; não avança/completa ocorrência principal.
- [x] Popup nativo e clique para restaurar janela; não executa áudio próprio do alarme.
- [x] Recibos na mesma instância LiteDB; persistência e verificação de snapshot antes de gravar.
- [x] Tratar falhas de envio/gravação, limites da janela, reinício e próximas recorrências.
- [x] Build cliente isolado: 0 erros/avisos.
- [x] 84 testes Core e 15 Local aprovados (99 total), incluindo 12 cenários novos do motor
  e 2 de persistência/CAS/limpeza, com relógio e banco isolados.
- [ ] Revisão manual de exibição/fechamento do popup e configurações no Windows.

Comandos em 11/10/2026:
- dotnet build src/clients/TeamReminder.Windows/TeamReminder.Windows.csproj -p:Platform=x64 -p:BaseOutputPath=E:\ProjetosPessoais\Team-Reminder-Client\artifacts\verification\
- dotnet test TeamReminder.Client.sln -p:Platform=x64 -p:BaseOutputPath=E:\ProjetosPessoais\Team-Reminder-Client\artifacts\verification\

Não foi alterada a configuração de antecedência do usuário para executar os testes.
Instância aberta preservada; validação visual ainda pendente.
