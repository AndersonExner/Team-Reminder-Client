# Tarefas e evidências

- [x] Especificar modal, padrões e comportamento de salvar/cancelar.
- [x] Remover formulário fixo; Novo alarme e Editar abrem ContentDialog compartilhado.
- [x] Bandeja Adicionar abre modal, aguardando carregamento da árvore visual quando necessário.
- [x] Salvar usa deferral e resultado explícito; falhas mantêm campos/erros no modal.
- [x] Bloquear diálogos simultâneos e fechamento ocupado; cancelar limpa estado sem gravação.
- [x] Data/hora atuais em HH:mm, atualizadas na abertura e limpeza; editar preserva horário salvo.
- [x] Tema claro como fallback; preservar preferência válida existente.
- [x] Regressão: 72 Core + 13 Local aprovados, incluindo três novos testes de fuso/virada de dia,
  resultado de salvar e cancelamento sem alteração de dados.
- [ ] Revisão visual/interação do modal conforme manual-checklist.md.

Verificações em 11/10/2026. Dados do usuário preservados; testes usam relógio/banco isolados.

- [x] Build final em pasta isolada: 0 avisos e 0 erros.
  Comando: dotnet build src/clients/TeamReminder.Windows/TeamReminder.Windows.csproj -p:Platform=x64 -p:BaseOutputPath=E:\ProjetosPessoais\Team-Reminder-Client\artifacts\verification\
  A instância aberta bloqueou a cópia para bin habitual; não foi encerrada.
  Smoke desta revisão não executado porque a instância normal permanece ativa.
