# Modal e alarmes de grupos

A modal organiza o formulário por destinatário, título e horário. Data e hora ficam lado a lado; link e som são opções recolhidas. Novos alarmes sugerem um horário cinco minutos à frente.

Só para mim é o destino padrão. Ao abrir a modal, o aplicativo consulta os grupos da conta e oferece apenas aqueles em que o usuário é proprietário ou administrador. Membros recebem os alarmes e veem a origem na lista, mas não podem editar ou excluir. O destino de um alarme existente não é alterado pela edição.

Alarmes de grupos são publicados online na API e ficam no cache da conta. Não entram na fila dos alarmes pessoais. A sincronização automática existente, a cada 60 segundos, também traz os alarmes de grupos e remove aqueles aos quais o usuário perdeu acesso. Uma resposta de rede incompleta ou com erro não limpa o cache.

A publicação usa identificador estável no rascunho e mutationId para repetir uma solicitação sem criar outra cópia. Edição e exclusão usam a versão do alarme. Conflitos e erros mantêm o formulário para revisão. A exclusão avisa que vale para todos os participantes.

O agendador e os comprovantes de disparo continuam locais e independentes em cada dispositivo. Os comprovantes são preservados durante atualização da definição e suspensão temporária do grupo. Quem entra depois recebe as definições atuais; avisos únicos antigos são tratados como histórico pelas regras de sincronização já existentes.

Arquivos de som personalizados não são enviados aos membros. No grupo, escolha som integrado ou o padrão de cada computador.

A API com a migração 5 deve ser integrada antes da distribuição deste aplicativo.

## Prévia isolada Debug
TEAMREMINDER_TEST_EDITOR_REPORT e TEAMREMINDER_TEST_DATA_FILE habilitam EditorSmokeProbe. Ele abre a modal com um grupo fictício, confirma a legenda Publicar no grupo, renderiza o conteúdo em PNG e encerra a instância isolada. Não cria alarmes nem usa a conta real. O código é excluído de builds Release.
