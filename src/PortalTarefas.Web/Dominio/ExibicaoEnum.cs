namespace PortalTarefas.Web.Dominio;

/// <summary>Textos amigáveis para exibir enums nas telas.</summary>
public static class ExibicaoEnum
{
    public static string Exibir(this SituacaoTarefa situacao) => situacao switch
    {
        SituacaoTarefa.Pendente => "Pendente",
        SituacaoTarefa.EmAndamento => "Em andamento",
        SituacaoTarefa.Concluida => "Concluída",
        _ => situacao.ToString()
    };

    public static string Exibir(this PrioridadeTarefa prioridade) => prioridade switch
    {
        PrioridadeTarefa.Baixa => "Baixa",
        PrioridadeTarefa.Media => "Média",
        PrioridadeTarefa.Alta => "Alta",
        _ => prioridade.ToString()
    };
}
