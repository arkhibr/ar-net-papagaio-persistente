# Domínio híbrido: rico vs. anêmico

## Regra direta

A decisão é tomada por entidade/agregado, não é única para a solution inteira, e é revisitável conforme a complexidade real daquela parte do domínio se revela.

- **Modele como agregado rico** (construtor privado, factory method, métodos de ação de negócio, invariantes validados internamente) quando a entidade representa uma decisão de negócio real, com invariantes que importam e que, se violados, geram um problema de verdade.
- **Deixe anêmico** o que é genuinamente só dado: listas de referência, configurações, entidades de leitura que nunca mudam de estado por conta própria, projeções/DTOs que existem só para exibição.
- **Promova de anêmico para rico quando** precisar adicionar um `if` de validação antes de um setter em mais de um lugar do código: esse é o sinal de que a entidade "ganhou" uma regra e deixou de ser genuinamente só dado.

## Fronteira de agregado entre módulos

Entre módulos, a fronteira de agregado é a própria fronteira de módulo. Ao decidir se algo é uma coleção _dentro_ de um agregado existente ou um agregado _novo_ referenciado por id: se as duas entidades nunca precisam ser consistentes na mesma transação, são agregados separados, referenciados por id, nunca por navegação de objeto.

Num monólito modular, esse teste ganha um degrau extra: **se as duas entidades vivem em módulos diferentes, elas já são agregados separados por definição**, já que módulos diferentes são bounded contexts diferentes (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)). A pergunta "deveria ser um agregado só?" só faz sentido _dentro_ do mesmo módulo; entre módulos, a resposta já está decidida antes de perguntar.

## Onde a regra de negócio não mora

Autorização/permissão nunca entra dentro do agregado, mesmo que pareça regra de negócio. Teste rápido: **a regra continuaria valendo se a operação fosse disparada por um processo automatizado, sem usuário logado?** Se sim, é invariante de domínio (mora no agregado). Se não, é permissão (mora na Application/Api — ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)).

Esse teste é equivalente ao usado em [`16-validacao-sintatica-vs-invariante.md`](16-validacao-sintatica-vs-invariante.md) para a mesma fronteira: "depende de quem está pedindo, não só do estado do agregado?". Os dois testes sempre chegam à mesma conclusão; use qualquer um dos dois, o que for mais natural no contexto.

Uma mesma ação frequentemente combina as duas coisas: uma regra de negócio sobre o valor/estado da operação e uma restrição sobre quem pode executá-la. O teste acima decompõe o caso em vez de tratar a ação inteira como uma coisa só:

- Uma operação tem um limite de valor acima do qual passa a exigir uma segunda aprovação. O limite em si (e o fato de a operação mudar de estado para "aguardando aprovação" acima dele) continua valendo mesmo disparado por um processo automatizado: é invariante de domínio, mora no agregado. Já a lista de quem tem permissão para dar essa aprovação, e a checagem de que o usuário logado que está tentando aprovar consta nessa lista, não fazem sentido sem um usuário logado: são permissão, moram fora do agregado. Não é um caso-limite nem uma mistura das duas categorias: é a "terceira categoria" descrita em [`16-validacao-sintatica-vs-invariante.md`](16-validacao-sintatica-vs-invariante.md): a checagem depende de quem está pedindo, não do estado do agregado, então nem é invariante nem é validação sintática.
- Um item só pode ser cancelado antes de entrar em determinado estágio do seu ciclo de vida. Essa janela de tempo/estado é invariante de domínio, mora no agregado, e vale para qualquer chamador. Já a regra de que só quem criou o item (ou um perfil com privilégio elevado) pode cancelá-lo é permissão, mora na Application/Api.

## Núcleo funcional, casca imperativa

As regras acima — o agregado valida os próprios invariantes, a autorização e o I/O ficam fora dele — são facetas de um princípio único: **o domínio é um núcleo funcional (puro e determinístico); o efeito colateral vive na casca (`Infrastructure`/`Api`).**

**Regra direta: o `Domain` não faz I/O, não lê relógio, não gera aleatoriedade nem consulta configuração por conta própria.** Todo estado ambiente chega resolvido como parâmetro do método de negócio: o tempo vem como `DateTimeOffset` que a `Application` obteve do `TimeProvider` (ver [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md), "Testabilidade de tempo"); o dado de outro módulo vem como snapshot (ver [`29-modelagem-e-dados-entre-modulos.md`](29-modelagem-e-dados-entre-modulos.md)). A consequência é que o agregado é um POCO testável passando valores literais — sem mock, sem banco, sem DI.

O efeito colateral — persistência, publicação de evento, chamada externa — acontece na borda, depois que o núcleo decidiu: o commit e o dispatch pós-commit no `UnitOfWorkBehavior` (ver [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md)), a chamada externa no adapter (ver [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md)). O núcleo decide *o quê*; a casca executa *o efeito*.

Corolário de imutabilidade: as mensagens que atravessam o pipeline (`Command`/`Query`/DTO) são imutáveis (`record`) — dado que trafega não muda no caminho (ver [`03-commands-e-queries.md`](03-commands-e-queries.md)).

## Veja também

- [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md): módulo como unidade de fronteira
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): referência entre agregados de módulos diferentes
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): onde a permissão mora, fora do agregado
- [`29-modelagem-e-dados-entre-modulos.md`](29-modelagem-e-dados-entre-modulos.md): por que o invariante mora dentro da fronteira de consistência, e como o dado de outro módulo entra no agregado (snapshot)
