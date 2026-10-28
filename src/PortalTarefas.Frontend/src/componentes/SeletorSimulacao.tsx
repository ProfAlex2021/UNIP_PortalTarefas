import { useState } from "react";
import { definirModoSimulacao, obterModoSimulacao, type ModoSimulacao } from "../api/simulacao";

/** Painel de desenvolvimento para simular atraso e falhas da API (não aparece no build de produção). */
export function SeletorSimulacao() {
  const [modo, setModo] = useState<ModoSimulacao>(obterModoSimulacao());
  if (!import.meta.env.DEV) return null;

  return (
    <label className="simulacao">
      Simular (dev):{" "}
      <select
        value={modo}
        onChange={(e) => {
          const novo = e.target.value as ModoSimulacao;
          definirModoSimulacao(novo);
          setModo(novo);
        }}
      >
        <option value="nenhuma">nenhuma</option>
        <option value="atraso">atraso de 2,5 s</option>
        <option value="falha-rede">falha de rede</option>
        <option value="erro-500">erro 500</option>
      </select>
    </label>
  );
}
