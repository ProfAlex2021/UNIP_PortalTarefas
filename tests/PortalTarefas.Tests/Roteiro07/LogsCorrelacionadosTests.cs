using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortalTarefas.Tests.Apoio;

namespace PortalTarefas.Tests.Roteiro07;

/// <summary>Fábrica que também guarda os logs em memória.</summary>
public sealed class FabricaComLogs : FabricaPortal
{
    public LogsEmMemoria Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => s.AddLogging(l => l.AddProvider(Logs)));
    }
}

public class LogsCorrelacionadosTests(FabricaComLogs fabrica) : IClassFixture<FabricaComLogs>
{
    [Fact]
    public async Task Erro_e_localizado_nos_logs_pelo_identificador_de_correlacao()
    {
        var cliente = fabrica.CriarClienteHttps();
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/diagnostico/erro");
        requisicao.Headers.Add("X-Correlation-Id", "aula-roteiro-07-erro");

        var resposta = await cliente.SendAsync(requisicao);
        var correlacao = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!["correlationId"]!.GetValue<string>();

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Equal("aula-roteiro-07-erro", correlacao);

        // Os logs da mesma requisição carregam o mesmo identificador (no escopo ou na mensagem).
        var daRequisicao = fabrica.Logs.Registros.Where(r => r.TextoCompleto.Contains(correlacao)).ToList();
        Assert.Contains(daRequisicao, r => r.Nivel == LogLevel.Error);
        Assert.Contains(daRequisicao, r => r.Mensagem.Contains("<-- Saída"));
    }

    [Fact]
    public async Task Login_nao_registra_senha_cookie_nem_email_digitado()
    {
        var cliente = fabrica.CriarClienteHttps();
        var senha = fabrica.SenhaDe(Perfil.UsuarioAlfa);

        var ok = await cliente.PostAsJsonAsync("/api/auth/login", new { email = FabricaPortal.Emails[Perfil.UsuarioAlfa], senha });
        var cookie = ok.Headers.GetValues("Set-Cookie").First().Split(';')[0].Split('=', 2)[1];
        await cliente.PostAsJsonAsync("/api/auth/login", new { email = "tentativa@exemplo.com", senha = "SenhaErrada123" });

        var texto = string.Join("\n", fabrica.Logs.Registros.Select(r => r.TextoCompleto));
        Assert.DoesNotContain(senha, texto);
        Assert.DoesNotContain("SenhaErrada123", texto);
        Assert.DoesNotContain(cookie, texto);
        Assert.DoesNotContain("tentativa@exemplo.com", texto);
        Assert.Contains(fabrica.Logs.Registros, r => r.Categoria == "Auditoria" && r.Mensagem.StartsWith("Login do usuário"));
    }
}
