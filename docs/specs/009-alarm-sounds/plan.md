# Plano
1. Domínio: AlarmSound com enum estável, validação e valor Default para registros antigos; levar seleção pelo serviço/ViewModel.
2. Infra: mapear campos no documento LiteDB; biblioteca de arquivos com IDs GUID, cópia limitada, validação delegada ao Windows, limpeza conservadora e preferência JSON escrita atomicamente.
3. Windows: controle reutilizado no modal e configurações; FileOpenPicker associado à janela; MediaPlayer para WAV/MP3, PlaySound com aliases para eventos do Windows. Preview separado do player do disparo, interrompido antes de alarmes reais.
4. Validar compatibilidade, round-trip após reinício, cancelamento, arquivos inválidos e referências compartilhadas; build WinUI Debug/Release e smoke de entrega com banco isolado.
