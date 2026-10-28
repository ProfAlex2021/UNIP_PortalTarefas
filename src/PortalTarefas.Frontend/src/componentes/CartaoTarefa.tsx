import { Link } from "react-router";
import { PRIORIDADES, SITUACOES, type TarefaResumo } from "../api/tarefasApi";
import { formatarData } from "../util/formatacao";

export function rotuloSituacao(valor: string) {
  return SITUACOES.find((s) => s.valor === valor)?.rotulo ?? valor;
}

export function rotuloPrioridade(valor: string) {
  return PRIORIDADES.find((p) => p.valor === valor)?.rotulo ?? valor;
}

/** Cartão de uma tarefa na lista; o título leva à página de detalhes por URL. */
export function CartaoTarefa({ tarefa }: { tarefa: TarefaResumo }) {
  return (
    <li className={`cartao${tarefa.vencida ? " vencida" : ""}`}>
      <h3>
        <Link to={`/tarefas/${tarefa.id}`}>{tarefa.titulo}</Link>
      </h3>
      <p className="etiquetas">
        <span className={`etiqueta situacao-${tarefa.situacao}`}>{rotuloSituacao(tarefa.situacao)}</span>
        <span className={`etiqueta prioridade-${tarefa.prioridade}`}>Prioridade {rotuloPrioridade(tarefa.prioridade)}</span>
        {tarefa.prazo && <span className="etiqueta">Prazo {formatarData(tarefa.prazo)}</span>}
        {tarefa.vencida && <span className="etiqueta alerta">Vencida</span>}
      </p>
    </li>
  );
}
