import { useEffect, useRef, type ReactNode } from "react";

/**
 * Título principal da página. Ao trocar de rota, o foco vai para ele, para que
 * leitores de tela anunciem a nova página (a SPA não recarrega o documento).
 */
export function Titulo({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    ref.current?.focus();
    document.title = `${typeof children === "string" ? children : "Portal de Tarefas"} — Portal de Tarefas`;
  }, [children]);
  return (
    <h1 ref={ref} tabIndex={-1}>
      {children}
    </h1>
  );
}
