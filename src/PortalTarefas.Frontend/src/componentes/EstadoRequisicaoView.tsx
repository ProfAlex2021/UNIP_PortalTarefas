import type { ReactNode } from "react";
import type { EstadoRequisicao } from "../hooks/useRequisicao";
import { MensagemErro } from "./MensagemErro";

interface Props<T> {
  estado: EstadoRequisicao<T>;
  idle: ReactNode;
  vazio: ReactNode;
  textoCarregando?: string;
  onTentarNovamente?: () => void;
  children: (dados: T) => ReactNode;
}

/**
 * Renderiza explicitamente cada um dos cinco estados. As mudanças de estado são
 * anunciadas por leitores de tela (role="status" / role="alert" com aria-live).
 */
export function EstadoRequisicaoView<T>({ estado, idle, vazio, textoCarregando = "Carregando…", onTentarNovamente, children }: Props<T>) {
  switch (estado.status) {
    case "idle":
      return <div className="estado estado-idle" data-estado="idle">{idle}</div>;
    case "carregando":
      return (
        <div className="estado estado-carregando" data-estado="carregando" role="status" aria-live="polite" aria-busy="true">
          <span className="indicador" aria-hidden="true" /> {textoCarregando}
        </div>
      );
    case "vazio":
      return <div className="estado estado-vazio" data-estado="vazio" role="status" aria-live="polite">{vazio}</div>;
    case "erro":
      return (
        <div data-estado="erro">
          <MensagemErro erro={estado.erro} onTentarNovamente={onTentarNovamente} />
        </div>
      );
    case "sucesso":
      return <div data-estado="sucesso">{children(estado.dados)}</div>;
  }
}
