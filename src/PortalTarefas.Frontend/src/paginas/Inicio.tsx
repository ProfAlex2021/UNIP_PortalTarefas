import { Link } from "react-router";
import { tarefasApi } from "../api/tarefasApi";
import { EstadoRequisicaoView } from "../componentes/EstadoRequisicaoView";
import { Titulo } from "../componentes/Titulo";
import { useRequisicao } from "../hooks/useRequisicao";

/** Tela inicial: faz uma chamada simples para confirmar que a API está acessível. */
export function Inicio() {
  const { estado, recarregar } = useRequisicao("inicio", (signal) => tarefasApi.listar({ tamanho: 1 }, signal));

  return (
    <section>
      <Titulo>Portal de Tarefas</Titulo>
      <p>SPA React (Vite + TypeScript) consumindo a API v1 pelo cliente gerado com NSwag.</p>

      <EstadoRequisicaoView estado={estado} idle={null} vazio={null} textoCarregando="Verificando a API…" onTentarNovamente={recarregar}>
        {(pagina) => <p className="mensagem" role="status">API conectada: {pagina.totalItens} tarefa(s) cadastrada(s).</p>}
      </EstadoRequisicaoView>

      <ul>
        <li><Link to="/tarefas?pagina=1">Ver tarefas</Link></li>
        <li><Link to="/tarefas">Abrir a lista no estado inicial (sem consulta)</Link></li>
        <li><Link to="/tarefas/nova">Cadastrar tarefa</Link></li>
      </ul>
    </section>
  );
}
