using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Servicos.Painel;

namespace PortalTarefas.Web.Controllers;

public sealed record ResultadoConsultaPainel(string Fonte, IReadOnlyList<LinhaPainel> Linhas, double DuracaoMs);

/// <summary>
/// Executa a mesma consulta de painel com EF Core e com Dapper e mostra os dois resultados
/// lado a lado, com o tempo medido nesta máquina (medição local, não conclusão geral).
/// </summary>
public sealed class PainelController(IEnumerable<IConsultaPainel> consultas, TimeProvider relogio) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancelamento)
    {
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);
        var resultados = new List<ResultadoConsultaPainel>();

        foreach (var consulta in consultas)
        {
            await consulta.ObterAsync(hoje, cancelamento); // aquecimento: conexão, compilação da consulta
            var cronometro = Stopwatch.StartNew();
            var linhas = await consulta.ObterAsync(hoje, cancelamento);
            resultados.Add(new(consulta.Fonte, linhas, cronometro.Elapsed.TotalMilliseconds));
        }

        return View(resultados);
    }
}
