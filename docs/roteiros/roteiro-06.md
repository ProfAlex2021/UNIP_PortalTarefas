# Roteiro 6 — ASP.NET Core Identity, cookies, tokens bearer, JWT, roles, claims e rotas protegidas

## Preparação (uma vez por máquina)

Os usuários de teste estão descritos em `appsettings.Development.json` **sem senha**. As senhas ficam
em User Secrets (fora do repositório). Defina uma senha para cada um (mínimo 10 caracteres, com
maiúscula, minúscula e número):

```bash
dotnet user-secrets set "SeedUsuarios:0:Senha" "<senha da admin>"  --project src/PortalTarefas.Web
dotnet user-secrets set "SeedUsuarios:1:Senha" "<senha da Ana>"    --project src/PortalTarefas.Web
dotnet user-secrets set "SeedUsuarios:2:Senha" "<senha do Bruno>"  --project src/PortalTarefas.Web
dotnet user-secrets list --project src/PortalTarefas.Web           # para consultar depois
```

Ao iniciar em Development, a aplicação aplica a migration `IdentidadeEEquipes` e cria (uma única vez):

| E-mail | Papel | Equipe | Claim extra |
|---|---|---|---|
| admin@portaltarefas.local | Administrador | Alfa | `permissao = tarefas.editar-outras-equipes` |
| ana@portaltarefas.local | Usuario | Alfa | — |
| bruno@portaltarefas.local | Usuario | Beta | — |

Usuário sem senha configurada não é criado (aparece um aviso no log), em vez de receber uma senha fixa.

## Arquivos produzidos

| Arquivo | Papel |
|---|---|
| `Seguranca/ConfiguracaoSeguranca.cs` | Identity, requisitos de senha e bloqueio, cookie, políticas |
| `Seguranca/Usuario.cs` | `Usuario : IdentityUser`, nomes de papéis, claims e políticas |
| `Seguranca/EdicaoTarefaHandler.cs` | Autorização por recurso (equipe, proprietário, claim, admin) |
| `Seguranca/ProtecaoCsrfMiddleware.cs` | Exige `X-Requested-With` em alterações via cookie |
| `Seguranca/SemeadorIdentidade.cs` | Seed controlado e idempotente de papéis e usuários |
| `Api/AutenticacaoEndpoints.cs` | `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/sessao` |
| `Controllers/ContaController.cs` + `Views/Conta` | Login/logout da interface MVC |
| `Dados/Migrations/*IdentidadeEEquipes*` | Tabelas do Identity + colunas `Equipe` e `CriadaPorId` ([SQL](../sql/03-IdentidadeEEquipes.sql)) |
| Front-end: `src/autenticacao/*`, `src/paginas/Entrar.tsx`, `src/api/autenticacaoApi.ts` | Contexto de sessão, rota protegida e login |

## Etapa 2 — Escolha do esquema: cookie

| Critério | Cookie do Identity (escolhido) | Token bearer do Identity | JWT de um provedor OAuth/OIDC |
|---|---|---|---|
| Origem | Mesma origem (proxy em dev; API serve a SPA em produção) | Qualquer | Qualquer |
| Clientes | Navegador | Navegador, apps, scripts | Vários sistemas, SSO |
| Armazenamento no cliente | Cookie `HttpOnly`: JavaScript não lê | Precisa guardar o token (memória/armazenamento) | Idem |
| Risco principal | CSRF | Roubo do token por XSS | Roubo do token; configuração do provedor |
| Revogação | Logout troca o *security stamp*: cookies antigos são recusados | Token vale até expirar (exige *refresh*) | Expira; revogação depende do provedor |
| Infraestrutura | Nenhuma extra | Nenhuma extra | Servidor de identidade |

**Decisão:** o único cliente é a SPA no mesmo domínio, então o cookie é o mais simples e seguro: o
navegador gerencia a sessão e nenhum token fica acessível a scripts. Se surgirem outros clientes
(aplicativo móvel, integrações), a recomendação é um provedor OAuth/OIDC emitindo JWT de curta duração.

**Controles do cookie:** `HttpOnly`, `Secure` (somente HTTPS), `SameSite=Strict`, expiração de 8 h com
renovação deslizante, nome próprio (`PortalTarefas.Sessao`). Para a API, sem sessão → **401** e sem
permissão → **403** (em vez de redirecionar para a página de login).

**CSRF:** além do `SameSite=Strict`, requisições que alteram dados em `/api` e carregam o cookie precisam do
cabeçalho `X-Requested-With: PortalTarefas`. Outro site não consegue enviá-lo sem um *preflight* CORS, que a
política da API recusa. Os formulários MVC usam o token antiforgery (`AutoValidateAntiforgeryToken`).

**Logout de verdade:** o logout troca o *security stamp* do usuário e o cookie é conferido a cada requisição
(`ValidationInterval = 0`). Um cookie copiado antes do logout passa a receber 401. O custo é uma consulta por
requisição autenticada — aceitável neste projeto e documentado.

**Senha e bloqueio:** mínimo de 10 caracteres com maiúscula, minúscula e número; 5 tentativas falhas
bloqueiam a conta por 5 minutos. O login responde sempre "E-mail ou senha inválidos." (401), sem revelar
se a conta existe ou está bloqueada.

## Etapa 3 — Autorização

| Política | Regra | Onde |
|---|---|---|
| `tarefas.ler` | usuário autenticado | todo o grupo `/api/v1/tarefas`, endpoints do Roteiro 1, páginas MVC |
| `tarefas.criar` | papel Usuario ou Administrador | POST |
| `tarefas.editar` | papel + **recurso**: mesma equipe, proprietário, claim `tarefas.editar-outras-equipes` ou Administrador | PUT, edição e mudança de situação no MVC |
| `tarefas.editar-outras-equipes` | Administrador ou claim `permissao = tarefas.editar-outras-equipes` | usada pelo handler |
| `tarefas.excluir` | papel Administrador | DELETE |

A edição carrega a tarefa e chama `IAuthorizationService.AuthorizeAsync(usuario, tarefa, "tarefas.editar")`;
por isso o bloqueio vale mesmo para requisições feitas fora da SPA (curl, `.http`, Postman).

## Etapa 4 — SPA protegida

- `ProvedorAutenticacao` restaura a sessão com `GET /api/auth/sessao` ao abrir a aplicação (nada em `localStorage`).
- `RotaProtegida` redireciona para `/entrar?retorno=<url pretendida>`; após o login, volta à mesma URL com os filtros.
  `retornoSeguro` só aceita caminhos internos (evita redirecionamento para outro site).
- Qualquer 401 da API encerra a sessão na SPA (evento `portaltarefas:sessao-expirada`).
- "Nova tarefa" e "Excluir" aparecem conforme `sessao.permissoes`, calculadas **pelo servidor**. Esconder
  botões é só experiência: a API continua sendo a autoridade final.

## Matriz de testes (anônimo, usuário comum, administrador)

| Operação | Anônimo | Usuário (Ana, Alfa) | Administrador |
|---|---|---|---|
| GET `/api/v1/tarefas` | 401 | 200 | 200 |
| POST `/api/v1/tarefas` | 401 | 201 | 201 |
| PUT tarefa da própria equipe | 401 | 200 | 200 |
| PUT tarefa de outra equipe (Bruno, Beta → Alfa) | 401 | **403** | 200 (claim) |
| DELETE `/api/v1/tarefas/{id}` | 401 | **403** | 204 |
| Página MVC `/Tarefas` | redireciona ao login | 200 | 200 |

Automatizada em `tests/.../Roteiro06/MatrizAutorizacaoTests.cs`.

## Extensões implementadas

- **Política baseada no proprietário do recurso** (`EdicaoTarefaHandler`: quem criou pode editar).
- **Auditoria de login/logout** na categoria de log `Auditoria`, registrando só o ID do usuário —
  nunca senha, cookie ou o e-mail digitado em tentativas falhas.
- **Testes de integração** de autenticação e autorização (`Roteiro06/*`) e testes da rota protegida no front-end.

## Testes obrigatórios (relatório, item 4)

| Teste | Onde |
|---|---|
| Login válido e inválido sem revelar se a conta existe | `AutenticacaoTests.Login_*`, `BloqueioDeContaTests` |
| Logout com rejeição do cookie invalidado | `Logout_invalida_o_cookie_mesmo_que_ele_seja_reenviado` |
| 401 para não autenticado | `MatrizAutorizacaoTests` (coluna anônimo) |
| 403 para autenticado sem permissão | `Exclusao_somente_administrador`, `Usuario_de_outra_equipe_nao_edita_403` |
| Role/claim no servidor e reflexo na SPA | `Sessao_anonima_e_sessao_autenticada_*`; botões conforme `permissoes` |
