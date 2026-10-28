import { Link } from "react-router";
import { useAutenticacao } from "../autenticacao/ContextoAutenticacao";
import { Titulo } from "../componentes/Titulo";

/**
 * Tela inicial. A restauração da sessão (GET /api/auth/sessao, endpoint público) é a
 * chamada simples que confirma o acesso à API.
 */
export function Inicio() {
  const { sessao, restaurando } = useAutenticacao();

  return (
    <section>
      <Titulo>Portal de Tarefas</Titulo>
      <p>SPA React (Vite + TypeScript) consumindo a API v1 pelo cliente gerado com NSwag.</p>

      {restaurando ? (
        <p className="estado" role="status" aria-live="polite">Verificando a API e a sessão…</p>
      ) : sessao.autenticado ? (
        <p className="mensagem" role="status">
          Olá, {sessao.nome}! Equipe {sessao.equipe} · papéis: {sessao.papeis.join(", ")}.
        </p>
      ) : (
        <p className="mensagem" role="status">API acessível. <Link to="/entrar">Entre</Link> para ver as tarefas.</p>
      )}

      <ul>
        <li><Link to="/tarefas?pagina=1">Ver tarefas</Link></li>
        <li><Link to="/tarefas">Abrir a lista no estado inicial (sem consulta)</Link></li>
        <li><Link to="/tarefas/nova">Cadastrar tarefa</Link></li>
      </ul>
    </section>
  );
}
