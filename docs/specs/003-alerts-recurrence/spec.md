# 003 - Disparo, data padrão e recorrência

Status: implementado; testes e disparo no executável aprovados; validação visual completa pendente.

## Pedido

Emitir aviso com som no horário escolhido, preencher a data atual ao criar e permitir
uma vez, todos os dias ou dias da semana selecionados (ex.: segunda, quarta e sexta).

## Regras

- R01: novo formulário inicia com a data atual no fuso do Windows. Após salvar/cancelar,
  retorna à data atual; horário continua sem preenchimento automático.
- R02: recorrência padrão Uma vez. Todos os dias usa o mesmo horário diariamente.
  Dias da semana exige ao menos um dia marcado e permite qualquer combinação.
- R03: Uma vez exige horário futuro. Recorrentes exigem data inicial a partir de hoje;
  se o horário de hoje passou, escolher a primeira ocorrência futura elegível.
- R04: a lista mostra a próxima ocorrência e a recorrência. Editar preenche o próximo
  horário, mantém a regra e os dias; salvar uma edição reprograma o alarme.
- R05: verificar vencimentos aproximadamente a cada segundo com o app aberto,
  incluindo quando minimizado. Emitir som curto próprio do aplicativo e aviso na janela.
  Enviar também notificação Windows quando o canal estiver disponível.
- R06: falha do canal Windows não impede som/aviso na janela; informar essa condição.
  Volume/mudo e permissões de notificações são controlados pelo Windows.
- R07: registrar no LiteDB a ocorrência entregue. Uma vez muda para Disparado;
  recorrentes avançam para a próxima ocorrência futura, sem disparar a cada segundo.
- R08: reabrir o app não repete ocorrência já registrada. Alarmes antigos sem campos
  de recorrência são interpretados como Uma vez; seus dados devem ser preservados.
- R09: depois de suspensão/app fechado, emitir um único aviso atrasado por alarme
  pendente, e avançar recorrentes para o próximo horário futuro, sem reproduzir todos os atrasados.
- R10: recorrência mantém o horário no fuso salvo no alarme. Pular horários inexistentes
  em mudança de horário de verão; em horário ambíguo futuro, usar a primeira ocorrência.
  Horário inicialmente escolhido inexistente/ambíguo continua sendo rejeitado.
- R11: falha ao entregar não marca como entregue. Falha ao registrar tenta novamente
  sem tocar repetidamente no mesmo processo. Interrupção entre entrega e gravação
  pode repetir o aviso ao reiniciar; não há promessa de entrega exatamente uma vez.
- R12: excluir impede ocorrências futuras; editar reprograma sem permitir que um estado
  antigo sobrescreva a alteração. Disparo não descarta campos ainda não salvos no formulário.
- R13: alarmes simultâneos devem gerar avisos individuais, sem que a falha de um impeça os outros.

## Critérios de aceitação

| ID | Cenário e resultado |
| --- | --- |
| AC01 | Abrir cadastro e cancelar/salvar retorna à data de hoje, mantendo horário vazio. |
| AC02 | Uma vez emite um aviso com som ao vencer e não repete em ticks seguintes/reinício. |
| AC03 | Todos os dias mantém o horário e avança para o dia seguinte após disparar. |
| AC04 | Segunda/quarta/sexta só agenda nesses dias, inclusive na virada da semana. |
| AC05 | Dias da semana sem seleção impede gravação com mensagem explicativa. |
| AC06 | Editar/reabrir mantém recorrência e dias selecionados; registros antigos continuam disponíveis. |
| AC07 | Um atraso de vários dias produz um aviso, seguido de próxima ocorrência futura. |
| AC08 | Falhas de envio/gravação são recuperáveis sem repetição contínua após envio aceito. |
| AC09 | Alteração/exclusão não é sobrescrita por conclusão de ocorrência antiga. |
| AC10 | Fusos e transições de horário de verão preservam o horário local da recorrência. |
| AC11 | Com app aberto/minimizado, canal Windows disponível recebe notificação; indisponível mantém aviso local. |

## Limites

App fechado não executa o motor. System Tray, inicialização automática, adiamento,
antecedência, recorrência mensal, som contínuo e seleção de sons ficam para próximas specs.
Esta entrega modifica a limpeza do formulário prevista em 001/002: a data passa a voltar para hoje.
Validação visual anterior continua pendente até ser executada.
