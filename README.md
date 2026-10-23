# Forge

Terminal leve para Windows com workspaces persistentes: grupos e abas configurados uma vez continuam lá depois que o
PC é desligado e ligado de novo. Um clique sobe todos os servidores de um projeto.

Feito pela [G3G Labs](https://www.g3glabs.com.br/). Sem IA, sem conta, sem nuvem, sem telemetria.

## O que faz

- **Abas e grupos** por projeto, com nome, cor e recolher. Renomear, trocar cor e mudar o comando acontecem no lugar.
- **Qualquer shell**: PowerShell 7 e 5.1, CMD, Git Bash e cada distro WSL, detectados sozinhos, mais perfis próprios.
- **Persistência automática**, sem botão salvar. Ao abrir, tudo volta; só os comandos marcados iniciam sozinhos.
- **Ações de um clique**: iniciar, reiniciar e parar o comando da aba ou do grupo inteiro, abrir no VS Code e no
  Explorer, copiar caminho, abrir como administrador.
- **Auxílios**: autocomplete sem maiúsculas no bash e zsh, histórico que sobrevive ao reinício e histórico unificado
  pesquisável com Ctrl+R.
- **Git** na aba e na barra de status: branch, alterações e commits à frente e atrás.

## Estrutura

C# em .NET 10 com WPF, e o terminal nativo do Windows Terminal (C++), a mesma base do WSL Vision.

| Projeto | Papel |
|---|---|
| `src/Forge.App` | Telas (WPF, MVVM). Gera o `Forge.exe`. |
| `src/Forge.Core` | Lógica pura: workspaces, persistência, shells, integração de shell, Git. Sem UI. |
| `src/Forge.Terminal` | Borda com o Windows: controle do terminal (ConPTY), Job Objects, UAC, registro. |
| `tests/Forge.Core.Tests` | Testes do Core (xUnit), rodam em qualquer sistema. |

Detalhes em [docs/ARQUITETURA.md](docs/ARQUITETURA.md); o escopo do produto em [docs/Escopo-Forge.md](docs/Escopo-Forge.md).

## Compilar

Precisa do .NET 10 SDK no Windows 10 (1809 ou mais novo) ou 11.

```bash
dotnet build Forge.sln
```

```bash
dotnet test tests/Forge.Core.Tests
```

```bash
dotnet publish src/Forge.App -c Release -p:Edition=Lite
```

## Dados

Tudo fica em `%APPDATA%\Forge`, em JSON legível: `workspaces.json`, `settings.json` e `history.jsonl`.
A saída dos terminais nunca é salva.
