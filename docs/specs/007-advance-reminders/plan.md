# Plano

EarlyReminderScheduler separado do motor principal, com relógio injetado e callbacks testáveis.
Recibos em coleção earlyReminderReceipts da mesma instância LiteDB, sem mudar documentos antigos.
Comparar snapshot antes de gravar, conservar ocorrência principal e limpar recibo ao excluir.
ViewModel executa pré-aviso somente quando há candidato e mantém rascunho/modal intactos.
Configuração local advance-minutes.txt; janela injeta preferência e popup da bandeja.
Testar limite exato, reinício, falhas, recorrência e persistência/CAS usando relógio e banco isolados.
Verificar build em diretório isolado; aparência/duração exigem revisão manual no Windows.

Fonte: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0
