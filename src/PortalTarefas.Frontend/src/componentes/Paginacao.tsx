interface Props {
  pagina: number;
  totalPaginas: number;
  totalItens: number;
  aoMudar: (pagina: number) => void;
}

export function Paginacao({ pagina, totalPaginas, totalItens, aoMudar }: Props) {
  return (
    <nav className="paginacao" aria-label="Paginação">
      <button type="button" onClick={() => aoMudar(pagina - 1)} disabled={pagina <= 1}>
        ← Anterior
      </button>
      <span>
        Página {pagina} de {Math.max(totalPaginas, 1)} · {totalItens} tarefa(s)
      </span>
      <button type="button" onClick={() => aoMudar(pagina + 1)} disabled={pagina >= totalPaginas}>
        Próxima →
      </button>
    </nav>
  );
}
