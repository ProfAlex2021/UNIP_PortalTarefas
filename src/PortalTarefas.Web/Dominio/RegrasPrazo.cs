namespace PortalTarefas.Web.Dominio;

/// <summary>
/// Regra de negócio de prazo, compartilhada pela interface MVC (atributo de validação)
/// e pela API (DTOs), para que as duas portas de entrada apliquem exatamente a mesma regra.
/// </summary>
public static class RegrasPrazo
{
    public const string MensagemAltaSemPrazo = "Tarefas de prioridade Alta precisam de um prazo.";
    public const string MensagemPrazoNoPassado = "O prazo não pode ser anterior a hoje para tarefas não concluídas.";

    /// <summary>Devolve a mensagem de erro ou null quando a combinação é válida.</summary>
    public static string? Validar(PrioridadeTarefa? prioridade, DateOnly? prazo, SituacaoTarefa situacao, DateOnly hoje)
    {
        if (prioridade == PrioridadeTarefa.Alta && prazo is null)
            return MensagemAltaSemPrazo;

        if (prazo < hoje && situacao != SituacaoTarefa.Concluida)
            return MensagemPrazoNoPassado;

        return null;
    }
}
