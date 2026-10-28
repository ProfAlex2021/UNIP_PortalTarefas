import type { ErroApi } from "../api/erros";

const DICAS: Partial<Record<ErroApi["tipo"], string>> = {
  rede: "Falha de rede: a API não respondeu.",
  validacao: "Os dados enviados foram recusados pela API.",
  "nao-encontrado": "O recurso solicitado não existe.",
  conflito: "O recurso foi alterado por outra pessoa.",
  "nao-autorizado": "É preciso entrar no sistema.",
  proibido: "Você não tem permissão para esta operação.",
  http: "A API respondeu com erro.",
};

/** Exibe um ErroApi (rede, status HTTP ou Problem Details) de forma acessível. */
export function MensagemErro({ erro, onTentarNovamente }: { erro: ErroApi; onTentarNovamente?: () => void }) {
  return (
    <div className="estado estado-erro" role="alert">
      <strong>{erro.titulo}</strong>
      {erro.detalhe && <p>{erro.detalhe}</p>}
      <p className="dica">
        {DICAS[erro.tipo]}
        {erro.status ? ` (HTTP ${erro.status})` : ""}
      </p>
      {erro.errosPorCampo && (
        <ul>
          {Object.entries(erro.errosPorCampo).flatMap(([campo, mensagens]) =>
            mensagens.map((m) => <li key={campo + m}>{campo}: {m}</li>),
          )}
        </ul>
      )}
      {erro.correlationId && <p className="correlacao">Código para suporte: {erro.correlationId}</p>}
      {onTentarNovamente && (
        <button type="button" onClick={onTentarNovamente}>
          Tentar novamente
        </button>
      )}
    </div>
  );
}
