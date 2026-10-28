import { executar, type Resultado } from "./cliente";
import type {
  PaginaTarefas,
  PrioridadeTarefa,
  SituacaoTarefa,
  TarefaCriacao,
  TarefaDetalhe,
  TarefaResumo,
} from "./gerado/portalTarefasClient";

// Tipos de página, resumo e detalhe vêm do contrato (gerados pelo NSwag), não são reescritos à mão.
export type { PaginaTarefas, PrioridadeTarefa, SituacaoTarefa, TarefaCriacao, TarefaDetalhe, TarefaResumo };

export const SITUACOES: { valor: SituacaoTarefa; rotulo: string }[] = [
  { valor: "Pendente", rotulo: "Pendente" },
  { valor: "EmAndamento", rotulo: "Em andamento" },
  { valor: "Concluida", rotulo: "Concluída" },
];

export const PRIORIDADES: { valor: PrioridadeTarefa; rotulo: string }[] = [
  { valor: "Baixa", rotulo: "Baixa" },
  { valor: "Media", rotulo: "Média" },
  { valor: "Alta", rotulo: "Alta" },
];

export const CAMPOS_ORDENACAO = ["id", "titulo", "prazo", "prioridade", "situacao", "criadaEm"] as const;

export interface FiltrosListagem {
  pagina?: number;
  tamanho?: number;
  ordenarPor?: string;
  direcao?: "asc" | "desc";
  situacao?: SituacaoTarefa;
  prioridade?: PrioridadeTarefa;
  busca?: string;
}

/** Funções de acesso usadas pelas páginas: nenhuma página chama fetch diretamente. */
export const tarefasApi = {
  listar: (f: FiltrosListagem, signal?: AbortSignal): Promise<Resultado<PaginaTarefas>> =>
    executar((c) => c.listarTarefas(f.pagina, f.tamanho, f.ordenarPor, f.direcao, f.situacao, f.prioridade, f.busca, signal)),

  obter: (id: number, signal?: AbortSignal): Promise<Resultado<TarefaDetalhe>> =>
    executar((c) => c.obterTarefa(id, signal)),

  criar: (dados: TarefaCriacao, signal?: AbortSignal): Promise<Resultado<TarefaDetalhe>> =>
    executar((c) => c.criarTarefa(dados, signal)),
};
