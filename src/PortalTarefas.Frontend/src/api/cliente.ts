import { PortalTarefasClient } from "./gerado/portalTarefasClient";
import { normalizarErro, type ErroApi } from "./erros";
import { fetchComSimulacao } from "./simulacao";

/** Resultado de toda chamada à API: dados tipados OU erro normalizado — nunca uma exceção. */
export type Resultado<T> = { ok: true; dados: T } | { ok: false; erro: ErroApi };

/** Evento disparado quando a API responde 401: a sessão expirou ou foi encerrada. */
export const EVENTO_SESSAO_EXPIRADA = "portaltarefas:sessao-expirada";

/**
 * URL base vista pelo navegador. Vazia = mesma origem (proxy do Vite em desenvolvimento,
 * ou a própria API servindo o front-end em produção).
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

/**
 * Única instância do cliente gerado pelo NSwag.
 * - credentials "same-origin": o navegador envia o cookie HttpOnly da sessão; o JavaScript nunca o lê.
 * - X-Requested-With: cabeçalho exigido pela proteção CSRF da API (Roteiro 6).
 */
export const cliente = new PortalTarefasClient(baseUrl, {
  fetch: (url, init) => {
    const cabecalhos = new Headers(init?.headers);
    cabecalhos.set("X-Requested-With", "PortalTarefas");
    return fetchComSimulacao(url, { ...init, headers: cabecalhos, credentials: "same-origin" });
  },
});

/**
 * Função central de acesso: executa a operação do cliente gerado e converte
 * qualquer falha em ErroApi. Componentes não fazem try/catch nem leem Response.
 */
export async function executar<T>(operacao: (c: PortalTarefasClient) => Promise<T>): Promise<Resultado<T>> {
  try {
    return { ok: true, dados: await operacao(cliente) };
  } catch (erro) {
    const normalizado = normalizarErro(erro);
    if (normalizado.tipo === "nao-autorizado") window.dispatchEvent(new Event(EVENTO_SESSAO_EXPIRADA));
    return { ok: false, erro: normalizado };
  }
}
