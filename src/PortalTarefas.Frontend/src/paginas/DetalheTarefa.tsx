import { useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { Permissoes } from "../api/autenticacaoApi";
import type { ErroApi } from "../api/erros";
import { useAutenticacao } from "../autenticacao/ContextoAutenticacao";
import { MensagemErro } from "../componentes/MensagemErro";
import { tarefasApi } from "../api/tarefasApi";
import { rotuloPrioridade, rotuloSituacao } from "../componentes/CartaoTarefa";
import { EstadoRequisicaoView } from "../componentes/EstadoRequisicaoView";
import { Titulo } from "../componentes/Titulo";
import { useRequisicao } from "../hooks/useRequisicao";
import { formatarData, formatarDataHora } from "../util/formatacao";
import { NaoEncontrada } from "./NaoEncontrada";

/** Detalhes de uma tarefa a partir do identificador na URL (/tarefas/:id). */
export function DetalheTarefa() {
  const { id } = useParams();
  const idNumerico = Number(id);
  const valido = Number.isInteger(idNumerico) && idNumerico > 0;
  const mensagem = (useLocation().state as { mensagem?: string } | null)?.mensagem;
  const { pode } = useAutenticacao();
  const navegar = useNavigate();
  const [erroExclusao, setErroExclusao] = useState<ErroApi | null>(null);

  async function excluir() {
    if (!window.confirm("Excluir esta tarefa?")) return;
    const resultado = await tarefasApi.excluir(idNumerico);
    if (resultado.ok) navegar("/tarefas?pagina=1", { replace: true });
    else setErroExclusao(resultado.erro); // ex.: 403 se a permissão mudou no servidor
  }

  const { estado, recarregar } = useRequisicao(valido ? `tarefa-${idNumerico}` : null, (signal) => tarefasApi.obter(idNumerico, signal));

  if (!valido || (estado.status === "erro" && estado.erro.tipo === "nao-encontrado")) {
    return <NaoEncontrada mensagem={`Não existe tarefa com o identificador "${id}".`} />;
  }

  return (
    <section>
      <Titulo>Detalhes da tarefa</Titulo>
      {mensagem && <p className="mensagem" role="status">{mensagem}</p>}

      <EstadoRequisicaoView estado={estado} idle={null} vazio={null} textoCarregando="Carregando tarefa…" onTentarNovamente={recarregar}>
        {(tarefa) => (
          <article className="cartao">
            <h2>{tarefa.titulo}</h2>
            <dl className="detalhes">
              <dt>Situação</dt><dd>{rotuloSituacao(tarefa.situacao)}</dd>
              <dt>Prioridade</dt><dd>{rotuloPrioridade(tarefa.prioridade)}</dd>
              <dt>Prazo</dt><dd>{tarefa.prazo ? formatarData(tarefa.prazo) : "(sem prazo)"}</dd>
              <dt>Descrição</dt><dd>{tarefa.descricao ?? "(sem descrição)"}</dd>
              <dt>Equipe</dt><dd>{tarefa.equipe}</dd>
              <dt>Criada em</dt><dd>{formatarDataHora(tarefa.criadaEm)}</dd>
            </dl>
          </article>
        )}
      </EstadoRequisicaoView>

      {erroExclusao && <MensagemErro erro={erroExclusao} />}
      <p>
        {/* Só esconde o botão; a API recusa a exclusão (403) para quem não é Administrador. */}
        {pode(Permissoes.excluir) && estado.status === "sucesso" && (
          <button type="button" className="perigo" onClick={excluir}>Excluir</button>
        )}{" "}
        <Link to="/tarefas?pagina=1">Voltar à lista</Link>
      </p>
    </section>
  );
}
