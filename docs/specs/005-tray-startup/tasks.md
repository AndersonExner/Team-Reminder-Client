# Tarefas e evidências

- [x] Spec e plano antes da implementação.
- [x] Ícone ICO em sete resoluções, com gerador vetorial reproduzível e prévia inspecionada.
- [x] Bandeja, próximos alarmes, ações e manutenção do motor ao fechar a janela.
- [x] Início --tray independente de Loaded; falha da bandeja abre a janela.
- [x] Startup por usuário, opt-in nas configurações, sem habilitar no computador de desenvolvimento.
- [x] Restauração de instância existente por named pipe limitado ao usuário Windows.
- [x] Fonte do instalador Inno Setup e script de publicação self-contained x64.
- [x] Build Debug/x64 sem erros/avisos; 69 Core + 13 Local aprovados.
- [x] Smoke --tray: janela oculta, som/notificação aceitos, recibo persistido, reinício sem repetição.
- [x] Smoke fechamento: janela oculta e alarme entregue após CloseMainWindow.
- [x] Smoke segunda instância: encerra instância extra e restaura janela original.
- [ ] Compilar Setup.exe: ISCC.exe/Inno Setup não disponível neste computador.
- [ ] Instalar/desinstalar e validar logout/login e configurações de startup.
- [ ] Revisão de menu, saída, temas e DPI conforme manual-checklist.md.

Verificado em 11/10/2026. Smoke usa banco isolado e encerra apenas os processos que criou.
Diagnósticos de janela existem somente em Debug. Aceitação de áudio não comprova audibilidade.
A publicação Release self-contained foi aprovada; conferir novamente após alterações subsequentes.
