using System.Reflection;

namespace SharedKernel.Modules;

/// <summary>
/// Declaração, feita pelo próprio módulo dentro do Add{Modulo}Module, de quais assemblies
/// pertencem a ele (o Contracts e o assembly interno). É a partir disto que o ISender composto
/// e os pipeline behaviors descobrem a qual módulo uma mensagem pertence
/// (arquitetura/01-estrutura-de-projetos-monolito-modular.md, "Como Api compõe o sistema": o
/// SharedKernel nunca lista os módulos da solution; cada módulo se registra).
/// </summary>
public sealed record ModuleRoute(string ModuleKey, IReadOnlyCollection<Assembly> Assemblies);
