// Roteiro 4, Etapa 4: chamada real à API usando SOMENTE o cliente gerado pelo NSwag.
// Nenhum tipo, URL ou DTO é escrito à mão aqui: tudo vem de src/api/gerado.
//
// Uso (com a API rodando no perfil https):
//   npm run chamada-real
// Para outra URL:  API_URL=https://localhost:7260 npm run chamada-real
import { ApiException, PortalTarefasClient, type TarefaCriacao } from "../src/api/gerado/portalTarefasClient";

const baseUrl = process.env.API_URL ?? "https://localhost:7260";
const cliente = new PortalTarefasClient(baseUrl, { fetch: (url, init) => fetch(url, init) });

async function main() {
  console.log(`API: ${baseUrl}\n`);

  // 1. Listagem paginada com filtro e ordenação
  const pagina = await cliente.listarTarefas(1, 3, "prioridade", "desc", "Pendente");
  console.log(`Página ${pagina.pagina}/${pagina.totalPaginas} — ${pagina.totalItens} tarefa(s) pendente(s):`);
  for (const t of pagina.itens) console.log(`  #${t.id} [${t.prioridade}] ${t.titulo}${t.vencida ? " (vencida)" : ""}`);

  // 2. Cadastro (POST) — o corpo é tipado pelo DTO gerado
  const nova: TarefaCriacao = { titulo: `Chamada real ${new Date().toISOString()}`, prioridade: "Baixa" };
  const criada = await cliente.criarTarefa(nova);
  console.log(`\nCriada: #${criada.id} "${criada.titulo}" (versão ${criada.versao})`);

  // 3. Erro tratado: recurso inexistente -> 404 Problem Details
  try {
    await cliente.obterTarefa(999999);
  } catch (erro) {
    const status = erro instanceof ApiException ? erro.status : (erro as { status?: number }).status;
    console.log(`\nGET /api/v1/tarefas/999999 -> ${status} (esperado 404)`);
  }

  // 4. Limpeza
  await cliente.excluirTarefa(criada.id);
  console.log(`Excluída: #${criada.id}`);
}

main().catch((erro) => {
  console.error("Falha na chamada real:", erro);
  process.exitCode = 1;
});
