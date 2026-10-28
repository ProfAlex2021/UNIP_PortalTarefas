import { PortalTarefasClient } from "./gerado/portalTarefasClient";
import { normalizarErro, type ErroApi } from "./erros";
import { fetchComSimulacao } from "./simulacao";

/** Resultado de toda chamada à API: dados tipados OU erro normalizado — nunca uma exceção. */
export type Resultado<T> = { ok: true; dados: T } | { ok: false; erro: ErroApi };

/**
 * URL base vista pelo navegador. Vazia = mesma origem (proxy do Vite em desenvolvimento,
 * ou a própria API servindo o front-end em produção).
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

/** Única instância do cliente gerado pelo NSwag, com o fetch da simulação. */
export const cliente = new PortalTarefasClient(baseUrl, {
  fetch: (url, init) => fetchComSimulacao(url, { ...init, credentials: "same-origin" }),
});

/**
 * Função central de acesso: executa a operação do cliente gerado e converte
 * qualquer falha em ErroApi. Componentes não fazem try/catch nem leem Response.
 */
export async function executar<T>(operacao: (c: PortalTarefasClient) => Promise<T>): Promise<Resultado<T>> {
  try {
    return { ok: true, dados: await operacao(cliente) };
  } catch (erro) {
    return { ok: false, erro: normalizarErro(erro) };
  }
}
