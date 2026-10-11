# 002 - Editar e excluir alarmes locais

Status: implementado; build e testes aprovados; validação visual pendente.

## Histórias

Como usuário, quero corrigir título e horário de um alarme sem criar outro registro.
Como usuário, quero excluir um alarme que não preciso mais, após confirmar a exclusão.

## Regras

- R01: selecionar um alarme habilita Editar e Excluir; sem seleção, ambos ficam indisponíveis.
- R02: Editar preenche o formulário com título, data e horário no fuso local atual.
  O formulário identifica edição e oferece Cancelar edição.
- R03: Salvar alterações mantém Id e CreatedAtUtc, atualizando título, horário UTC e fuso.
  Aplicar as mesmas validações do cadastro. Um alarme passado pode ser reagendado para o futuro.
  Esta entrega não permite salvar uma edição com horário passado, mesmo que só o título tenha mudado.
- R04: Cancelar edição limpa o formulário e retorna ao modo de criação sem gravar.
- R05: selecionar outra linha não altera o alarme em edição. Clicar Editar nessa nova seleção
  substitui o formulário, descartando os campos não salvos da edição anterior.
- R06: excluir solicita confirmação identificando título e horário. Cancelar não altera nada.
- R07: só remover da lista após o banco confirmar a exclusão. Se o item excluído estiver
  em edição, limpar o formulário. Excluir outro item preserva a edição atual.
- R08: falhas mantêm lista e campos; mostrar mensagem e permitir nova tentativa.
- R09: bloquear operações simultâneas, inclusive durante a confirmação de exclusão.
- R10: atualização ou exclusão de Id ausente informa que o alarme não existe mais,
  orientando atualizar a lista. Atualização nunca cria um registro ausente.
- R11: ordenar a lista novamente após edição e preservar os outros registros.
- R12: alterações e exclusões persistem após fechar/reabrir o banco/aplicativo.
- R13: atualizar a lista preserva os campos em edição se o Id ainda existir.
  Se o Id não existir, cancelar essa edição e informar o usuário.

## Critérios de aceitação

| ID | Cenário e resultado |
| --- | --- |
| AC01 | Selecionar e editar preenche o formulário com os dados do item correto. |
| AC02 | Salvar edição válida atualiza o mesmo Id, mantém criação e reordena a lista. |
| AC03 | Edição inválida mantém os campos e o registro anterior, sem gravar. |
| AC04 | Cancelar edição mantém o banco e limpa o formulário para criar outro alarme. |
| AC05 | Cancelar confirmação de exclusão mantém lista, banco e formulário. |
| AC06 | Confirmar exclusão remove somente o item selecionado; excluir o último mostra lista vazia. |
| AC07 | Falhas de edição/exclusão mantêm registros e campos e permitem tentar novamente. |
| AC08 | Cliques repetidos durante a operação produzem apenas uma gravação/exclusão. |
| AC09 | Reabrir o banco/aplicativo mantém alterações e não traz de volta itens excluídos. |
| AC10 | Id ausente não é recriado ao editar; exclusão ausente informa a ausência. |
| AC11 | Selecionar outra linha mantém a edição atual; excluir outro item também a mantém. |
| AC12 | Atualizar a lista mantém a edição existente ou informa que o item desapareceu. |

## Fora desta entrega

Disparo de notificações, recorrência, adiamento, System Tray e sincronização.
A validação visual da entrega 001 permanece pendente.


## Alteração em 2026-10-11 - spec 003

A limpeza do formulário passa a preencher a data de hoje, mantendo o horário vazio.
Disparo e recorrência agora seguem ../003-alerts-recurrence/spec.md.
Um horário vencido aparece como Aguardando disparo; após entrega única, Disparado.
Recorrentes mostram a próxima ocorrência. As evidências originais desta spec
representam a entrega da época; o README e a spec 003 descrevem o estado atual.
