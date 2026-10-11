# 004 — Temas e organização visual

Status: implementado na branch feature/visual-usability; build e testes aprovados; revisão visual manual pendente.

## Objetivo
Organizar alarmes e preferências, com aparência clara/escura e layout adaptável.

## Critérios de aceitação
- AC01: abas Alarmes e Configurações, sem fechamento ou reorganização das abas.
- AC02: escolher Padrão do Windows, Claro ou Escuro aplica imediatamente à janela e diálogos.
- AC03: preferência permanece após reiniciar. Arquivo ausente/inválido usa padrão do Windows;
  falha ao salvar informa o usuário sem impedir a aplicação do tema nesta sessão.
- AC04: lista e formulário usam painéis lado a lado em largura útil >= 900 px;
  abaixo disso ficam empilhados com rolagem. Não perder campos ao redimensionar/trocar abas.
- AC05: ações de editar/excluir/atualizar ficam junto da lista. Cadastro mantém validação,
  recorrência, confirmação de exclusão e data padrão existentes.
- AC06: alertas permanecem visíveis em ambas as abas; agendamento continua durante navegação.
- AC07: cores usam recursos do tema, rótulos e foco de teclado permanecem disponíveis.

## Escopo
Preferência visual local, sem mudanças de schema dos alarmes ou de regras de disparo.
Tema não altera som, notificações do Windows, nem adiciona execução com app fechado.
