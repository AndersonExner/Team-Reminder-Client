# Correção da inicialização instalada

A publicação Release deve incluir os recursos WinUI da própria aplicação, além dos runtimes. A ausência do arquivo TeamReminder.Windows.pri impede carregar a interface.

Critérios de aceite:
- O publish inclui TeamReminder.Windows.pri.
- A geração falha caso o arquivo não tenha sido produzido.
- O executável publicado abre uma janela sem encerrar por falha.
- O instalador inclui o arquivo e mantém os dados locais em atualizações.

Validação: Release publicado encerra ao remover o PRI e abre a janela Team Reminder ao restaurá-lo. Build Release sem erros. Instalador recompilado com o recurso incluso.
