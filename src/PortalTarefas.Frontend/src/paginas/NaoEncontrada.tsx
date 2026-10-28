import { Link } from "react-router";
import { Titulo } from "../componentes/Titulo";

export function NaoEncontrada({ mensagem = "A página procurada não existe." }: { mensagem?: string }) {
  return (
    <section data-estado="nao-encontrado">
      <Titulo>Não encontrado</Titulo>
      <p>{mensagem}</p>
      <p><Link to="/tarefas?pagina=1">Ir para a lista de tarefas</Link></p>
    </section>
  );
}
