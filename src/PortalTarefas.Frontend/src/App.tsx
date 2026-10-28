import { NavLink, Outlet, Route, Routes, useNavigate } from "react-router";
import { Permissoes } from "./api/autenticacaoApi";
import { useAutenticacao } from "./autenticacao/ContextoAutenticacao";
import { RotaProtegida } from "./autenticacao/RotaProtegida";
import { SeletorSimulacao } from "./componentes/SeletorSimulacao";
import { DetalheTarefa } from "./paginas/DetalheTarefa";
import { Entrar } from "./paginas/Entrar";
import { Inicio } from "./paginas/Inicio";
import { ListaTarefas } from "./paginas/ListaTarefas";
import { NaoEncontrada } from "./paginas/NaoEncontrada";
import { NovaTarefa } from "./paginas/NovaTarefa";

function AreaSessao() {
  const { sessao, sair } = useAutenticacao();
  const navegar = useNavigate();

  if (!sessao.autenticado) return <NavLink to="/entrar">Entrar</NavLink>;

  return (
    <>
      <span>
        {sessao.nome} · {sessao.equipe}
      </span>
      <button
        type="button"
        onClick={async () => {
          navegar("/"); // sai da rota protegida antes, para não passar pela tela de login
          await sair();
        }}
      >
        Sair
      </button>
    </>
  );
}

function Layout() {
  const { sessao, pode } = useAutenticacao();
  return (
    <>
      <a className="pular" href="#conteudo">Pular para o conteúdo</a>
      <header className="topo">
        <NavLink className="marca" to="/">Portal de Tarefas</NavLink>
        <nav aria-label="Navegação principal">
          <NavLink to="/" end>Início</NavLink>
          {sessao.autenticado && <NavLink to="/tarefas?pagina=1">Tarefas</NavLink>}
          {pode(Permissoes.criar) && <NavLink to="/tarefas/nova">Nova tarefa</NavLink>}
        </nav>
        <div className="sessao">
          <AreaSessao />
        </div>
      </header>
      <main id="conteudo" className="conteudo">
        <Outlet />
      </main>
      <footer className="rodape">
        Desenvolvimento Web com .NET — UNIP <SeletorSimulacao />
      </footer>
    </>
  );
}

/** Rotas da SPA. Todas funcionam por URL direta (inclusive filtros na query string). */
export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Inicio />} />
        <Route path="entrar" element={<Entrar />} />
        {/* Roteiro 6: rotas que exigem sessão. */}
        <Route element={<RotaProtegida />}>
          <Route path="tarefas" element={<ListaTarefas />} />
          <Route path="tarefas/nova" element={<NovaTarefa />} />
          <Route path="tarefas/:id" element={<DetalheTarefa />} />
        </Route>
        <Route path="*" element={<NaoEncontrada />} />
      </Route>
    </Routes>
  );
}
