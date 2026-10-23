<p align="center">
  <img src="assets/brand/forge-logo-256.png" width="128" height="128" alt="Logo do Forge">
</p>

<h1 align="center">Forge</h1>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/licen%C3%A7a-MIT-blue.svg" alt="Licença: MIT"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4" alt="Windows 10 e 11">
  <img src="https://img.shields.io/badge/status-pr%C3%A9--alfa-lightgrey" alt="Status: pré-alfa">
</p>

Terminal leve para Windows em que abas e grupos configurados uma vez continuam lá depois que o PC é desligado e ligado de novo. Cada grupo é um projeto, com a pasta e o ambiente dele (Windows ou WSL): ao abrir, os terminais já estão lá, organizados.

**Por quê?** Terminais como o Warp e o Windows Terminal agrupam abas e abrem PowerShell, CMD, Git Bash e WSL, mas, depois de reiniciar o PC, a organização se perde: projeto por projeto, navegar até a pasta, renomear as abas, reagrupar e rodar `npm run dev` em cada uma. O Forge faz só o essencial (abas, grupos, cores, Git, ações rápidas, autocomplete e histórico) e trata a organização como algo permanente.

**O que ele não é:** sem IA, sem conta, sem nuvem e sem motor web embutido. Um único `.exe` para Windows, em que cada recurso precisa justificar o próprio consumo de memória.

> [!NOTE]
> Projeto em fase inicial. O código compila e os testes passam, mas o app ainda não foi testado no Windows. Acompanhe e sugira pelas [Issues](../../issues).

## O que tem

| Recurso | O que entrega |
| --- | --- |
| **Shells** | PowerShell 7 e 5.1, CMD, Git Bash e cada distro WSL, detectados sozinhos. Perfis próprios (executável + argumentos). |
| **Abas** | Criar com um clique ("+" ou `Ctrl+T`), já na pasta e no ambiente do grupo; fechar, duplicar, arrastar, renomear com duplo clique ou `F2`, cor própria. |
| **Grupos (workspaces)** | Um por projeto: nome, cor, a pasta do projeto e o ambiente (Windows, com PowerShell, CMD ou Git Bash, ou WSL, com a distro), escolhidos ao criar e editáveis depois. Recolher, e arrastar abas entre grupos sem parar o processo. |
| **Configuração da aba** | Nome e cor com prévia, shell (o do grupo ou outro), pasta, "comando ao abrir" opcional (ex.: `bun dev`, `yarn dev`), variáveis de ambiente e "abrir junto com o app", num painel no lugar. |
| **Persistência** | Tudo salva sozinho, sem botão. Ao reabrir, cada aba volta com nome, pasta e shell; só os comandos marcados iniciam. |
| **Auxílios** | Autocomplete que ignora maiúsculas no bash e zsh (`cd doc` + Tab → `Documentos/`), histórico do shell gravado a cada comando e histórico unificado de todas as abas, pesquisável com `Ctrl+R`. |
| **Git** | Branch na aba e na barra de status, alterações pendentes e commits à frente e atrás. Muda sozinho depois de um `git checkout`. |
| **Ações rápidas** | Abrir a pasta no VS Code (inclusive conectado ao WSL) e no Explorer; copiar caminho; reiniciar o shell; abrir como administrador. O Forge não tem botão de iniciar ou parar projeto: ele não adivinha se o projeto usa npm, yarn ou bun. |
| **Aparência** | Temas escuro e claro, fonte, tamanho e cursor. Importa esquemas de cores do Windows Terminal. |
| **Identificação** | Nome de usuário e de máquina que você escolhe, no cabeçalho da aba e, se quiser, no prompt do PowerShell e do bash. |

Abas de administrador abrem numa segunda janela, elevada pelo UAC e marcada em amarelo: o Windows não permite elevar só uma aba dentro de uma janela comum.

### Atalhos

| Ação | Atalho |
| --- | --- |
| Nova aba no grupo atual | `Ctrl+T` |
| Novo grupo | `Ctrl+Shift+G` |
| Fechar aba | `Ctrl+W` |
| Renomear aba | `F2` |
| Próxima / anterior | `Ctrl+Tab` / `Ctrl+Shift+Tab` |
| Ir para a aba 1–9 | `Ctrl+1` … `Ctrl+9` |
| Reiniciar o shell da aba | `Ctrl+Shift+R` |
| Buscar no histórico unificado | `Ctrl+R` |
| Abrir pasta no VS Code | `Ctrl+Shift+E` |
| Recolher barra lateral | `Ctrl+B` |
| Configurações | `Ctrl+,` |

Os atalhos valem com o foco dentro do terminal e podem ser remapeados no `settings.json`. `Ctrl+R` e `Ctrl+W` substituem a busca reversa e o "apagar palavra" do bash; isso ainda pode mudar antes do beta.

## Download

Ainda não há versão publicada. Quando houver, os `.exe` ficam em [Releases](../../releases), sem instalação, em duas edições:

- **Forge-lite.exe**: usa o [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) já instalado. Ocupa menos disco e menos RAM.
- **Forge.exe**: portátil, roda em qualquer Windows 10 ou 11 sem instalar nada.

## Requisitos

- Windows 10 versão 1809 ou mais nova (exigida pelo ConPTY) ou Windows 11.
- Opcional: WSL 2 com uma ou mais distros, Git for Windows, Git e VS Code (com a extensão WSL, para abrir pastas de abas WSL).

O app nunca pede administrador, exceto quando você abre uma aba de administrador.

## Compilar e rodar

```powershell
# no Windows, com o .NET 10 SDK
dotnet run --project src/Forge.App
```

Gerar o `.exe` único:

| Edição | Comando |
| --- | --- |
| **Leve** | `dotnet publish src/Forge.App -c Release -p:Edition=Lite -o publish/lite` |
| **Portátil** | `dotnet publish src/Forge.App -c Release -o publish` |

Testes da lógica (rodam em qualquer sistema, inclusive Linux):

```bash
dotnet test tests/Forge.Core.Tests
```

Argumentos: `--minimized` (usado por "Iniciar com o Windows") e `--admin` (janela de administrador).

### Publicar uma versão

As releases saem pelo GitHub Actions ([release.yml](.github/workflows/release.yml)) a partir de uma tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

O workflow roda os testes, gera as duas edições e cria a release com `Forge-lite.exe`, `Forge.exe` e `SHA256SUMS.txt`. Tags com sufixo (ex.: `v0.1.0-beta.1`) viram pré-lançamento.

## Seus dados

Tudo fica em `%APPDATA%\Forge`, em JSON legível para editar à mão e versionar:

| Arquivo | Conteúdo |
| --- | --- |
| `workspaces.json` | Grupos, abas, pastas, comandos, layout da janela |
| `workspaces-admin.json` | As abas da janela de administrador |
| `settings.json` | Preferências, perfis de shell, tema, atalhos |
| `history.jsonl` | Histórico unificado: um comando por linha, com data, aba e pasta (até 50 mil linhas) |

A gravação é atômica e mantém uma cópia do último arquivo válido: desligar o PC no meio de uma gravação não corrompe os workspaces. A saída dos terminais nunca é salva.

## Arquitetura

C# em .NET 10 com WPF, e o terminal nativo do Windows Terminal (C++, ConPTY e DirectWrite), a mesma base do [WSL Vision](https://github.com/G3G-Labs/wsl-vision).

```
src/
├── Forge.App        telas (WPF, MVVM): barra lateral, abas, Ctrl+R, Configurações. Gera o Forge.exe
├── Forge.Core       lógica pura, sem interface: workspaces, persistência, shells,
│                    integração de shell (OSC 133), Git, ações. Testado em qualquer sistema
└── Forge.Terminal   borda com o Windows: controle do terminal, Job Objects, UAC, registro
tests/Forge.Core.Tests   xUnit
assets/brand             marca (SVG e PNG); tools/gerar-logo.js a gera de novo
```

A dependência é em um só sentido: o App usa o Core e o Terminal; o Terminal usa o Core; o Core não conhece nenhum dos dois. Detalhes, fluxos e decisões em [docs/ARQUITETURA.md](docs/ARQUITETURA.md); o escopo do produto em [docs/Escopo-Forge.md](docs/Escopo-Forge.md).

### Decisões técnicas

- **Terminal nativo do Windows Terminal em vez de um emulador próprio.** Corta o maior item de engenharia do escopo (parser VT e renderização) e o risco de quebrar vim, htop e as barras do npm. O projeto `Forge.Terminal` isola o controle: dá para trocá-lo por um motor próprio sem mexer nas telas.
- **Grupo = projeto.** O grupo guarda a pasta e o ambiente; as abas seguem o grupo, a menos que você escolha outro shell ou outra pasta para uma aba. Mudar o ambiente do grupo muda todas as abas que o seguem.
- **Sem iniciar ou parar projeto.** O "comando ao abrir" é opcional e escrito por você; o app o digita no shell quando ele abre, como se você digitasse, e `Ctrl+C` para o processo sem fechar a aba.
- **Nada é gravado nos seus arquivos.** Pasta, variáveis, autocomplete, histórico e integração de shell vêm de um script que o app gera para cada aba e passa ao shell (`--rcfile`, `ZDOTDIR`, `-Command`). Seu `.bashrc`, `.zshrc` e perfil do PowerShell continuam carregando normalmente.
- **Abertura rápida com muitas abas.** Todas aparecem na hora, mas só a aba visível e as marcadas para iniciar sozinhas ganham shell; as outras só quando você clica nelas.
- **Nenhum processo órfão.** O app roda num Job Object do Windows: se ele fechar ou cair, os shells e servidores encerram junto.

## Metas de leveza

Uma versão que estoure qualquer uma destas metas não é lançada:

| Métrica | Meta |
| --- | --- |
| RAM do app com 10 abas abertas (sem contar os shells) | < 150 MB |
| Abertura até a primeira aba utilizável | < 1 s |
| Restauração de 20 abas em 5 grupos | < 2 s |
| CPU com todas as abas ociosas | < 1% |
| Latência de digitação | < 16 ms |

## Privacidade

Funciona offline, sem conta e sem telemetria. A única chamada de rede é a verificação de atualização, que pode ser desligada.

## Estado atual

- Estrutura completa da solução: Core, Terminal, App, testes do Core, workflow de release e marca.
- Telas desenhadas para todas as funcionalidades do MVP (janela principal, abas e grupos, Ctrl+R, erro e Git, administrador, Configurações e Sobre).
- Compila sem avisos; 59 testes de unidade passam. As duas edições do `.exe` são geradas (leve ~9 MB, portátil ~134 MB).
- **Ainda não testado no Windows.** As pendências conhecidas estão no fim de [docs/ARQUITETURA.md](docs/ARQUITETURA.md) (busca no terminal, aviso de shell encerrado, log em arquivo e verificação de atualização).

## Próximas versões

1. Arquivo de workspace no repositório (`.workspace.json`), importado pela equipe com um clique.
2. Sugestão de comandos a partir dos scripts do `package.json`.
3. Divisão de tela: duas ou mais abas lado a lado.
4. Alerta quando o comando de uma aba termina com erro ou cai.
5. Porta em uso na aba (ex.: `:3000`), com link para o navegador.

## Contribuindo

Issues e pull requests são bem-vindos. Antes de mandar um PR:

- Rode `dotnet test tests/Forge.Core.Tests` e confira se o build não gera avisos (eles contam como erro).
- Mantenha a regra das camadas: o `Forge.Core` não referencia WPF nem APIs só do Windows.
- Nada pode ser gravado nos arquivos de configuração do usuário (`.bashrc`, `.zshrc`, perfil do PowerShell).
- Para mudanças grandes, abra uma issue antes para conversarmos sobre a ideia.

## Licença

Distribuído sob a licença [MIT](LICENSE), pela [G3G Labs](https://www.g3glabs.com.br/). Windows, Windows Terminal, PowerShell e WSL são marcas da Microsoft; este projeto não tem vínculo com ela.
