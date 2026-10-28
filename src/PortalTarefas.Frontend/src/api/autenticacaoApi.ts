import { executar, type Resultado } from "./cliente";
import type { Sessao } from "./gerado/portalTarefasClient";

export type { Sessao };

/** Permissões calculadas pelo servidor (nomes das políticas). */
export const Permissoes = {
  ler: "tarefas.ler",
  criar: "tarefas.criar",
  editarOutrasEquipes: "tarefas.editar-outras-equipes",
  excluir: "tarefas.excluir",
} as const;

export const SESSAO_ANONIMA: Sessao = { autenticado: false, nome: undefined, email: undefined, equipe: undefined, papeis: [], permissoes: [] };

/**
 * Login, logout e sessão. A senha só trafega no corpo do POST /api/auth/login (HTTPS);
 * a resposta não contém token: a sessão fica no cookie HttpOnly, invisível ao JavaScript.
 */
export const autenticacaoApi = {
  entrar: (email: string, senha: string): Promise<Resultado<Sessao>> => executar((c) => c.entrar({ email, senha })),
  sair: (): Promise<Resultado<void>> => executar((c) => c.sair()),
  sessao: (signal?: AbortSignal): Promise<Resultado<Sessao>> => executar((c) => c.obterSessao(signal)),
};
