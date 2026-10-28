// Carga CONTROLADA para observar rate limiting, output caching e resiliência (Roteiro 7).
// Não serve para declarar capacidade de produção: roda numa só máquina, contra o
// ambiente de desenvolvimento, com o próprio gerador disputando CPU com a API.
//
// Uso (com a API rodando no perfil https):
//   node tools/carga/carga.mjs [urlBase] [requisicoes] [concorrencia]
// Exemplo:
//   node tools/carga/carga.mjs https://localhost:7260 200 10
//
// Para confiar no certificado de desenvolvimento, exporte-o uma vez e use NODE_EXTRA_CA_CERTS:
//   dotnet dev-certs https --export-path .cert/localhost.pem --format Pem
//   NODE_EXTRA_CA_CERTS=.cert/localhost.pem node tools/carga/carga.mjs

const [base = "https://localhost:7260", totalTexto = "200", concTexto = "10"] = process.argv.slice(2);
const total = Number(totalTexto);
const concorrencia = Number(concTexto);

function percentil(valores, p) {
  const ordenados = [...valores].sort((a, b) => a - b);
  return ordenados[Math.min(ordenados.length - 1, Math.floor((p / 100) * ordenados.length))] ?? 0;
}

async function cenario(nome, requisicao) {
  const status = new Map();
  const duracoes = [];
  let doCache = 0;
  let proxima = 0;

  async function trabalhador() {
    while (proxima < total) {
      proxima++;
      const inicio = performance.now();
      try {
        const resposta = await requisicao();
        await resposta.arrayBuffer();
        status.set(resposta.status, (status.get(resposta.status) ?? 0) + 1);
        if (resposta.headers.has("age")) doCache++;
      } catch {
        status.set("erro de rede", (status.get("erro de rede") ?? 0) + 1);
      }
      duracoes.push(performance.now() - inicio);
    }
  }

  const inicio = performance.now();
  await Promise.all(Array.from({ length: concorrencia }, trabalhador));
  const segundos = (performance.now() - inicio) / 1000;

  console.log(`\n== ${nome}`);
  console.log(`   ${total} requisições, concorrência ${concorrencia}, ${segundos.toFixed(2)} s (${(total / segundos).toFixed(0)} req/s)`);
  console.log(`   status: ${[...status].map(([s, n]) => `${s}=${n}`).join("  ")}`);
  console.log(`   latência ms: p50=${percentil(duracoes, 50).toFixed(1)}  p95=${percentil(duracoes, 95).toFixed(1)}  máx=${Math.max(...duracoes).toFixed(1)}`);
  if (doCache) console.log(`   respostas servidas do output cache (cabeçalho Age): ${doCache}`);
}

console.log(`Carga controlada contra ${base} (resultado local, não é medida de capacidade)`);

await cenario("Leitura pública com output cache — GET /api/publico/painel", () =>
  fetch(`${base}/api/publico/painel`));

await cenario("Login com rate limiting — POST /api/auth/login (credenciais inválidas)", () =>
  fetch(`${base}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: "carga@portaltarefas.local", senha: "SenhaInvalida123" }),
  }));

await cenario("Liveness — GET /health/live", () => fetch(`${base}/health/live`));
