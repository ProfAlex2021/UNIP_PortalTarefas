# Portal de Tarefas — Desenvolvimento Web com .NET (UNIP)

Projeto de referência construído de forma progressiva pelos 8 roteiros da disciplina
**Desenvolvimento Web com .NET**. Cada roteiro corresponde a **um commit** e a uma **tag**
(`roteiro-01` … `roteiro-08`), para que seja possível ver o estado exato do código ao fim de cada atividade:

```bash
git log --oneline          # um commit por roteiro
git checkout roteiro-03    # código como ficou ao fim do Roteiro 3
git diff roteiro-02 roteiro-03 --stat   # o que mudou de um roteiro para o outro
```

| Roteiro | Tema | Guia |
|---|---|---|
| 1 | Pipeline HTTP, middleware, roteamento, binding, Minimal APIs x MVC | [docs/roteiros/roteiro-01.md](docs/roteiros/roteiro-01.md) |
| 2 | Controllers, validação, filtros e interfaces Razor reutilizáveis | [docs/roteiros/roteiro-02.md](docs/roteiros/roteiro-02.md) |

## Ambiente

- Visual Studio 2026 (18.x) com a carga de trabalho **ASP.NET e desenvolvimento Web**
- SDK .NET 10 (fixado em `global.json`, com `rollForward: latestFeature`)

## Executar

```bash
dotnet run --project src/PortalTarefas.Web --launch-profile https
dotnet test
```

Ou abra `PortalTarefas.slnx` no Visual Studio 2026 e pressione F5.
