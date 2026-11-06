namespace Api.Bff;

/// <summary>
/// Model da view de login do backend (Views/Auth/Login.cshtml; adaptacao-bff-angular.md, B10). A
/// seção de personas só é renderizada quando <see cref="Desenvolvimento"/> é true (decisão D5).
/// </summary>
internal sealed record PaginaDeLoginModel(string ReturnUrl, string? Erro, bool Desenvolvimento);
