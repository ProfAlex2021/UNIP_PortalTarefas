import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Resultado } from "../api/cliente";
import { tarefasApi, type PaginaTarefas } from "../api/tarefasApi";
import { ListaTarefas } from "./ListaTarefas";

// Extensão do Roteiro 5: testes de componente (a camada de API é substituída por um dublê).
vi.mock("../api/tarefasApi", async (original) => ({
  ...(await original<typeof import("../api/tarefasApi")>()),
  tarefasApi: { listar: vi.fn(), obter: vi.fn(), criar: vi.fn() },
}));
const listar = vi.mocked(tarefasApi.listar);

const pagina = (itens: PaginaTarefas["itens"]): Resultado<PaginaTarefas> => ({
  ok: true,
  dados: { itens, pagina: 1, tamanho: 10, totalItens: itens.length, totalPaginas: itens.length ? 1 : 0 },
});

function MostrarUrl() {
  const local = useLocation();
  return <div data-testid="url">{local.pathname + local.search}</div>;
}

function renderizar(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/tarefas" element={<><ListaTarefas /><MostrarUrl /></>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("ListaTarefas", () => {
  beforeEach(() => listar.mockReset());

  it("sem parâmetros na URL fica no estado inicial (idle) e não chama a API", () => {
    renderizar("/tarefas");
    expect(screen.getByText(/Defina os filtros/)).toBeInTheDocument();
    expect(listar).not.toHaveBeenCalled();
  });

  it("mostra carregando e depois os itens (sucesso)", async () => {
    listar.mockResolvedValue(pagina([{ id: 1, titulo: "Estudar React", situacao: "Pendente", prioridade: "Alta", prazo: undefined, vencida: false }]));
    renderizar("/tarefas?pagina=1");

    expect(screen.getByRole("status")).toHaveTextContent("Carregando tarefas");
    expect(await screen.findByRole("link", { name: "Estudar React" })).toHaveAttribute("href", "/tarefas/1");
  });

  it("mostra o estado vazio", async () => {
    listar.mockResolvedValue(pagina([]));
    renderizar("/tarefas?pagina=1&busca=inexistente");
    expect(await screen.findByText("Nenhuma tarefa encontrada com esses filtros.")).toBeInTheDocument();
  });

  it("mostra o erro e permite tentar novamente sem ficar preso em carregando", async () => {
    listar.mockResolvedValueOnce({ ok: false, erro: { tipo: "rede", titulo: "Não foi possível conectar à API." } });
    listar.mockResolvedValueOnce(pagina([]));
    renderizar("/tarefas?pagina=1");

    expect(await screen.findByRole("alert")).toHaveTextContent("Não foi possível conectar à API.");
    await userEvent.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(await screen.findByText("Nenhuma tarefa encontrada com esses filtros.")).toBeInTheDocument();
  });

  it("lê os filtros da URL e grava os novos filtros na URL ao buscar", async () => {
    listar.mockResolvedValue(pagina([]));
    renderizar("/tarefas?pagina=2&situacao=Pendente&tamanho=5");

    expect(listar).toHaveBeenCalledWith(expect.objectContaining({ pagina: 2, situacao: "Pendente", tamanho: 5 }), expect.any(AbortSignal));

    await userEvent.selectOptions(screen.getByLabelText("Prioridade"), "Alta");
    await userEvent.click(screen.getByRole("button", { name: "Buscar" }));

    const url = screen.getByTestId("url").textContent!;
    expect(url).toContain("prioridade=Alta");
    expect(url).toContain("situacao=Pendente");
    expect(url).toContain("pagina=1");
  });
});
