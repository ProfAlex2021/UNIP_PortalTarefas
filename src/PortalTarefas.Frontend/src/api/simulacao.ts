/**
 * Simulação de condições adversas para testar o feedback ao usuário (Roteiro 5, Etapa 4).
 * Disponível apenas em desenvolvimento, escolhida no rodapé da aplicação.
 */
export type ModoSimulacao = "nenhuma" | "atraso" | "falha-rede" | "erro-500";

const CHAVE = "portaltarefas.simulacao";
export const ATRASO_MS = 2500;

export function obterModoSimulacao(): ModoSimulacao {
  if (!import.meta.env.DEV) return "nenhuma";
  try {
    return (localStorage.getItem(CHAVE) as ModoSimulacao | null) ?? "nenhuma";
  } catch {
    return "nenhuma";
  }
}

export function definirModoSimulacao(modo: ModoSimulacao): void {
  try {
    localStorage.setItem(CHAVE, modo);
  } catch {
    // armazenamento indisponível: a simulação simplesmente não é aplicada
  }
}

function esperar(ms: number, signal?: AbortSignal | null): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener("abort", () => {
      clearTimeout(timer);
      reject(new DOMException("Requisição cancelada", "AbortError"));
    });
  });
}

/** fetch que aplica a simulação escolhida antes de chamar o fetch real. */
export async function fetchComSimulacao(entrada: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  switch (obterModoSimulacao()) {
    case "atraso":
      await esperar(ATRASO_MS, init?.signal);
      return fetch(entrada, init);
    case "falha-rede":
      await esperar(400, init?.signal);
      throw new TypeError("Failed to fetch (falha de rede simulada)");
    case "erro-500":
      await esperar(400, init?.signal);
      return new Response(
        JSON.stringify({ status: 500, title: "Erro interno ao processar a requisição.", detail: "Erro simulado no front-end.", correlationId: "simulado" }),
        { status: 500, headers: { "Content-Type": "application/problem+json" } },
      );
    default:
      return fetch(entrada, init);
  }
}
