# Autenticação e sessão

## Fronteira de confiança

O backend é a fronteira de confiança, nunca o browser: o usuário autenticado não é "passado" pelo frontend para os endpoints, ele é reconstruído pelo backend a partir de uma sessão validada. A cadeia de confiança:

```text
IdP (qualquer protocolo)
 ↓
token/assertion validada
 ↓
identidade externa
 ↓
identidade interna
 ↓
sessão
 ↓
cookie protegido
 ↓
ClaimsPrincipal
 ↓
ICurrentUser
 ↓
Authorization
 ↓
Application / Domain
```

Tudo que vem diretamente do navegador (header customizado, query string, corpo da requisição) é tratado como não confiável para determinar identidade. Ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md) para autorização por recurso, que é um assunto separado deste.

## Tokens e o padrão BFF

Access token e refresh token (quando o protocolo de autenticação os emite) nunca vivem no frontend — o padrão default é BFF: eles ficam só no servidor, nunca no navegador. **Nunca armazenar token no frontend** (`localStorage`/`sessionStorage`). O frontend recebe apenas um cookie de sessão `HttpOnly`, `Secure`, com `SameSite` apropriado.

**Exceção isolada:** protótipo interno de baixo risco, sem dado sensível e sem exposição fora de uma rede controlada, pode armazenar token no frontend. Fora desse caso explícito, a regra é categórica.

## Identidade interna canônica

O sistema mantém um identificador interno próprio (`UserId`), independente do protocolo do IdP, nunca e-mail, login ou qualquer identificador que possa mudar ou ser reciclado, mapeado a partir da identidade externa:

```text
User
  Id
  SecurityVersion

ExternalIdentity
  UserId
  Provider
  Issuer
  ExternalSubject
```

Essa camada de mapeamento é o que permite trocar o protocolo do IdP (ex.: de WS-Federation/SAML para OIDC) sem alterar o modelo de sessão nem o restante da aplicação: só o processo de resolução da identidade externa muda.

## Sessão server-side

Estado autoritativo da sessão fica no servidor, não só no conteúdo do cookie:

```text
AuthSession
  SessionId
  UserId
  CreatedAt
  ExpiresAt
  RevokedAt
  SecurityVersion
```

Isso permite logout administrativo, revogação imediata e expiração centralizada. Um cookie totalmente autocontido permanece utilizável até expirar, a menos que exista verificação externa de revogação a cada requisição. O custo é uma consulta ao datastore de sessão por requisição; para a maioria dos sistemas corporativos, revogação e rastreabilidade valem mais que a latência que uma sessão puramente stateless economizaria.

**`SecurityVersion`** em `User` e em `AuthSession`: quando divergem, a sessão é considerada inválida. Permite invalidar todas as sessões de um usuário (comprometimento, bloqueio administrativo, reset de acesso) sem precisar localizar e apagar cada sessão individualmente.

## `ICurrentUser` em vez de `HttpContext` no domínio

**Esta é a definição canônica de `ICurrentUser` para toda a referência** — os demais documentos exibem apenas o subconjunto de membros que cada fluxo usa, mas nenhum acrescenta membro fora desta lista:

```csharp
public interface ICurrentUser
{
    Guid UserId { get; }        // identidade interna canônica; nunca e-mail/login
    Guid SessionId { get; }
    bool IsAuthenticated { get; }
    bool IsSystemActor { get; } // true para identidade de ator automatizado (ver 04 e 21)
    bool IsInRole(string role); // papel genérico para leitura A de autorização (ver 12)
}
```

Implementação na camada Web/Infrastructure; `Domain` e `Application` nunca dependem de `HttpContext` diretamente. Mantém só o contexto necessário: não se transforma num objeto gigante de sessão carregando permissão ou dado de negócio (isso muda com frequência e não deveria estar embutido na sessão nem no cookie).

**`ICurrentUser` é identidade, não consulta.** Ele responde "quem é o ator", não "este ator tem vínculo com tal recurso?". Consulta de vínculo ator↔recurso é responsabilidade de um serviço dedicado (`IAuthorizationContext`, ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)), nunca um método pendurado aqui: manter a identidade sem acesso a banco é o que a mantém injetável em qualquer camada sem arrastar infraestrutura junto.

## CSRF

Como o cookie de sessão é enviado automaticamente pelo navegador em qualquer requisição, inclusive as disparadas por outro site, autenticação por cookie sozinha não garante que a requisição partiu da aplicação legítima. **Critério objetivo: toda operação que muda estado através do cookie de sessão exige token antiforgery/XSRF validado no backend**, tipicamente `POST`/`PUT`/`PATCH`/`DELETE` (ver [`09-convencoes-rest-api.md`](09-convencoes-rest-api.md) para o mapeamento de verbo por operação). `GET`/leitura não exige o token, por não mudar estado. `SameSite` apropriado no cookie é complementar, não substitui o token: reduz a superfície de ataque, mas não cobre todos os vetores (ex.: navegadores/configurações legadas, subdomínios confiáveis).

## Chave de criptografia do cookie sobrevivendo a réplica e reinício

Ver [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md): a chave de criptografia do cookie de sessão precisa estar num store externo ao processo, acessível por qualquer réplica: requisito que se torna obrigatório a partir do momento em que existe mais de uma réplica do processo.

## Nota de aplicação

Um sistema concreto pode precisar integrar um IdP institucional legado via um protocolo mais antigo (ex.: WS-Federation/SAML), com evolução planejada para OIDC. A camada de mapeamento de identidade externa (`ExternalIdentity`) é exatamente o que absorve essa transição sem afetar o restante do sistema. Da mesma forma, o store de sessão e a chave de Data Protection podem viver num banco relacional já disponível em vez de Redis, quando não há justificativa operacional para introduzir uma peça de infraestrutura nova só para isso (ver [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md)).

## Veja também

- [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md): onde a chave de criptografia do cookie e os segredos de autenticação vivem
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): mecanismo de autorização dependente de dado/recurso
