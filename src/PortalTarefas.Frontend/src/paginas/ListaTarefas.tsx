import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router";
import { CAMPOS_ORDENACAO, PRIORIDADES, SITUACOES, tarefasApi, type FiltrosListagem, type PrioridadeTarefa, type SituacaoTarefa } from "../api/tarefasApi";
import { CartaoTarefa } from "../componentes/CartaoTarefa";
import { EstadoRequisicaoView } from "../componentes/EstadoRequisicaoView";
import { Paginacao } from "../componentes/Paginacao";
import { Titulo } from "../componentes/Titulo";
import { useRequisicao } from "../hooks/useRequisicao";

/** Lê os filtros da URL. A URL é a fonte de verdade: dá para compartilhar, voltar e recarregar. */
export function filtrosDaUrl(params: URLSearchParams): FiltrosListagem {
  const numero = (nome: string) => (params.has(nome) ? Number(params.get(nome)) : undefined);
  const texto = (nome: string) => params.get(nome) || undefined;
  return {
    pagina: numero("pagina"),
    tamanho: numero("tamanho"),
    ordenarPor: texto("ordenarPor"),
    direcao: texto("direcao") as FiltrosListagem["direcao"],
    situacao: texto("situacao") as SituacaoTarefa | undefined,
    prioridade: texto("prioridade") as PrioridadeTarefa | undefined,
    busca: texto("busca"),
  };
}

/**
 * Lista de tarefas. Sem parâmetros na URL a página fica no estado inicial (idle) aguardando
 * a busca; com parâmetros (ex.: /tarefas?pagina=1) a consulta é feita automaticamente.
 */
export function ListaTarefas() {
  const [params, setParams] = useSearchParams();
  const filtros = filtrosDaUrl(params);
  const chave = params.size > 0 ? params.toString() : null;

  const { estado, recarregar } = useRequisicao(
    chave,
    (signal) => tarefasApi.listar(filtros, signal),
    (pagina) => pagina.itens.length === 0,
  );

  // Rascunho do formulário; só vai para a URL ao clicar em Buscar.
  const [rascunho, setRascunho] = useState(() => ({
    busca: filtros.busca ?? "",
    situacao: filtros.situacao ?? "",
    prioridade: filtros.prioridade ?? "",
    ordenarPor: filtros.ordenarPor ?? "id",
    direcao: filtros.direcao ?? "asc",
    tamanho: String(filtros.tamanho ?? 10),
  }));

  function aplicarFiltros(evento: FormEvent) {
    evento.preventDefault();
    const novos = new URLSearchParams();
    Object.entries(rascunho).forEach(([chave, valor]) => valor && novos.set(chave, valor));
    novos.set("pagina", "1");
    setParams(novos);
  }

  function irParaPagina(pagina: number) {
    const novos = new URLSearchParams(params);
    novos.set("pagina", String(pagina));
    setParams(novos);
  }

  const alterar = (campo: keyof typeof rascunho) => (e: { target: { value: string } }) =>
    setRascunho((r) => ({ ...r, [campo]: e.target.value }));

  return (
    <section>
      <Titulo>Tarefas</Titulo>

      <form className="filtros" onSubmit={aplicarFiltros} aria-label="Filtros da lista">
        <label>
          Buscar no título
          <input value={rascunho.busca} onChange={alterar("busca")} maxLength={100} />
        </label>
        <label>
          Situação
          <select value={rascunho.situacao} onChange={alterar("situacao")}>
            <option value="">Todas</option>
            {SITUACOES.map((s) => <option key={s.valor} value={s.valor}>{s.rotulo}</option>)}
          </select>
        </label>
        <label>
          Prioridade
          <select value={rascunho.prioridade} onChange={alterar("prioridade")}>
            <option value="">Todas</option>
            {PRIORIDADES.map((p) => <option key={p.valor} value={p.valor}>{p.rotulo}</option>)}
          </select>
        </label>
        <label>
          Ordenar por
          <select value={rascunho.ordenarPor} onChange={alterar("ordenarPor")}>
            {CAMPOS_ORDENACAO.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </label>
        <label>
          Direção
          <select value={rascunho.direcao} onChange={alterar("direcao")}>
            <option value="asc">crescente</option>
            <option value="desc">decrescente</option>
          </select>
        </label>
        <label>
          Por página
          <select value={rascunho.tamanho} onChange={alterar("tamanho")}>
            {[...new Set(["5", "10", "20", "50", rascunho.tamanho])]
              .sort((a, b) => Number(a) - Number(b))
              .map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
        </label>
        <button type="submit">Buscar</button>
      </form>

      <EstadoRequisicaoView
        estado={estado}
        textoCarregando="Carregando tarefas…"
        onTentarNovamente={recarregar}
        idle={<p>Defina os filtros e clique em <strong>Buscar</strong> para consultar as tarefas.</p>}
        vazio={<p>Nenhuma tarefa encontrada com esses filtros.</p>}
      >
        {(pagina) => (
          <>
            <ul className="lista">
              {pagina.itens.map((t) => <CartaoTarefa key={t.id} tarefa={t} />)}
            </ul>
            <Paginacao pagina={pagina.pagina} totalPaginas={pagina.totalPaginas} totalItens={pagina.totalItens} aoMudar={irParaPagina} />
          </>
        )}
      </EstadoRequisicaoView>

      <p><Link className="botao" to="/tarefas/nova">Nova tarefa</Link></p>
    </section>
  );
}
