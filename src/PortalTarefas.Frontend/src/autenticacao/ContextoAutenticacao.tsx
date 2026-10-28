import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { autenticacaoApi, SESSAO_ANONIMA, type Sessao } from "../api/autenticacaoApi";
import { EVENTO_SESSAO_EXPIRADA } from "../api/cliente";
import type { ErroApi } from "../api/erros";

interface ValorAutenticacao {
  sessao: Sessao;
  /** true enquanto a sessão está sendo restaurada ao abrir a aplicação. */
  restaurando: boolean;
  entrar: (email: string, senha: string) => Promise<ErroApi | null>;
  sair: () => Promise<void>;
  /** Apenas melhoria de experiência (esconder botões). Quem decide de verdade é a API. */
  pode: (permissao: string) => boolean;
}

const Contexto = createContext<ValorAutenticacao | null>(null);

export function ProvedorAutenticacao({ children }: { children: ReactNode }) {
  const [sessao, setSessao] = useState<Sessao>(SESSAO_ANONIMA);
  const [restaurando, setRestaurando] = useState(true);

  // Restauração de sessão: ao recarregar a página, o cookie continua no navegador;
  // perguntamos à API quem é o usuário em vez de guardar dados de sessão no localStorage.
  useEffect(() => {
    const controlador = new AbortController();
    autenticacaoApi.sessao(controlador.signal).then((r) => {
      if (controlador.signal.aborted) return;
      setSessao(r.ok ? r.dados : SESSAO_ANONIMA);
      setRestaurando(false);
    });
    return () => controlador.abort();
  }, []);

  // Se qualquer chamada receber 401, a sessão acabou (expirou ou logout em outra aba).
  useEffect(() => {
    const aoExpirar = () => setSessao(SESSAO_ANONIMA);
    window.addEventListener(EVENTO_SESSAO_EXPIRADA, aoExpirar);
    return () => window.removeEventListener(EVENTO_SESSAO_EXPIRADA, aoExpirar);
  }, []);

  const entrar = useCallback(async (email: string, senha: string) => {
    const resultado = await autenticacaoApi.entrar(email, senha);
    if (!resultado.ok) return resultado.erro;
    setSessao(resultado.dados);
    return null;
  }, []);

  const sair = useCallback(async () => {
    await autenticacaoApi.sair();
    setSessao(SESSAO_ANONIMA);
  }, []);

  const valor = useMemo<ValorAutenticacao>(
    () => ({ sessao, restaurando, entrar, sair, pode: (p) => sessao.permissoes.includes(p) }),
    [sessao, restaurando, entrar, sair],
  );

  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useAutenticacao(): ValorAutenticacao {
  const valor = useContext(Contexto);
  if (!valor) throw new Error("useAutenticacao deve ser usado dentro de <ProvedorAutenticacao>.");
  return valor;
}
