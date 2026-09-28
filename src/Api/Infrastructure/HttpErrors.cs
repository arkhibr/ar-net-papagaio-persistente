using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Infrastructure;

/// <summary>
/// Fábrica única do corpo de erro RFC 9457 (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md),
/// usada pelo GlobalExceptionHandler, por ResultExtensions, pelo 400 automático do
/// [ApiController] e pelos 400 de cabeçalho ausente (A7/M7 de achados.md). Todo 400 carrega
/// errors[] com pointer/codigo/mensagem; type e title são fixos por caso.
/// </summary>
internal static class HttpErrors
{
    private const string BaseType = "erros/";

    /// <summary>
    /// Campos de Command que nunca vêm do corpo (cabeçalho ou ICurrentUser): o erro não aponta
    /// para campo de formulário.
    /// </summary>
    private static readonly HashSet<string> CamposForaDoCorpo = new(StringComparer.OrdinalIgnoreCase)
    {
        "IdempotencyKey", "RowVersion", "SolicitanteId", "TecnicoId",
    };

    public sealed record ErroDeCampo(string? Pointer, string Codigo, string Mensagem);

    public static ProblemDetails Validation(IEnumerable<ErroDeCampo> erros) =>
        ComErros(StatusCodes.Status400BadRequest, "validacao", "Um ou mais campos são inválidos",
            "Um ou mais campos são inválidos.", erros);

    public static ProblemDetails Validation(IEnumerable<SharedKernel.ValidationError> erros) =>
        Validation(erros.Select(e => new ErroDeCampo(Pointer(e.PropertyName), Codigo(e.ErrorCode), e.ErrorMessage)));

    public static ProblemDetails Validation(ModelStateDictionary modelState) =>
        Validation(modelState
            .Where(entrada => entrada.Value is { Errors.Count: > 0 })
            .SelectMany(entrada => entrada.Value!.Errors.Select(erro => new ErroDeCampo(
                PointerDeModelBinding(entrada.Key),
                "formato_invalido",
                string.IsNullOrWhiteSpace(erro.ErrorMessage) ? "Valor em formato inválido." : erro.ErrorMessage))));

    public static ProblemDetails MissingHeader(string cabecalho, string detalhe) =>
        ComErros(StatusCodes.Status400BadRequest, "cabecalho-obrigatorio", $"Cabeçalho {cabecalho} ausente ou inválido",
            detalhe, [new ErroDeCampo(null, "cabecalho_obrigatorio", detalhe)]);

    public static ProblemDetails BusinessRule(string mensagem) =>
        ComErros(StatusCodes.Status400BadRequest, "regra-de-negocio-violada", "Regra de negócio violada",
            mensagem, [new ErroDeCampo(null, "regra_de_negocio_violada", mensagem)]);

    /// <summary>DomainException que escapou do handler (anomalia; o caminho esperado é Result.Failure).</summary>
    public static ProblemDetails DomainAnomaly(string mensagem) =>
        ComErros(StatusCodes.Status400BadRequest, "regra-de-negocio-violada", "Regra de negócio violada",
            mensagem, [new ErroDeCampo(null, "estado_invalido", mensagem)]);

    /// <summary>
    /// Mesmo corpo para "não existe" e "sem acesso" (M3 de achados.md): detail fixo, nunca a
    /// mensagem de quem lançou.
    /// </summary>
    public static ProblemDetails NotFound() =>
        Simples(StatusCodes.Status404NotFound, "nao-encontrado", "Recurso não encontrado",
            "O recurso não existe ou você não tem acesso a ele.");

    public static ProblemDetails Forbidden(string detalhe) =>
        Simples(StatusCodes.Status403Forbidden, "nao-autorizado", "Não autorizado", detalhe);

    /// <summary>Sem sessão válida (adaptacao-bff-angular.md, B2): o Angular redireciona para /auth/login.</summary>
    public static ProblemDetails Unauthorized() =>
        Simples(StatusCodes.Status401Unauthorized, "nao-autenticado", "Não autenticado",
            "Sessão ausente ou expirada. Entre de novo.");

    /// <summary>Falha de CSRF, distinta do 403 de autorização (arquitetura/17; M1 de achados.md).</summary>
    public static ProblemDetails CsrfInvalid() =>
        Simples(StatusCodes.Status403Forbidden, "csrf-invalido", "Token antiforgery ausente ou inválido",
            "Recarregue a página e tente de novo.");

    /// <summary>Rate limiting do login local (adaptacao-bff-angular.md, B10).</summary>
    public static ProblemDetails TooManyRequests() =>
        Simples(StatusCodes.Status429TooManyRequests, "muitas-tentativas", "Muitas tentativas",
            "Aguarde um minuto e tente de novo.");

    public static ProblemDetails OperationInProgress(string detalhe) =>
        Simples(StatusCodes.Status409Conflict, "operacao-em-andamento", "Operação em andamento", detalhe);

    public static ProblemDetails Concurrency(string detalhe) =>
        Simples(StatusCodes.Status409Conflict, "conflito-de-concorrencia", "Conflito de concorrência", detalhe);

    public static ProblemDetails PreconditionFailed(string detalhe) =>
        Simples(StatusCodes.Status412PreconditionFailed, "versao-desatualizada", "Versão desatualizada", detalhe);

    public static ProblemDetails IdempotencyKeyReused(string detalhe) =>
        Simples(StatusCodes.Status422UnprocessableEntity, "idempotency-key-reutilizada", "Idempotency-Key reutilizada", detalhe);

    public static ProblemDetails Internal() =>
        Simples(StatusCodes.Status500InternalServerError, "erro-interno", "Erro interno", null);

    private static ProblemDetails Simples(int status, string tipo, string titulo, string? detalhe) => new()
    {
        Type = BaseType + tipo,
        Title = titulo,
        Status = status,
        Detail = detalhe,
    };

    private static ProblemDetails ComErros(int status, string tipo, string titulo, string detalhe, IEnumerable<ErroDeCampo> erros)
    {
        var problemDetails = Simples(status, tipo, titulo, detalhe);
        problemDetails.Extensions["errors"] = erros
            .Select(e => new { pointer = e.Pointer, codigo = e.Codigo, mensagem = e.Mensagem })
            .ToArray();
        return problemDetails;
    }

    /// <summary>Nome da propriedade do Command → campo JSON (camelCase, arquitetura/09).</summary>
    private static string? Pointer(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName) || CamposForaDoCorpo.Contains(propertyName))
        {
            return null;
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }

    /// <summary>"$.categoriaId" → "categoriaId"; a chave do objeto inteiro ("request", "") → null.</summary>
    private static string? PointerDeModelBinding(string chave)
    {
        if (chave.StartsWith("$.", StringComparison.Ordinal))
        {
            return chave[2..];
        }

        return string.IsNullOrEmpty(chave) || chave == "$" || chave == "request" ? null : Pointer(chave);
    }

    private static string Codigo(string errorCode) => errorCode switch
    {
        "NotEmptyValidator" or "NotNullValidator" => "obrigatorio",
        "EnumValidator" => "valor_invalido",
        "MaximumLengthValidator" or "MinimumLengthValidator" or "LengthValidator" => "tamanho_invalido",
        _ when !errorCode.EndsWith("Validator", StringComparison.Ordinal) && errorCode.Length > 0 => errorCode,
        _ => "formato_invalido",
    };
}
