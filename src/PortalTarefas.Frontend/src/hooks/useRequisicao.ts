import { useCallback, useEffect, useRef, useState } from "react";
import type { Resultado } from "../api/cliente";
import type { ErroApi } from "../api/erros";

/** Os cinco estados explícitos de uma requisição. */
export type EstadoRequisicao<T> =
  | { status: "idle" }
  | { status: "carregando" }
  | { status: "vazio" }
  | { status: "sucesso"; dados: T }
  | { status: "erro"; erro: ErroApi };

/**
 * Executa `buscar` sempre que `chave` muda. Com `chave` nula nada é feito (estado idle).
 * A requisição anterior é CANCELADA (AbortController) quando a chave muda ou o componente
 * é desmontado, e respostas obsoletas são ignoradas — a tela nunca mostra dados de uma
 * consulta que deixou de ser relevante nem fica presa em "carregando".
 */
export function useRequisicao<T>(
  chave: string | null,
  buscar: (signal: AbortSignal) => Promise<Resultado<T>>,
  ehVazio: (dados: T) => boolean = () => false,
) {
  const [estado, setEstado] = useState<EstadoRequisicao<T>>({ status: "idle" });
  const [tentativa, setTentativa] = useState(0);

  // Guardamos as funções em refs para não reexecutar o efeito a cada renderização.
  const buscarRef = useRef(buscar);
  const ehVazioRef = useRef(ehVazio);
  buscarRef.current = buscar;
  ehVazioRef.current = ehVazio;

  useEffect(() => {
    if (chave === null) {
      setEstado({ status: "idle" });
      return;
    }

    const controlador = new AbortController();
    setEstado({ status: "carregando" });

    buscarRef.current(controlador.signal).then((resultado) => {
      if (controlador.signal.aborted) return; // resposta obsoleta: ignorar
      if (!resultado.ok) {
        if (resultado.erro.tipo !== "cancelado") setEstado({ status: "erro", erro: resultado.erro });
        return;
      }
      setEstado(ehVazioRef.current(resultado.dados) ? { status: "vazio" } : { status: "sucesso", dados: resultado.dados });
    });

    return () => controlador.abort();
  }, [chave, tentativa]);

  const recarregar = useCallback(() => setTentativa((t) => t + 1), []);

  return { estado, recarregar };
}
