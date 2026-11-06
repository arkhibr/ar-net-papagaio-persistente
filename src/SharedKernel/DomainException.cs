namespace SharedKernel;

/// <summary>
/// Exceção lançada quando um invariante de domínio é violado dentro de um agregado.
/// Nunca usada para autorização (isso é IRequiresAuthorization/AuthorizationDeniedException,
/// fora do Domain) nem para validação sintática de campo isolado (isso é FluentValidation
/// na Application). Ver arquitetura/16-validacao-sintatica-vs-invariante.md.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
