# Validação manual - 003

Status: pendente; testes automatizados e APIs nativas já verificados.

1. Abra o app: confira Data inicial preenchida com hoje, horário vazio e Uma vez selecionado.
2. Cadastre para o próximo minuto e mantenha aberto. Confira som, aviso na janela e toast Windows.
3. Aguarde mais alguns segundos: o alerta não deve repetir e a linha deve mostrar Disparado.
4. Feche/reabra: a ocorrência já entregue não deve tocar de novo.
5. Escolha Todos os dias para o próximo minuto. Depois do aviso, confira próxima ocorrência amanhã no mesmo horário.
6. Escolha Todos os dias com horário já passado hoje: confira que a primeira ocorrência é amanhã.
7. Escolha Dias da semana sem marcar dias: confira a validação.
8. Marque segunda/quarta/sexta e salve: confira dias e próxima ocorrência elegível na lista.
9. Edite esse alarme: confira regra e dias restaurados. Cancelar/salvar retorna à data de hoje.
10. Minimize com um alarme para o próximo minuto: confira som e notificação.
11. Mantenha dois alarmes para o mesmo minuto: confira avisos individuais.
12. Com o app fechado durante o vencimento, reabra: confira um aviso atrasado, sem sequência de alertas perdidos.
13. Exclua um alarme futuro e confirme que ele não toca no horário removido.
14. Confira teclado, rolagem do formulário e temas claro/escuro.

Volume e permissões de notificação pertencem ao Windows. O app informa falha de canal
quando detectada e continua com os canais locais disponíveis. Não existe System Tray nesta entrega.
Registre data e resultado em tasks.md antes de marcar itens concluídos.
