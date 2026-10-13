# Tarefas e evidências
- [x] L01/L02: campos opcionais e validação HTTP/HTTPS, host, limites, controles e credenciais.
- [x] L03: LiteDB com campos opcionais e compatibilidade; editar/remover/cancelar.
- [x] L04/L05/L06: botões na lista/aviso local/toast e abertura somente por clique; XML escapado e launch simulado sem navegador real.
- [x] L07/L08: avisos limitados a dez snapshots e limpeza via Dispensar; README e checklist manual atualizados.

## Verificações
- Debug: 101 Core + 32 Local passaram (133).
- Release: 101 Core + 31 Local passaram (132); conexão Shared é exclusiva de Debug.
- Build WinUI Debug/x64: zero erros/avisos.
- Publish Release e Inno Setup concluídos; log em artifacts/installer-build-links.log.
- URLs inválidas não gravam; falhas de gravação preservam rascunho; campos opcionais, edição e remoção sobrevivem ao reinício.
- XML conserva query/fragmento e rótulo com aspas, ampersand e sinais; link inválido/ausente não gera botão.
- Launch simulado comprova que salvar/carregar/reportar não abre página e que abrir ou falhar não altera o alarme.

## Validação nativa/manual
Smoke Windows concluído com app instalado ativo: botão de protocolo HTTP/HTTPS e query/fragmento preservados; notificação registrada/aceita; janela de teste oculta na bandeja; segunda instância de teste restaurou a janela correta; ação preservada no banco e reinício sem repetição. Evidência: artifacts/smoke/f33d296b-1ea2-4512-8b9a-734aac19f113. Clique no toast e navegação real, revisão visual dos temas e atualização pelo assistente seguem no checklist manual.
O modo diagnóstico Debug agora isola mutex/IPC por banco de teste; sem a variável de diagnóstico, a regra normal de instância única continua. Release não inclui essa exceção.
