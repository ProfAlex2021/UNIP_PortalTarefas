# Portal de Tarefas — Desenvolvimento Web com .NET (UNIP)

Projeto de referência construído de forma progressiva pelos 8 roteiros da disciplina
**Desenvolvimento Web com .NET**. Cada roteiro corresponde a **um commit** e a uma **tag**
(`roteiro-01` … `roteiro-08`), para que seja possível ver o estado exato do código ao fim de cada atividade:

```bash
git log --oneline          # um commit por roteiro
git checkout roteiro-03    # código como ficou ao fim do Roteiro 3
git diff roteiro-02 roteiro-03 --stat   # o que mudou de um roteiro para o outro
git checkout main          # voltar à versão final
```

| Roteiro | Tema | Guia |
|---|---|---|
| 1 | Pipeline HTTP, middleware, roteamento, binding, Minimal APIs x MVC | [docs/roteiros/roteiro-01.md](docs/roteiros/roteiro-01.md) |
| 2 | Controllers, validação, filtros e interfaces Razor reutilizáveis | [docs/roteiros/roteiro-02.md](docs/roteiros/roteiro-02.md) |
| 3 | EF Core, migrations, seed, pooling, transações, concorrência e acesso leve | [docs/roteiros/roteiro-03.md](docs/roteiros/roteiro-03.md) |
| 4 | Recursos REST, paginação, filtros, Problem Details, OpenAPI e cliente TypeScript | [docs/roteiros/roteiro-04.md](docs/roteiros/roteiro-04.md) |
| 5 | Front-end React consumindo a API com estados de requisição e roteamento | [docs/roteiros/roteiro-05.md](docs/roteiros/roteiro-05.md) |
| 6 | ASP.NET Core Identity, cookies, tokens bearer, JWT, roles, claims e rotas protegidas | [docs/roteiros/roteiro-06.md](docs/roteiros/roteiro-06.md) |
| 7 | Logging, métricas, tracing, Health Checks, resiliência, rate limiting e output caching | [docs/roteiros/roteiro-07.md](docs/roteiros/roteiro-07.md) |
| 8 | Configuração por ambientes, segredos, contêineres e pipeline reprodutível | [docs/roteiros/roteiro-08.md](docs/roteiros/roteiro-08.md) |

Cada guia traz: como executar, arquivos produzidos, decisões técnicas, extensões implementadas e onde estão
as evidências pedidas no relatório. O contrato da API está em [docs/contrato-api-v1.md](docs/contrato-api-v1.md).

## Estrutura

```
PortalTarefas.slnx                 solução (abre no Visual Studio 2026)
src/PortalTarefas.Web/             API (Minimal APIs) + interface MVC/Razor + Identity
src/PortalTarefas.Frontend/        SPA React (Vite + TypeScript) e cliente gerado pelo NSwag
tests/PortalTarefas.Tests/         testes de unidade e integração (pastas por roteiro)
contratos/                         documento OpenAPI gerado no build (versionado)
docs/                              guias por roteiro, contrato e scripts SQL das migrations
tools/carga/                       carga controlada (Roteiro 7)
Dockerfile, docker-compose*.yml    contêineres (Roteiros 7 e 8)
.github/workflows/ci-cd.yml        pipeline de CI/CD (Roteiro 8)
```

## Ambiente

- Visual Studio 2026 (18.x) com a carga de trabalho **ASP.NET e desenvolvimento Web** (inclui SQL Server LocalDB)
- SDK .NET 10 (fixado em `global.json`, com `rollForward: latestFeature`)
- Node.js 22 LTS e npm — cliente TypeScript (Roteiro 4) e SPA React (Roteiro 5)
- Docker Desktop — contêineres (Roteiros 7 e 8)

## Primeira execução (máquina nova)

```bash
dotnet tool restore
# Connection string e senhas dos usuários de teste ficam em User Secrets (fora do repositório):
dotnet user-secrets set "ConnectionStrings:PortalTarefas" "Server=(localdb)\MSSQLLocalDB;Database=PortalTarefas;Trusted_Connection=True;TrustServerCertificate=True" --project src/PortalTarefas.Web
dotnet user-secrets set "SeedUsuarios:0:Senha" "<senha da admin>" --project src/PortalTarefas.Web
dotnet user-secrets set "SeedUsuarios:1:Senha" "<senha da Ana>"   --project src/PortalTarefas.Web
dotnet user-secrets set "SeedUsuarios:2:Senha" "<senha do Bruno>" --project src/PortalTarefas.Web
```

Senhas: mínimo de 10 caracteres com maiúscula, minúscula e número. Detalhes nos Roteiros 3 e 6.

## Executar

```bash
dotnet run --project src/PortalTarefas.Web --launch-profile https   # https://localhost:7260 (MVC, API, /scalar)
cd src/PortalTarefas.Frontend && npm install && npm run dev           # http://localhost:5173 (SPA)
dotnet test                                                          # testes .NET (usam LocalDB, banco isolado por classe)
cd src/PortalTarefas.Frontend && npm test                            # testes do front-end
docker compose up -d --build                                         # tudo em contêiner (ver Roteiro 8)
```

Ou abra `PortalTarefas.slnx` no Visual Studio 2026, escolha o perfil **https** e pressione F5.
