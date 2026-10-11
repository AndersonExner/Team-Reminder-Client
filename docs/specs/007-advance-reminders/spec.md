# 007 — Avisos antecipados

Status: implementado em feature/visual-usability; build e 99 testes aprovados; revisão visual do popup pendente.

## Critérios de aceitação
- AC01: remover das Configurações a frase sobre Windows ligado/usuário conectado.
- AC02: Configurações > Lembrete antecipado oferece Desativado (padrão), 5, 10, 15, 30 e
  60 minutos. Preferência global neste computador, aplicada a todos os alarmes futuros;
  salvo separadamente do tema. Configuração inválida/ausente desativa; falha de escrita informa.
- AC03: às 15:20 avisar de um alarme às 15:30 com antecedência de 10 minutos.
  Exibir título e horário em popup nativo da bandeja, sem executar o som próprio do alarme.
  Não roubar foco. Clique no aviso restaura janela. Fechamento automático controlado pelo Windows.
- AC04: um pedido de aviso por alarme/ocorrência; registro persistente impede repetição após
  reiniciar dentro da janela de antecedência. Alterar antecedência não repete ocorrência já avisada.
- AC05: se retomar/criar dentro da janela de antecedência, avisar uma vez se o horário ainda
  está no futuro. No horário ou após ele, emitir somente alarme principal, sem pré-aviso atrasado.
- AC06: recorrentes recebem novo pré-aviso para cada próxima ocorrência.
  Pré-aviso não marca alarme como disparado, nem avança recorrência, nem muda horário.
- AC07: falha de envio tenta novamente; falha ao salvar recibo tenta gravar sem repetir o pedido
  no mesmo processo. Snapshot antigo não grava recibo de alarme alterado/excluído.
- AC08: manter disparo principal independente de falhas/configuração do aviso antecipado.

## Limites e decisões
Popup usa NotifyIcon.ShowBalloonTip. Windows pode apresentar aviso como notificação, silenciar,
agrupar ou suprimir pedidos; aparência, posição, sons do sistema e duração seguem configurações
do Windows. Recibo significa pedido aceito pelo componente, não comprovação de leitura/exibição.
Interrupção entre envio e gravação pode repetir após reinício. Não há promessa de exatamente uma vez.
Antecedência global nesta entrega; configuração por alarme e múltiplos pré-avisos ficam para evolução.
