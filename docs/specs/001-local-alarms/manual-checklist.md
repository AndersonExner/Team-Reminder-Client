# Validação manual - 001

Status: pendente. Não marcar itens sem executar.

1. Execute o aplicativo conforme o README.
2. Sem alarmes existentes, confira a orientação para criar o primeiro.
3. Tente salvar título vazio e somente espaços; confira a mensagem junto ao título.
4. Tente título com mais de 120 caracteres; confira a validação.
5. Tente salvar sem data/horário e com horário passado; confira a mensagem.
6. Cadastre um alarme futuro válido; confira o registro e a limpeza dos campos.
7. Cadastre outro mais próximo; confira a ordenação cronológica.
8. Feche e reabra o aplicativo; confira os mesmos registros e horários.
9. Cadastre um alarme para o próximo minuto; confira o disparo do alarme.
   Com a spec 003 implementada, confira som/aviso e o estado Disparado.
10. Confira texto legível, navegação por teclado e mensagens nos temas claro/escuro.
11. Com a conexão de rede indisponível, repita cadastro e reabertura.

Não altere o banco do usuário para provocar falhas. Os testes automatizados
cobrem falhas de leitura/gravação e preservação de arquivo inválido.

Registre data, resultado e problemas encontrados em tasks.md.
