# 009 — Sons por alarme

## Comportamento
- S01: criar/editar permite Usar padrão do aplicativo, Notificação, Exclamação, Erro crítico do Windows, Sem som ou Arquivo personalizado.
- S02: Configurações permite escolher o padrão, incluindo silêncio e arquivo personalizado. Alarmes que seguem o padrão acompanham alterações futuras. Padrão inicial: Notificação do Windows. Alarmes antigos seguem o padrão.
- S03: selecionar MP3/WAV (até 20 MiB) copia para Sounds no perfil local. Validar a cópia com o decodificador Windows antes de aceitar. Cancelar seleção mantém a escolha anterior; falha não substitui arquivo válido.
- S04: LiteDB guarda tipo, identificador do arquivo gerenciado e nome exibido; nunca depende do caminho original. Arquivos permanecem fora do banco.
- S05: Ouvir/Parar permite prévia. Fechar o modal interrompe a prévia. Disparo real interrompe prévia e toca uma vez, sem repetição. Alarmes simultâneos usam o último som iniciado, mantendo todos os avisos visuais.
- S06: sons do Windows seguem o esquema atual, incluindo eventos silenciosos. Arquivo ausente/inválido usa Notificação do Windows e informa a falha; falha de áudio não impede registrar e mostrar o alarme. Sem som nunca usa fallback sonoro.
- S07: remover cópias sem referências somente após carregar o banco com sucesso. Preservar arquivos referenciados pelo padrão ou qualquer alarme. Cancelar cadastro/edição descarta apenas cópias sem referências.
- S08: alarmes, escolha padrão e cópias sobrevivem ao reinício e atualização do instalador.

Não inclui edição/conversão de áudio, volume por alarme nem repetição até confirmação. Lembretes antecipados mantêm o comportamento atual da bandeja.
