# 010 — Ação de abrir link
- L01: Link opcional e Texto do botão (40 caracteres) na criação/edição. Sem texto usa Abrir link. Texto sem URL indica erro.
- L02: URL absoluta HTTP/HTTPS com host, até 2048 caracteres, sem credenciais/controles. Validar ao salvar e abrir. Falha mantém formulário.
- L03: LiteDB guarda URL/rótulo opcionais. Antigos ficam sem ação; editar troca/remove; cancelar preserva.
- L04: botões na lista e nos avisos recebidos abrem navegador padrão somente por clique; falha informa e permite tentar novamente.
- L05: botão de protocolo na notificação Windows, com XML escapado. Corpo do aviso apenas restaura app.
- L06: salvar/carregar/disparar nunca abre página. Abrir não confirma nem reagenda alarme.
- L07: som, recorrência e entrega permanecem; falha ao abrir não repete alarme.
- L08: aviso usa snapshot da ação no momento do disparo. Até dez avisos locais; Dispensar limpa botões e avisos.

Não inclui abertura automática, comandos/arquivos nem envio de ponto. A ação no site continua manual.
