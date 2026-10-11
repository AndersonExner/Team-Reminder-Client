# Visão do produto e MVP 1

## Propósito

Organizar coisas que pessoas precisam lembrar e realizar juntas.
O MVP 1 estabelece a base local do cliente Windows antes da colaboração.

## Usuário inicial

Uma pessoa usando seu computador Windows, sem conta e sem conexão obrigatória.

## Escopo completo do MVP 1

- Criar, editar, excluir e listar alarmes.
- Configurar data, horário e antecedência.
- Configurar recorrência.
- Entregar notificações Windows.
- Adiar um alerta.
- Manter execução em segundo plano com System Tray.

Cada grupo terá uma especificação própria antes da implementação.
A primeira entrega cobre somente criação, listagem e persistência.

## Fora do MVP 1

Backend, PostgreSQL, autenticação, Spaces, chat, responsáveis compartilhados,
confirmações sincronizadas, web, mobile, integrações e IA.

## Persistência

No MVP 1, LiteDB é a fonte oficial dos dados locais.
Uma instância compartilhada de LiteDatabase atende os serviços do processo.
O arquivo fica no perfil do usuário, fora do repositório e da pasta de instalação.

No MVP 2, PostgreSQL passa a representar os dados oficiais sincronizados.
A migração de alarmes locais será especificada nessa fase.

## Entregas propostas

1. Cadastro, listagem e persistência.
2. Edição e exclusão.
3. Agendamento e notificações Windows.
4. Antecedência e adiamento.
5. Recorrência.
6. System Tray e ciclo de vida em segundo plano.

## Decisões ainda necessárias

- Comportamento de alertas vencidos após desligamento ou suspensão.
- Política quando o usuário altera o fuso horário.
- Regras de recorrência mensal e horário de verão.
- Comportamento de fechar a janela quando houver System Tray.

Essas decisões pertencem às próximas especificações e não bloqueiam o cadastro local.
