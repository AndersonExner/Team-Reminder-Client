# Desenvolvimento guiado por especificações

## Ciclo

Especificação -> plano técnico -> tarefas -> implementação -> validação.

1. spec.md descreve o comportamento observável e seus critérios de aceitação.
2. plan.md descreve como construir esse comportamento.
3. tasks.md divide o trabalho em entregas pequenas e registra as verificações.
4. O código implementa os requisitos.
5. A validação produz evidência de que os critérios foram atendidos.

## Exemplo

Requisito: um alarme salvo permanece disponível depois de reiniciar.
Plano: usar um repositório LiteDB em um arquivo no perfil do usuário.
Tarefa: implementar inserção e leitura.
Verificação: salvar, fechar o banco, reabrir o mesmo arquivo e consultar o alarme.

## Regras de trabalho

- Usar identificadores de requisito para ligar tarefas e validações à especificação.
- Registrar hipóteses como decisões propostas, evitando ambiguidades escondidas.
- Atualizar a especificação quando o comportamento mudar.
- Marcar tarefas apenas depois da execução e verificação.
- Não confundir documento escrito com funcionalidade implementada.
- Escrever testes para regras e riscos reais, sem reproduzir detalhes internos.
- Manter uma especificação por entrega coerente, sem especificar todo o produto de uma vez.
- Usar Git para revisar documentos e código juntos; não há obrigação de ferramenta SDD específica.

## Definition of Done

Uma entrega está pronta quando seus critérios de aceitação foram verificados,
as falhas foram corrigidas e os documentos refletem o comportamento implementado.
Registrar no tasks.md comandos, resultados e limitações materiais.

## Próximo exercício

Leia a especificação 001 como usuário: ela permite prever o comportamento?
Leia o plano como desenvolvedor: ele permite construir a solução?
Leia as tarefas como executor: elas permitem saber o que falta e como verificar?

## Branches

Cada alteração deve ocorrer em uma branch feature/. Nunca desenvolver ou commitar diretamente na main.
