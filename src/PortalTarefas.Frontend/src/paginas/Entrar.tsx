import { useState, type FormEvent } from "react";
import { Navigate, useNavigate, useSearchParams } from "react-router";
import type { ErroApi } from "../api/erros";
import { useAutenticacao } from "../autenticacao/ContextoAutenticacao";
import { retornoSeguro } from "../autenticacao/RotaProtegida";
import { MensagemErro } from "../componentes/MensagemErro";
import { Titulo } from "../componentes/Titulo";

/** Página de login. Depois de entrar, volta para a URL que o usuário tentou abrir. */
export function Entrar() {
  const { sessao, entrar } = useAutenticacao();
  const navegar = useNavigate();
  const [params] = useSearchParams();
  const retorno = retornoSeguro(params.get("retorno"));

  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<ErroApi | null>(null);

  if (sessao.autenticado) return <Navigate to={retorno} replace />;

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setEnviando(true);
    const falha = await entrar(email, senha);
    setEnviando(false);
    setSenha(""); // a senha não fica guardada no estado depois da tentativa
    if (falha) setErro(falha);
    else navegar(retorno, { replace: true });
  }

  return (
    <section>
      <Titulo>Entrar</Titulo>
      {erro && <MensagemErro erro={erro} />}
      <form onSubmit={enviar} aria-busy={enviando}>
        <div className="campo">
          <label htmlFor="email">E-mail</label>
          <input id="email" type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </div>
        <div className="campo">
          <label htmlFor="senha">Senha</label>
          <input id="senha" type="password" autoComplete="current-password" value={senha} onChange={(e) => setSenha(e.target.value)} required />
        </div>
        <button type="submit" disabled={enviando}>{enviando ? "Entrando…" : "Entrar"}</button>
      </form>
    </section>
  );
}
