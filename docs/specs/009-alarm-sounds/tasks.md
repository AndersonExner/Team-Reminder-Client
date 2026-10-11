# Tarefas e evidências

- [x] S01/S02: controle de som no modal e nas configurações; preferência padrão persistida.
- [x] S03/S04/S07: importação limitada, persistência LiteDB e limpeza conservadora, protegendo importações em andamento.
- [x] S05/S06: MediaPlayer para WAV/MP3, eventos Windows, silêncio, prévia/Parar e fallback.
- [x] S08: README atualizado; build/publish e instalador recompilados com recursos WinUI.

## Verificações automatizadas
- `dotnet test TeamReminder.Client.sln -p:Platform=x64`: 87 Core + 30 Local, todos passaram.
- `dotnet test TeamReminder.Client.sln -c Release -p:Platform=x64`: 87 Core + 29 Local, todos passaram. O teste de conexão Shared só é compilado em Debug; Release usa Direct.
- Build WinUI Debug/x64: zero avisos e erros.
- `scripts/Test-AlarmSounds.ps1`: decodificação real de WAV e MP3; importação, remoção do original, reprodução dos eventos Windows, silêncio, padrão personalizado persistido, arquivo ausente com fallback, rejeição de MP3 corrompido e construção XAML do modal.
- Evidência de áudio: `artifacts/audio-smoke/006b780b-e792-4d4d-b706-e783d327e4b1/report.json`.
- `scripts/Test-AlarmDelivery.ps1 -StartInTray -SoundKind Custom -AudioFile <MP3>`: reprodução aceita sem fallback, notificação Windows, janela oculta, segunda instância restaura janela, ocorrência persistida e reinício sem repetição. Evidência: `artifacts/smoke/0f204506-799d-438a-a47a-5b843d2c06ec`.
- Smoke com Silent: entrega sem fallback, persistência e reinício sem repetição (`artifacts/smoke/53977263-6053-4c5a-85c9-43025cb8e94d`).
- Smoke com Custom e arquivo ausente: fallback explícito, aviso entregue, persistência e reinício sem repetição (`artifacts/smoke/65f64ff7-bbfe-4d02-8e54-d24b1e06a3f8`).

## Limites da validação
Aceitação da API não comprova volume audível. A interação manual com o seletor de arquivos, revisão visual dos dois temas e atualização pelo assistente Inno continuam no checklist manual; os testes nativos validam os codecs e recursos usados por esses fluxos, com banco e arquivos isolados. O fluxo completo do instalador no computador do amigo requer validação nesse computador.