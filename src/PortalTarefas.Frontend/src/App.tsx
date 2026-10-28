import { NavLink, Outlet, Route, Routes } from "react-router";
import { SeletorSimulacao } from "./componentes/SeletorSimulacao";
import { DetalheTarefa } from "./paginas/DetalheTarefa";
import { Inicio } from "./paginas/Inicio";
import { ListaTarefas } from "./paginas/ListaTarefas";
import { NaoEncontrada } from "./paginas/NaoEncontrada";
import { NovaTarefa } from "./paginas/NovaTarefa";

function Layout() {
  return (
    <>
      <a className="pular" href="#conteudo">Pular para o conteúdo</a>
      <header className="topo">
        <NavLink className="marca" to="/">Portal de Tarefas</NavLink>
        <nav aria-label="Navegação principal">
          <NavLink to="/" end>Início</NavLink>
          <NavLink to="/tarefas?pagina=1">Tarefas</NavLink>
          <NavLink to="/tarefas/nova">Nova tarefa</NavLink>
        </nav>
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
        <Route path="tarefas" element={<ListaTarefas />} />
        <Route path="tarefas/nova" element={<NovaTarefa />} />
        <Route path="tarefas/:id" element={<DetalheTarefa />} />
        <Route path="*" element={<NaoEncontrada />} />
      </Route>
    </Routes>
  );
}
