# Roteiro 2 — Controllers, validação, filtros e interfaces Razor reutilizáveis

## Como executar

```bash
dotnet run --project src/PortalTarefas.Web --launch-profile https
# abrir https://localhost:7260/Tarefas
dotnet test --filter "FullyQualifiedName~Roteiro02"
```

## Arquivos produzidos

| Arquivo | Papel |
|---|---|
| `Program.cs` | `AddControllersWithViews`, rota convencional, endpoints do Roteiro 1 no grupo `/api` |
| `Dominio/Tarefa.cs` | Modelo de domínio (não chega às views nem à API diretamente) |
| `Servicos/ITarefaService.cs`, `TarefaServiceEmMemoria.cs` | Serviço em memória isolando a persistência futura |
| `ViewModels/TarefaInputModel.cs` | Entrada do formulário com Data Annotations em português |
| `ViewModels/TarefaViewModels.cs` | ViewModels de lista, resumo e detalhes |
| `Validacao/PrazoCompativelComPrioridadeAttribute.cs` | Validação customizada envolvendo prioridade, prazo e situação |
| `Validacao/ValidadorTituloUnico.cs` | **Extensão:** validação assíncrona dependente de dados |
| `Controllers/TarefasController.cs` | Ações Index, Details, Create e Edit |
| `Filtros/RegistrarExecucaoAcaoFilter.cs` | Filtro de ação: nome da ação, duração e resultado |
| `ViewComponents/TarefasVencidasViewComponent.cs` | **Extensão:** View Component de tarefas vencidas |
| `Views/Shared/_Layout.cshtml` | Layout com navegação, mensagens e View Component |
| `Views/Shared/_TarefaResumo.cshtml` | Partial com o resumo de uma tarefa |
| `Views/Tarefas/_Formulario.cshtml` | Partial com os campos usados por Create e Edit |
| `tests/.../Roteiro02/*` | **Extensão:** testes unitários do controller e da validação + integração das páginas |

## Onde layout, partials e Tag Helpers reduzem duplicação

- **`_Layout.cshtml`**: cabeçalho, navegação, rodapé, área de mensagens e o painel de vencidas
  são escritos uma vez e aparecem em todas as páginas (`_ViewStart.cshtml` define o layout).
- **`_TarefaResumo.cshtml`**: o mesmo "cartão" de tarefa é usado na lista e nos detalhes.
- **`_Formulario.cshtml`**: Create e Edit compartilham os mesmos campos e mensagens.
- **Tag Helpers** (`asp-for`, `asp-validation-for`, `asp-action`, `asp-route-id`, `asp-items`):
  geram `name`, `id`, `value`, rótulos (`[Display]`), links e mensagens a partir do modelo,
  sem strings repetidas à mão. O `<form>` com Tag Helper inclui o token antiforgery.
- **View Component** (`<vc:tarefas-vencidas />`): diferente da partial, busca os próprios dados.

## Validação

1. **Data Annotations** em `TarefaInputModel` (`Required`, `StringLength`, `EnumDataType`) com mensagens em português.
2. **Regra com mais de um campo** (`[PrazoCompativelComPrioridade]`):
   prioridade Alta exige prazo; prazo no passado só é aceito para tarefa concluída.
   O erro é associado ao campo `Prazo`.
3. **Regra dependente de dados** (`ValidadorTituloUnico`): não pode haver duas tarefas em aberto com o mesmo título.
4. O controller verifica `ModelState.IsValid` **antes** de chamar o serviço; se inválido, devolve a
   mesma view com o modelo recebido — os dados digitados são preservados.

Observação: a validação de nível de classe (item 2) só é executada pelo MVC quando as propriedades
individuais são válidas. Por isso, com título vazio e prioridade Alta sem prazo, aparece primeiro
o erro do título; corrigido o título, aparece o erro do prazo.

## Filtro de ação

`RegistrarExecucaoAcaoFilter` é registrado **por atributo** (`[ServiceFilter<...>]`) no
`TarefasController`. O escopo é o controller porque só as ações da interface MVC precisam desse
registro; as APIs já são registradas pelo middleware do Roteiro 1 e duplicar o log em todas as
camadas geraria ruído. O filtro apenas observa: a validação continua no `ModelState`.

Exemplo de log (POST inválido):

```
Ação Tarefas.Create [POST] iniciada (ModelState válido: False)
Ação Tarefas.Create [POST] concluída com View '(padrão)' (status 200) em 1.0 ms
```

## Testes obrigatórios (relatório, item 4)

| Teste | Onde |
|---|---|
| Cadastro válido e com título vazio | `TarefasControllerTests.Create_*`, `InterfaceMvcTests.Formulario_com_titulo_vazio_*` |
| Validação customizada de prazo | `ValidacaoPrazoTests` |
| Identificador existente e inexistente | `TarefasControllerTests.Details_*`, `InterfaceMvcTests.Detalhes_de_id_inexistente_*` |
| Três páginas compartilhando layout | `InterfaceMvcTests.Paginas_compartilham_o_mesmo_layout` (4 páginas) |
| Filtro sem duplicar regra de negócio | Logs acima; o filtro não acessa o serviço nem altera o resultado |
