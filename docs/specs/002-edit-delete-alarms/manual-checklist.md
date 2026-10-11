# Validação manual - 002

Status: pendente. Executar antes de marcar como concluído.

1. Abra o aplicativo e cadastre dois alarmes futuros.
2. Sem seleção, confira que Editar e Excluir estão indisponíveis.
3. Selecione o primeiro e clique Editar; confira título, data e horário preenchidos.
4. Mude o título, escolha um horário posterior ao segundo alarme e salve.
   Confira a mudança de ordem e que não apareceu um registro extra.
5. Edite novamente, digite mudanças e clique Cancelar edição; confira o registro original.
6. Tente editar com título vazio e horário passado; confira a validação e os campos preservados.
7. Inicie edição do primeiro e selecione o segundo. Confira que o formulário continua editando o primeiro.
8. Exclua o segundo: confira título/horário no diálogo e clique Cancelar.
   Confira que os registros e campos não mudaram.
9. Repita e confirme Excluir. Confira que somente o segundo desapareceu e que a edição do primeiro continua.
10. Exclua o primeiro e confirme; confira formulário limpo e indicação de lista vazia.
11. Cadastre e edite um alarme, feche e reabra; confira as alterações persistidas.
12. Exclua esse alarme, feche e reabra; confira que ele não retorna.
13. Confira diálogo legível, botão padrão Cancelar, navegação por teclado e temas claro/escuro.

Registre resultados em tasks.md. Falhas de armazenamento e Id ausente são cobertos
por testes controlados; não altere o banco real do usuário para provocar falhas.
