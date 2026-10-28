import { Navigate, Outlet, useLocation } from "react-router";
import { useAutenticacao } from "./ContextoAutenticacao";

/**
 * Protege rotas da SPA: sem sessão, redireciona para /entrar guardando a URL pretendida
 * (inclusive filtros da query string) para voltar a ela depois do login.
 * É só experiência do usuário — a API continua exigindo autenticação em cada chamada.
 */
export function RotaProtegida() {
  const { sessao, restaurando } = useAutenticacao();
  const local = useLocation();

  if (restaurando) {
    return <p className="estado" role="status" aria-live="polite">Verificando sessão…</p>;
  }

  if (!sessao.autenticado) {
    const retorno = encodeURIComponent(local.pathname + local.search);
    return <Navigate to={`/entrar?retorno=${retorno}`} replace />;
  }

  return <Outlet />;
}

/** Aceita apenas caminhos internos ("/..."), evitando redirecionamento para outro site. */
export function retornoSeguro(valor: string | null): string {
  return valor && valor.startsWith("/") && !valor.startsWith("//") ? valor : "/tarefas?pagina=1";
}
