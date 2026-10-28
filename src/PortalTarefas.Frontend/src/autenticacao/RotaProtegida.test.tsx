import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { SESSAO_ANONIMA } from "../api/autenticacaoApi";
import { retornoSeguro, RotaProtegida } from "./RotaProtegida";

const sessaoAtual = vi.hoisted(() => ({ valor: { autenticado: false } as Record<string, unknown>, restaurando: false }));

vi.mock("./ContextoAutenticacao", () => ({
  useAutenticacao: () => ({ sessao: sessaoAtual.valor, restaurando: sessaoAtual.restaurando }),
}));

function PaginaLogin() {
  const local = useLocation();
  return <p data-testid="login">{local.search}</p>;
}

function renderizar(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/entrar" element={<PaginaLogin />} />
        <Route element={<RotaProtegida />}>
          <Route path="/tarefas" element={<p>conteúdo protegido</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe("RotaProtegida", () => {
  it("sem sessão redireciona para /entrar preservando a URL pretendida", () => {
    sessaoAtual.valor = SESSAO_ANONIMA;
    renderizar("/tarefas?pagina=2&situacao=Pendente");

    const retorno = new URLSearchParams(screen.getByTestId("login").textContent!).get("retorno");
    expect(retorno).toBe("/tarefas?pagina=2&situacao=Pendente");
  });

  it("com sessão mostra o conteúdo", () => {
    sessaoAtual.valor = { ...SESSAO_ANONIMA, autenticado: true };
    renderizar("/tarefas");
    expect(screen.getByText("conteúdo protegido")).toBeInTheDocument();
  });

  it("enquanto restaura a sessão não redireciona", () => {
    sessaoAtual.restaurando = true;
    sessaoAtual.valor = SESSAO_ANONIMA;
    renderizar("/tarefas");
    expect(screen.getByRole("status")).toHaveTextContent("Verificando sessão");
    sessaoAtual.restaurando = false;
  });

  it("retornoSeguro só aceita caminhos internos", () => {
    expect(retornoSeguro("/tarefas/3")).toBe("/tarefas/3");
    expect(retornoSeguro("https://site-malicioso.exemplo")).toBe("/tarefas?pagina=1");
    expect(retornoSeguro("//site-malicioso.exemplo")).toBe("/tarefas?pagina=1");
  });
});
