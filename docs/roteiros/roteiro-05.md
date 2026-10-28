# Roteiro 5 — Front-end React consumindo a API com estados de requisição e roteamento

## Versões usadas

| Ferramenta | Versão |
|---|---|
| Node.js (LTS) | 22.16.0 |
| npm | 10.9.2 |
| Vite | 7.3.7 |
| React / React DOM | 19.3.0 |
| React Router | 7.18.5 |
| TypeScript | 5.9.3 |
| Vitest / Testing Library | 3.2.7 / 16.3.3 |

As versões estão fixadas em `src/PortalTarefas.Frontend/package.json` e `package-lock.json`.

## Como executar

Em dois terminais (ou no Visual Studio, iniciando a API e usando o terminal integrado para o front-end):

```bash
# 1) API
dotnet run --project src/PortalTarefas.Web --launch-profile https

# 2) Front-end
cd src/PortalTarefas.Frontend
npm install
npm run dev          # http://localhost:5173
npm test             # testes de componentes e da camada HTTP
npm run build        # build de produção em dist/
```

## Configuração de ambiente, proxy e CORS

`.env.development` (sem segredos — tudo com prefixo `VITE_` vai para o navegador):

| Variável | Valor | Uso |
|---|---|---|
| `VITE_API_BASE_URL` | vazio | URL base usada pelo cliente no navegador; vazio = mesma origem |
| `API_PROXY_TARGET` | `https://localhost:7260` | Destino do proxy do Vite (lida só no `vite.config.ts`) |

**Decisão: proxy de desenvolvimento.** O navegador chama `http://localhost:5173/api/...`; o Vite repassa
para a API. Para o navegador é a mesma origem, então não há CORS nem cookies entre origens (importante no
Roteiro 6). O CORS da API continua restrito a `http://localhost:5173` (política `FrontendLocal`, sem
`AllowAnyOrigin`) e é usado apenas se `VITE_API_BASE_URL` apontar diretamente para a API.

## Camada de acesso à API

| Arquivo | Papel |
|---|---|
| `src/api/gerado/portalTarefasClient.ts` | Cliente e tipos gerados pelo NSwag (Roteiro 4) — não editar |
| `src/api/cliente.ts` | Instância única do cliente e função central `executar()` → `Resultado<T>` |
| `src/api/erros.ts` | `normalizarErro()`: separa falha de rede, status sem Problem Details, Problem Details e cancelamento |
| `src/api/tarefasApi.ts` | Funções usadas pelas páginas (`listar`, `obter`, `criar`) e tipos re-exportados do contrato |
| `src/api/simulacao.ts` | Simulação de atraso, falha de rede e erro 500 (somente em desenvolvimento) |
| `src/hooks/useRequisicao.ts` | Estados `idle`, `carregando`, `vazio`, `sucesso`, `erro`; cancelamento com `AbortController` |

Nenhum componente chama `fetch` diretamente nem trata `Response`: todos recebem `{ ok: true, dados }` ou
`{ ok: false, erro }` já normalizado.

## Páginas e rotas

| Rota | Página | Destaques |
|---|---|---|
| `/` | `Inicio` | Chamada simples que confirma o acesso à API |
| `/tarefas` | `ListaTarefas` | Sem parâmetros: estado **idle**. Filtros, ordenação e paginação **na URL** |
| `/tarefas?pagina=1&situacao=Pendente…` | `ListaTarefas` | Funciona por URL direta, voltar/avançar e recarregar |
| `/tarefas/:id` | `DetalheTarefa` | Trata 404 com página própria |
| `/tarefas/nova` | `NovaTarefa` | Mostra os erros por campo do Problem Details (400); sucesso navega sem recarregar |

## Como observar os cinco estados

| Estado | Como provocar |
|---|---|
| idle | Abrir `/tarefas` sem parâmetros |
| carregando | Rodapé → "Simular: atraso de 2,5 s" e clicar em Buscar |
| vazio | Buscar um texto inexistente |
| sucesso | Buscar sem filtros |
| erro | Rodapé → "falha de rede" ou "erro 500"; ou parar a API |

Em todos os casos de erro há o botão **Tentar novamente**, e a tela não fica presa em "carregando".
Ao trocar de filtro durante uma requisição lenta, a anterior é cancelada (`AbortController`) e sua
resposta, se chegar, é ignorada.

## Extensões implementadas

- **Testes** (`npm test`): `src/api/erros.test.ts` (camada HTTP) e `src/paginas/ListaTarefas.test.tsx` (componente).
- **Acessibilidade**: estados anunciados com `role="status"`/`role="alert"` e `aria-live`; foco no título a cada
  troca de rota; foco no primeiro campo inválido; `aria-invalid` e `aria-describedby` nos campos; link
  "Pular para o conteúdo"; animação respeita `prefers-reduced-motion`.

## Testes obrigatórios (relatório, item 4)

| Teste | Onde / como |
|---|---|
| Estado inicial, carregamento normal e lista vazia | `ListaTarefas.test.tsx` + tela `/tarefas` |
| Erro de rede e resposta HTTP de erro | Simulação no rodapé; `erros.test.ts` |
| Navegação entre lista e detalhes por URL | Links dos cartões; `/tarefas/1` direto no navegador |
| Filtro e paginação preservados na URL | Teste "lê os filtros da URL…"; botões Anterior/Próxima |
| Cadastro válido sem recarregar a página | `/tarefas/nova` → navega para os detalhes com mensagem |
