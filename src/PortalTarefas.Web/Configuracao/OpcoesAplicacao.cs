using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace PortalTarefas.Web.Configuracao;

// Roteiro 8: configurações tipadas e VALIDADAS NA INICIALIZAÇÃO (ValidateOnStart).
// Se algo obrigatório faltar, a aplicação não sobe e a mensagem diz QUAL chave falta e
// como fornecê-la — sem nunca exibir valores (que podem ser segredos).
// A lista completa de variáveis está em .env.example.

/// <summary>Seção ConnectionStrings.</summary>
public sealed class OpcoesConexao
{
    public const string Secao = "ConnectionStrings";

    [Required(ErrorMessage = "Defina ConnectionStrings:PortalTarefas (variável de ambiente ConnectionStrings__PortalTarefas ou User Secrets).")]
    public string? PortalTarefas { get; set; }
}

/// <summary>Seção Dependencias:Calendario.</summary>
public sealed class OpcoesCalendario
{
    public const string Secao = "Dependencias:Calendario";

    [Required(ErrorMessage = "Defina Dependencias:Calendario:UrlBase (variável Dependencias__Calendario__UrlBase).")]
    [Url(ErrorMessage = "Dependencias:Calendario:UrlBase deve ser uma URL absoluta.")]
    public string? UrlBase { get; set; }
}

/// <summary>Seção Limites:Login.</summary>
public sealed class OpcoesLimiteLogin
{
    public const string Secao = "Limites:Login";

    [Range(1, 10_000, ErrorMessage = "Limites:Login:PermitidasPorJanela deve estar entre 1 e 10000.")]
    public int PermitidasPorJanela { get; set; } = 5;

    [Range(1, 3600, ErrorMessage = "Limites:Login:JanelaSegundos deve estar entre 1 e 3600.")]
    public int JanelaSegundos { get; set; } = 60;
}

public static class ConfiguracaoValidada
{
    public static IServiceCollection AddConfiguracaoValidada(this IServiceCollection servicos)
    {
        Validar(servicos.AddOptions<OpcoesConexao>().BindConfiguration(OpcoesConexao.Secao));
        Validar(servicos.AddOptions<OpcoesCalendario>().BindConfiguration(OpcoesCalendario.Secao));
        Validar(servicos.AddOptions<OpcoesLimiteLogin>().BindConfiguration(OpcoesLimiteLogin.Secao));
        return servicos;
    }

    private static void Validar<T>(OptionsBuilder<T> opcoes) where T : class
    {
        opcoes.ValidateDataAnnotations();
        // O gerador do documento OpenAPI (executado no build) inicia a aplicação só para ler os
        // endpoints, sem segredos nem banco. Nesse caso a validação fica para o primeiro uso.
        if (!GerandoDocumentoOpenApi)
            opcoes.ValidateOnStart();
    }

    private static readonly bool GerandoDocumentoOpenApi =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
