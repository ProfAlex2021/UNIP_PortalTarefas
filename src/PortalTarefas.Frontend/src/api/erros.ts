import { ApiException } from "./gerado/portalTarefasClient";

/** Categorias de erro tratadas pela interface. */
export type TipoErro = "rede" | "validacao" | "nao-encontrado" | "conflito" | "nao-autorizado" | "proibido" | "http" | "cancelado";

/** Erro normalizado: os componentes nunca lidam com Response, exceções do fetch ou JSON cru. */
export interface ErroApi {
  tipo: TipoErro;
  titulo: string;
  status?: number;
  detalhe?: string;
  /** Erros de validação por campo (chaves em camelCase, iguais aos campos do DTO). */
  errosPorCampo?: Record<string, string[]>;
  /** Identificador para localizar a requisição nos logs do servidor. */
  correlationId?: string;
}

interface ProblemDetailsLike {
  status?: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
  correlationId?: string;
}

function ehProblemDetails(valor: unknown): valor is ProblemDetailsLike {
  return typeof valor === "object" && valor !== null && ("title" in valor || "status" in valor) && !(valor instanceof Error);
}

function ehCancelamento(erro: unknown): boolean {
  return erro instanceof DOMException && erro.name === "AbortError";
}

function tipoPorStatus(status: number | undefined, temErros: boolean): TipoErro {
  if (status === 400 && temErros) return "validacao";
  if (status === 401) return "nao-autorizado";
  if (status === 403) return "proibido";
  if (status === 404) return "nao-encontrado";
  if (status === 409) return "conflito";
  return "http";
}

function deProblemDetails(problema: ProblemDetailsLike, statusPadrao?: number): ErroApi {
  const status = problema.status ?? statusPadrao;
  return {
    tipo: tipoPorStatus(status, !!problema.errors),
    status,
    titulo: problema.title ?? `Erro ${status ?? ""}`.trim(),
    detalhe: problema.detail,
    errosPorCampo: problema.errors,
    correlationId: problema.correlationId,
  };
}

/**
 * Converte qualquer falha de uma chamada em um ErroApi, separando:
 *  - falha de rede (fetch rejeitado: servidor fora do ar, DNS, CORS bloqueado);
 *  - resposta Problem Details (400, 404, 409, 500...) lançada pelo cliente gerado;
 *  - status não bem-sucedido sem corpo Problem Details (ApiException do NSwag);
 *  - cancelamento (AbortController), que a interface simplesmente ignora.
 */
export function normalizarErro(erro: unknown): ErroApi {
  if (ehCancelamento(erro)) {
    return { tipo: "cancelado", titulo: "Requisição cancelada." };
  }

  if (erro instanceof ApiException) {
    const excecao = erro as ApiException;
    try {
      const corpo = JSON.parse(excecao.response) as unknown;
      if (ehProblemDetails(corpo)) return deProblemDetails(corpo, excecao.status);
    } catch {
      // corpo vazio ou não-JSON: segue como erro HTTP genérico
    }
    return {
      tipo: tipoPorStatus(excecao.status, false),
      status: excecao.status,
      titulo: `O servidor respondeu com o status ${excecao.status}.`,
    };
  }

  if (ehProblemDetails(erro)) {
    return deProblemDetails(erro);
  }

  if (erro instanceof TypeError) {
    return {
      tipo: "rede",
      titulo: "Não foi possível conectar à API.",
      detalhe: "Verifique se a API está em execução e se a sua conexão está ativa.",
    };
  }

  return { tipo: "http", titulo: "Erro inesperado ao comunicar com a API." };
}
