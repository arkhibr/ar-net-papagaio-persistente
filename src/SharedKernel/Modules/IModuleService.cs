namespace SharedKernel.Modules;

/// <summary>
/// Porta que entrega a implementação de <typeparamref name="T"/> do módulo dono de uma mensagem
/// (ex.: o IUnitOfWork de Chamados para um Command de Chamados, o de Catalogo para um Command de
/// Catalogo). Cada módulo registra as próprias implementações das portas do SharedKernel como
/// serviço keyed pela chave do módulo; nenhuma porta do SharedKernel é registrada sem chave, então
/// o segundo módulo com escrita nunca herda silenciosamente o DbContext do primeiro.
///
/// Os behaviors e o ISender composto recebem esta porta por construtor. A resolução dinâmica
/// por chave fica concentrada em ModuleService&lt;T&gt;, o único ponto que conversa com o
/// container — ver Nota de aplicação em achados.md (M11/P1).
/// </summary>
public interface IModuleService<out T>
    where T : notnull
{
    T For(Type messageType);
}
