import { describe, expect, it } from "vitest";
import { normalizarErro } from "./erros";
import { ApiException } from "./gerado/portalTarefasClient";

// Extensão do Roteiro 5: testes da camada HTTP (normalização de erros).
describe("normalizarErro", () => {
  it("classifica falha de rede (fetch rejeitado com TypeError)", () => {
    const erro = normalizarErro(new TypeError("Failed to fetch"));
    expect(erro.tipo).toBe("rede");
    expect(erro.status).toBeUndefined();
  });

  it("converte Problem Details de validação (400) com erros por campo", () => {
    const erro = normalizarErro({
      status: 400,
      title: "Um ou mais campos são inválidos.",
      errors: { titulo: ["O título é obrigatório."] },
      correlationId: "abc123",
    });
    expect(erro.tipo).toBe("validacao");
    expect(erro.errosPorCampo?.titulo).toEqual(["O título é obrigatório."]);
    expect(erro.correlationId).toBe("abc123");
  });

  it("converte Problem Details 404 e 409", () => {
    expect(normalizarErro({ status: 404, title: "Tarefa não encontrada." }).tipo).toBe("nao-encontrado");
    expect(normalizarErro({ status: 409, title: "Conflito." }).tipo).toBe("conflito");
  });

  it("trata status sem corpo Problem Details (ApiException do NSwag)", () => {
    const erro = normalizarErro(new ApiException("Bad Gateway", 502, "<html>proxy</html>", {}, null));
    expect(erro.tipo).toBe("http");
    expect(erro.status).toBe(502);
  });

  it("lê Problem Details que veio dentro de uma ApiException", () => {
    const corpo = JSON.stringify({ status: 403, title: "Proibido." });
    expect(normalizarErro(new ApiException("Forbidden", 403, corpo, {}, null)).tipo).toBe("proibido");
  });

  it("identifica cancelamento por AbortController", () => {
    expect(normalizarErro(new DOMException("aborted", "AbortError")).tipo).toBe("cancelado");
  });
});
