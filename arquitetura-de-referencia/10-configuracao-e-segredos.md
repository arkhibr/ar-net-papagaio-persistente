# Configuração e segredos

## Quem lê configuração, e `IOptions<T>`

Só a camada de composição (Api/Worker) e as classes `DependencyInjection` de cada módulo leem `IConfiguration` diretamente; `Domain` nunca lê configuração. Qualquer configuração usada por mais de um lugar, ou com mais de um valor relacionado, vira uma classe de opções fortemente tipada (`IOptions<T>`), registrada com `.ValidateDataAnnotations().ValidateOnStart()`: a aplicação falha na inicialização se uma configuração obrigatória estiver ausente, em vez de falhar em produção na primeira requisição que precisar dela.

## Segredo em arquivo de configuração

Segredo nunca vai em arquivo de configuração versionado.

| Arquivo/local | Conteúdo | Vai para o controle de versão? |
| --- | --- | --- |
| Configuração base (`appsettings.json` ou equivalente) | Defaults não sensíveis, iguais em todo ambiente | Sim |
| Overrides por ambiente | Valores não sensíveis por ambiente | Sim |
| Segredo de desenvolvimento local | **Nunca** em arquivo de configuração | Não: usar `dotnet user-secrets` ou equivalente |
| Segredo de produção | **Nunca** em arquivo dentro do repositório, nunca embutido em imagem de container | Não: secret store gerenciado |

**Regra de bloqueio, não de convenção:** se um segredo real chegou a ser commitado, é considerado comprometido. A correção é rotacionar a credencial (ou reemitir o certificado) no serviço de origem, não só remover do histórico do git.

## Onde vive o segredo de produção

O mecanismo concreto (Key Vault, Secrets Manager, variável de ambiente injetada pelo orquestrador) é escolha de infraestrutura de deploy, não de arquitetura de código. A aplicação continua lendo tudo através de `IConfiguration`/`IOptions<T>`, sem saber de onde o valor físico vem: trocar o provedor de configuração é mudança de uma linha na composição raiz, não uma mudança em nenhuma camada interna.

## Chave de criptografia de sessão/cookie

Qualquer chave usada para cifrar cookie de sessão (ex.: ASP.NET Core Data Protection) precisa estar externalizada e compartilhada entre réplicas: num store acessível por **todas** as réplicas do processo, não em disco local do container. Isso não é opcional a partir do momento em que existe mais de uma réplica atrás de um load balancer: se a chave for local a cada instância, o cookie cifrado por uma réplica não é decifrável pela outra, e o usuário é deslogado de forma intermitente, dependendo de qual réplica atendeu a requisição, sem relação nenhuma com revogação de sessão intencional.

O store concreto (banco relacional já usado pela aplicação, Redis, blob storage) é escolha de infraestrutura. **O critério é ser externo ao processo e alcançável por qualquer réplica**, não uma tecnologia específica. Reaproveitar um store que a aplicação já opera (o mesmo banco da sessão, por exemplo) evita introduzir uma peça de infraestrutura nova só para isso.

## Checklist antes de expor uma configuração nova

- Tem mais de um valor relacionado, ou é usada em mais de um lugar? → `IOptions<T>` com validação.
- É um valor sensível? → nunca em arquivo de configuração versionado; `user-secrets` em desenvolvimento, secret store gerenciado em produção.
- Precisa mudar sem novo deploy? → considerar um provedor de configuração dinâmica, e só se essa necessidade for real, não por padrão.

## Quando uma configuração não precisa ser externalizada

Nem todo valor usado pela aplicação é configuração no sentido deste documento. Não externalizar:

- **Invariante de domínio** (ex.: número máximo de itens permitido por regra de negócio): pertence ao `Domain`, expresso em código, não em `IConfiguration`.
- **Valor fixo por contrato de arquitetura** (ex.: nome de uma claim, chave de um header interno): faz parte do desenho do sistema, não varia por ambiente.
- **Constante que só muda com alteração de código** (ex.: versão de um algoritmo interno, enum, limite estrutural sem relação com ambiente de execução): externalizá-la não adiciona flexibilidade real, só uma indireção sem uso.

Critério prático: se o valor nunca precisa diferir entre ambientes e mudá-lo exige de qualquer forma revisão de código/novo deploy, ele é constante de código, não configuração.

## Veja também

- [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md): onde a sessão e a chave são consumidas
- [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md): credenciais técnicas por adapter
