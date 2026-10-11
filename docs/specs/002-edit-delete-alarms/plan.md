# Plano técnico - 002

Status: implementado; build e testes aprovados; validação visual pendente.

- Manter os projetos e versões existentes; não adicionar dependências.
- Estender IAlarmRepository com UpdateAsync e DeleteAsync, retornando bool para existência.
- Usar Update e Delete do LiteDB, dentro da mesma serialização e conexão já existentes.
- Preservar CreatedAtUtc no documento persistido; atualização não usa Upsert.
- Compartilhar a validação do AlarmService entre CreateAsync e UpdateAsync.
- AlarmNotFoundException distingue ausência de falha de armazenamento.
- AlarmsViewModel mantém seleção e edição separadas, comandos de editar/cancelar/excluir,
  rótulos de formulário e botão salvar conforme o modo.
- A confirmação é um callback assíncrono injetado pela janela, com padrão seguro de cancelar.
  ContentDialog usa o XamlRoot da janela, botão padrão Cancelar e título/horário do item.
- A lista e o formulário só mudam após a persistência confirmar a operação.
- Busy abrange confirmação e gravação; bloqueia seleção, comandos e formulário.
- Testar comportamento usando o mesmo repositório controlado da entrega 001.
- Testar edição/exclusão com arquivos temporários reais, incluindo reabertura e Id ausente.

## Validação

Testes de regras, ViewModel e LiteDB, build x64 e verificação de inicialização.
Verificação visual de seleção, edição e ContentDialog registrada separadamente.
