# 001 - Criar, listar e persistir alarmes locais

Status: implementada; build e testes aprovados; validação visual pendente.

## História

Como usuário, quero cadastrar um alarme com título, data e horário
para consultá-lo novamente quando fechar e abrir o aplicativo.

## Escopo

Formulário de cadastro, listagem, validação e persistência local.
Sem disparo de alertas nesta entrega. O horário representa quando o alarme deverá ocorrer;
a entrega efetiva será implementada na especificação de notificações.

## Regras adotadas

- R01: título obrigatório, removendo espaços nas extremidades; entre 1 e 120 caracteres.
- R02: data e horário obrigatórios; o instante deve ser futuro no momento de salvar.
- R03: interpretar o horário no fuso local do Windows no momento do cadastro.
  Rejeitar horários inexistentes ou ambíguos por transição de horário de verão,
  com mensagem para escolher outro horário.
- R04: salvar somente após validação e confirmação da gravação.
- R05: exibir título, horário local e situação temporal, ordenando por horário crescente.
  Empates são ordenados pelo instante de criação e pelo identificador.
- R06: sem registros, exibir uma indicação de lista vazia.
- R07: após salvar, inserir na lista e limpar o formulário.
- R08: reabrir o aplicativo carrega os alarmes salvos no mesmo perfil.
- R09: falha de gravação mantém os campos e informa que o alarme não foi salvo.
- R10: impedir submissões simultâneas do mesmo formulário.
  Alarmes diferentes com título e horário iguais são permitidos.
- R11: alarme cujo horário passou permanece na lista como "Horário passado";
  demais alarmes aparecem como "Agendado". Esses rótulos não comprovam entrega.
- R12: falha ao carregar o banco deve aparecer como erro com opção de tentar novamente,
  sem apresentar a falha como lista vazia ou sobrescrever o arquivo.

## Critérios de aceitação

| ID | Cenário e resultado |
| --- | --- |
| AC01 | Dado título válido e horário futuro, ao salvar, o alarme aparece na lista e persiste. |
| AC02 | Título vazio, somente espaços ou maior que 120 caracteres impede gravação e mostra a regra. |
| AC03 | Data ou horário ausente, instante igual ao atual ou passado impede gravação. |
| AC04 | Ao fechar e reabrir o aplicativo, o alarme salvo reaparece com o mesmo identificador e horário. |
| AC05 | A lista vazia mostra orientação para criar o primeiro alarme. |
| AC06 | Uma falha de gravação informa o erro, preserva os campos e não exibe sucesso. |
| AC07 | Cliques repetidos durante a gravação não criam duplicatas da mesma submissão. |
| AC08 | Alarmes aparecem em ordem cronológica, com título, horário e situação temporal. |
| AC09 | Um alarme passado continua disponível; nenhuma notificação é esperada nesta entrega. |
| AC10 | O cadastro e a leitura funcionam sem rede. |
| AC11 | Horário local inválido ou ambíguo é rejeitado com mensagem explicativa. |
| AC12 | Falha de leitura mostra erro e permite nova tentativa, preservando o arquivo. |

## Experiência

Uma janela com listagem e formulário simples.
Mensagens em português, junto ao campo correspondente quando possível.
Durante a gravação, o botão salvar fica indisponível.
A situação temporal é recalculada ao carregar e enquanto a lista estiver aberta.

## Limites

Edição, exclusão, descrição, recorrência, antecedência, adiamento, notificações,
System Tray e sincronização pertencem às próximas entregas.
Os dados são isolados por perfil Windows. Não haverá suporte a abrir várias instâncias
simultâneas nesta entrega; a implementação deverá impedir a segunda instância.

## Evidência esperada

Testes das regras e de persistência com arquivo temporário real.
Verificação manual da interface e do reinício no Windows.


## Alteração em 2026-10-11 - spec 003

A limpeza do formulário passa a preencher a data de hoje, mantendo o horário vazio.
Disparo e recorrência agora seguem ../003-alerts-recurrence/spec.md.
Um horário vencido aparece como Aguardando disparo; após entrega única, Disparado.
Recorrentes mostram a próxima ocorrência. As evidências originais desta spec
representam a entrega da época; o README e a spec 003 descrevem o estado atual.
