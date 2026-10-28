import { useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import type { ErroApi } from "../api/erros";
import { PRIORIDADES, tarefasApi, type PrioridadeTarefa } from "../api/tarefasApi";
import { MensagemErro } from "../componentes/MensagemErro";
import { Titulo } from "../componentes/Titulo";

type Campo = "titulo" | "descricao" | "prioridade" | "prazo";

/**
 * Formulário de cadastro. A validação de verdade é a da API: os erros por campo do
 * Problem Details (400) aparecem ao lado de cada campo. Em caso de sucesso, a navegação
 * para os detalhes acontece dentro da SPA, sem recarregar o documento.
 */
export function NovaTarefa() {
  const navegar = useNavigate();
  const [dados, setDados] = useState({ titulo: "", descricao: "", prioridade: "Media" as PrioridadeTarefa, prazo: "" });
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<ErroApi | null>(null);
  const refs = { titulo: useRef<HTMLInputElement>(null), descricao: useRef<HTMLTextAreaElement>(null), prioridade: useRef<HTMLSelectElement>(null), prazo: useRef<HTMLInputElement>(null) };

  const errosCampo = (campo: Campo) => erro?.errosPorCampo?.[campo] ?? [];

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setEnviando(true);
    setErro(null);

    const resultado = await tarefasApi.criar({
      titulo: dados.titulo,
      descricao: dados.descricao || undefined,
      prioridade: dados.prioridade,
      prazo: dados.prazo || undefined,
    });
    setEnviando(false);

    if (resultado.ok) {
      navegar(`/tarefas/${resultado.dados.id}`, { state: { mensagem: `Tarefa "${resultado.dados.titulo}" cadastrada.` } });
      return;
    }

    setErro(resultado.erro);
    // Acessibilidade: leva o foco ao primeiro campo com erro.
    const primeiro = Object.keys(resultado.erro.errosPorCampo ?? {})[0] as Campo | undefined;
    if (primeiro && refs[primeiro]) refs[primeiro].current?.focus();
  }

  const propsCampo = (campo: Campo) => ({
    id: campo,
    "aria-invalid": errosCampo(campo).length > 0 || undefined,
    "aria-describedby": errosCampo(campo).length > 0 ? `${campo}-erro` : undefined,
  });

  const ErrosDoCampo = ({ campo }: { campo: Campo }) =>
    errosCampo(campo).length > 0 ? <span id={`${campo}-erro`} className="erro">{errosCampo(campo).join(" ")}</span> : null;

  return (
    <section>
      <Titulo>Nova tarefa</Titulo>

      {erro && erro.tipo !== "validacao" && <MensagemErro erro={erro} />}
      {erro?.tipo === "validacao" && <p className="erro" role="alert">{erro.titulo} Corrija os campos indicados.</p>}

      <form onSubmit={enviar} noValidate aria-busy={enviando}>
        <div className="campo">
          <label htmlFor="titulo">Título</label>
          <input ref={refs.titulo} {...propsCampo("titulo")} value={dados.titulo} onChange={(e) => setDados({ ...dados, titulo: e.target.value })} />
          <ErrosDoCampo campo="titulo" />
        </div>
        <div className="campo">
          <label htmlFor="descricao">Descrição</label>
          <textarea ref={refs.descricao} {...propsCampo("descricao")} rows={4} value={dados.descricao} onChange={(e) => setDados({ ...dados, descricao: e.target.value })} />
          <ErrosDoCampo campo="descricao" />
        </div>
        <div className="campo">
          <label htmlFor="prioridade">Prioridade</label>
          <select ref={refs.prioridade} {...propsCampo("prioridade")} value={dados.prioridade} onChange={(e) => setDados({ ...dados, prioridade: e.target.value as PrioridadeTarefa })}>
            {PRIORIDADES.map((p) => <option key={p.valor} value={p.valor}>{p.rotulo}</option>)}
          </select>
          <ErrosDoCampo campo="prioridade" />
        </div>
        <div className="campo">
          <label htmlFor="prazo">Prazo</label>
          <input ref={refs.prazo} {...propsCampo("prazo")} type="date" value={dados.prazo} onChange={(e) => setDados({ ...dados, prazo: e.target.value })} />
          <ErrosDoCampo campo="prazo" />
        </div>
        <button type="submit" disabled={enviando}>{enviando ? "Salvando…" : "Salvar"}</button>
      </form>
    </section>
  );
}
