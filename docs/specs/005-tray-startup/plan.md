# Plano

Usar NotifyIcon/ContextMenuStrip do Windows Forms como componente de bandeja na thread UI
da aplicação WinUI. Não criar segunda janela Forms ou segundo loop de mensagens.
Atualizar próximos alarmes em memória a cada tick e ao abrir o menu; tratar falha da bandeja.
WinUI AppWindow.Closing cancela fechamento comum e oculta, enquanto Sair fecha definitivamente.
Início --tray carrega explicitamente ViewModel/motor sem depender de Loaded da janela.
Registro HKCU/Software/Microsoft/Windows/CurrentVersion/Run, valor TeamReminder,
com caminho entre aspas e --tray; opção local lê estado real desse registro.
Inno Setup cria instalação por usuário, atalhos e opção de startup; publicação self-contained x64.
Ícone ICO multi-resolução gerado a partir de geometria vetorial própria, sem dependência de imagem externa.
Validar build, regressões e disparo com janela oculta; revisão de interações/instalador segue checklist.

Restauração usa named pipe por SID com PipeOptions.CurrentUserOnly, sem buscar janelas por título.
A primeira instância mantém o mutex; pedidos de abertura restauram a janela na thread WinUI.
Referenciar Microsoft.WindowsDesktop.App.WindowsForms explicitamente evita acionar o compilador WPF.
