namespace SharedKernel;

/// <summary>
/// Marcador do serviço estreito de consulta de vínculo ator-recurso de um módulo
/// (arquitetura/12-autorizacao-por-recurso.md). Cada módulo declara, no próprio Contracts, a
/// interface com perguntas nomeadas por intenção (ex.: "é o técnico atribuído?") e a implementa
/// na Infrastructure. Não é ICurrentUser: mantém a identidade livre de acesso a banco.
/// </summary>
public interface IAuthorizationContext;
