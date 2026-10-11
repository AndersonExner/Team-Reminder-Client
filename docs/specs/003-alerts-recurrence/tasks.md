# Tarefas - 003

Status: implementado e verificado por testes; revisão visual/áudio audível pendente.

- [x] Especificar os ajustes e as regras de disparo.
- [x] Implementar calendário, recorrência e primeira ocorrência.
- [x] Persistir recorrência/entrega com compatibilidade de registros antigos.
- [x] Implementar motor com prevenção de repetições e recuperação de falhas.
- [x] Atualizar formulário, data padrão e lista de próximos horários.
- [x] Implementar som e notificações Windows com aviso local.
- [x] Testar regras, fusos, concorrência, falhas e persistência.
- [x] Compilar e verificar disparo/reinício no executável.
- [ ] Verificar visualmente formulário, notificação e áudio audível no Windows.
- [x] Atualizar README e registrar evidências.

## Evidências - 2026-10-11

| Verificação | Resultado |
| --- | --- |
| dotnet build TeamReminder.Client.sln -p:Platform=x64 | Aprovado, 0 avisos/erros |
| dotnet test tests/TeamReminder.Core.Tests --verbosity minimal | 69 aprovados |
| dotnet test tests/TeamReminder.Local.Tests --verbosity minimal | 12 aprovados |
| scripts/Test-AlarmDelivery.ps1 | Disparo real, áudio aceito, notificação aceita, recibo gravado e reinício sem repetição |

81 testes no total. Entre os casos: segunda/quarta/sexta, viradas de mês/ano,
horário de verão, fuso salvo, default hoje, reprogramação, registros antigos,
um aviso para vários dias de atraso, falhas de envio/gravação e ticks simultâneos.

Teste no executável: ocorrência em 11/10/2026 às 15:44:29 (America/Sao_Paulo),
entrega às 15:44:30. SoundAccepted=true, NotificationRegistered=true, NotificationId=85080.
Evidência original: artifacts/smoke/462424a1-3c5d-41d1-9e16-876556700542/delivery.jsonl.
Nenhum alarme real do usuário foi alterado nesse teste.

A aceitação de som/notificação pelo Windows foi verificada. Volume audível,
layout, seleção de recorrência por controles reais e funcionamento minimizado
continuam no checklist manual. Não foram marcados como verificados.

## Decisões de implementação

Som WAV próprio, de aproximadamente 2 segundos, incluído como Content já existente
nos itens WinUI. Toast silencioso para não duplicar o áudio. Aviso local preserva
até dez alertas recentes; o usuário pode dispensá-los.
Dois parâmetros de ambiente exclusivos do build Debug permitem o teste isolado.
O estado de entrega é registrado após o notificador aceitar, com guarda em memória
quando a gravação falha; uma interrupção antes do recibo pode repetir após reinício.

## Ajuste Debug Shared - 2026-10-11

Connection=Shared habilitado exclusivamente em Debug; Release usa Direct.
Teste de conexão externa simultânea adicionado e aprovado.
Suite de persistência: 13 testes aprovados; total acumulado: 82.
