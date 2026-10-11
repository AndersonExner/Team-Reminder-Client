# 005 — Bandeja, início com Windows e instalação

Status: cliente implementado em feature/visual-usability; build e smoke aprovados. Instalador preparado, sem compilação por ausência do Inno Setup; revisão manual pendente.

## Comportamento e aceitação
- AC01: ícone próprio na bandeja e executável. Fechar a janela oculta para bandeja;
  minimizar mantém execução. Sair pelo menu encerra processo, banco e ícone.
- AC02: hover mostra resumo textual dos próximos alarmes (limite nativo de 127 caracteres).
  Clique abre menu com até três próximos alarmes, Adicionar alarme, Abrir, Configurações e Sair.
  Duplo clique restaura a janela. Tooltip não possui botões.
- AC03: Adicionar abre aba Alarmes e foca título. Se há edição em curso, preserva
  os campos e pede concluir/cancelar a edição antes de novo cadastro.
- AC04: instalar por usuário oferece checkbox Iniciar com o Windows, inicialmente desmarcado.
  Quando habilitado, registrar inicialização do executável instalado com --tray.
- AC05: iniciar com --tray não mostra a janela, mas carrega banco e inicia motor.
  Sem --tray, abrir janela normalmente. Configurações permite habilitar/desabilitar startup.
- AC06: se bandeja não puder ser criada, mostrar janela; nunca manter app invisível sem acesso.
- AC07: desinstalar remove registro de startup e arquivos de instalação, preservando alarmes.
- AC08: abrir outra instância tenta restaurar a janela existente sem abrir outro banco.
- AC09: alarmes continuam disparando após ocultar a janela e ao iniciar com --tray.

## Limites
Windows deve estar ligado e usuário conectado. Suspensão/desligamento não executa alarmes;
o motor recupera atrasos quando volta a executar. Windows pode colocar ícone em ícones ocultos.
Instalador não muda automaticamente a configuração de startup do computador de desenvolvimento.
Supersede o comportamento de encerramento ao fechar das specs 001/003/004.
