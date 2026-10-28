using System.ComponentModel.DataAnnotations;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Tests.Roteiro02;

/// <summary>Testes unitários das Data Annotations e da validação customizada de prazo.</summary>
public class ValidacaoPrazoTests
{
    private static readonly RelogioFixo Relogio = RelogioFixo.Padrao;

    private static List<ValidationResult> Validar(TarefaInputModel modelo)
    {
        var provedor = new ProvedorSimples(Relogio);
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo, provedor, null), resultados, validateAllProperties: true);
        return resultados;
    }

    [Fact]
    public void Modelo_valido_nao_gera_erros()
    {
        var modelo = new TarefaInputModel { Titulo = "Estudar Razor", Prioridade = PrioridadeTarefa.Media, Prazo = Relogio.Hoje.AddDays(3) };
        Assert.Empty(Validar(modelo));
    }

    [Fact]
    public void Titulo_vazio_gera_mensagem_em_portugues()
    {
        var erros = Validar(new TarefaInputModel { Titulo = "" });
        Assert.Contains(erros, e => e.ErrorMessage == "Informe o título da tarefa.");
    }

    [Fact]
    public void Prioridade_alta_sem_prazo_e_invalida()
    {
        var erros = Validar(new TarefaInputModel { Titulo = "Urgente", Prioridade = PrioridadeTarefa.Alta, Prazo = null });

        var erro = Assert.Single(erros);
        Assert.Contains(nameof(TarefaInputModel.Prazo), erro.MemberNames);
    }

    [Fact]
    public void Prazo_no_passado_e_invalido_para_tarefa_aberta()
    {
        var erros = Validar(new TarefaInputModel { Titulo = "Atrasada", Prazo = Relogio.Hoje.AddDays(-1) });
        Assert.Single(erros);
    }

    [Fact]
    public void Prazo_no_passado_e_aceito_para_tarefa_concluida()
    {
        var modelo = new TarefaInputModel
        {
            Titulo = "Já feita", Prazo = Relogio.Hoje.AddDays(-1), Situacao = SituacaoTarefa.Concluida
        };
        Assert.Empty(Validar(modelo));
    }

    private sealed class ProvedorSimples(TimeProvider relogio) : IServiceProvider
    {
        public object? GetService(Type tipo) => tipo == typeof(TimeProvider) ? relogio : null;
    }
}
