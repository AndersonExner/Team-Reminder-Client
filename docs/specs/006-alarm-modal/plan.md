# Plano

ContentDialog AlarmEditorDialog com bindings no ViewModel compartilhado.
PrimaryButtonClick usa deferral e resultado bool de SaveAsync; mantém modal aberto em falhas.
Botão secundário Cancelar e Closing bloqueiam fechamento enquanto ViewModel está ocupado.
MainWindow centraliza abertura de novo/edição/bandeja com trava de um diálogo por vez.
BeginCreate/DiscardForm limpam rascunho; ResetForm obtém data e HH:mm de uma única leitura do relógio.
Fallback do tema muda para ElementTheme.Light, mantendo a persistência da spec 004.
Testar horário em mudança de dia/fuso, cancelamento sem escrita e resultado de salvar.
Compilar XAML, executar regressões e smoke de disparo; interações visuais seguem checklist manual.
