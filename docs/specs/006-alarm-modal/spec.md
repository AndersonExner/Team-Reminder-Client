# 006 — Cadastro em modal e padrões

Status: implementado na branch feature/visual-usability; 85 testes aprovados; revisão visual manual pendente.

## Critérios de aceitação
- AC01: tela Alarmes exibe lista e um botão Novo alarme, sem formulário permanente.
- AC02: Novo alarme abre modal com título, data, horário, recorrência e dias selecionáveis.
  Editar usa o mesmo modal preenchido com dados do alarme. Bandeja Adicionar abre esse modal.
- AC03: abrir Novo alarme preenche data e hora atuais no fuso local, com precisão de minuto,
  mesmo que o aplicativo esteja aberto desde o dia anterior. Editar preserva o horário salvo.
- AC04: Salvar fecha modal somente após gravação bem-sucedida. Validação/falha mantém campos
  e mensagens dentro do modal. Cancelar/Esc descarta rascunho, sem gravar ou alterar alarmes.
  Não permitir fechamento durante gravação nem múltiplos diálogos simultâneos.
- AC05: claro é o tema inicial quando não há preferência válida. Preferências explícitas
  Claro/Escuro/Windows já salvas permanecem respeitadas; modal acompanha tema da janela.
- AC06: motor continua funcionando com modal aberto. Navegação e fechamento não deixam
  formulário invisível em edição após cancelar o modal.

## Decisões
Hora atual significa HH:mm atual, sem adicionar minutos automaticamente. Alarmes Uma vez
continuam exigindo horário futuro; o modal explica que esse horário precisa ser ajustado.
Supersede formulário lado a lado/empilhado de 004, horário vazio de 001/003 e comportamento
de cadastro pela bandeja de 005. Não altera persistência, recorrência ou regras de disparo.
