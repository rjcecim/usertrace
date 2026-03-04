# UserTrace — Documentação Técnica

## Índice

1. [Visão Geral](#1-visão-geral)
2. [Estrutura de Arquivos](#2-estrutura-de-arquivos)
3. [Configuração do Projeto](#3-configuração-do-projeto)
4. [Models](#4-models)
5. [Services](#5-services)
6. [Views](#6-views)
7. [Fluxo de Dados](#7-fluxo-de-dados)
8. [Sistema Visual Mica](#8-sistema-visual-mica)
9. [Build e Publicação](#9-build-e-publicação)
10. [Segurança e Permissões](#10-segurança-e-permissões)

---

## 1. Visão Geral

O **UserTrace** é uma aplicação desktop WinUI 3 para consulta de usuários e grupos no Active Directory. Utiliza duas APIs distintas para obter informações:

- **P/Invoke (`netapi32.dll`)** — detalhes completos de um usuário específico (nível 3 da API Win32)
- **LDAP (`System.DirectoryServices`)** — busca por nome parcial e listagem/membros de grupos

A aplicação é distribuída como um único EXE self-contained (~85 MB), sem necessidade de instalação ou dependências externas.

---

## 2. Estrutura de Arquivos

```
usertrace/
├── .gitignore
├── README.md
├── DOCUMENTATION.md
└── UserTrace/                          ← pasta do projeto
    ├── UserTrace.csproj
    ├── app.manifest
    ├── build.ps1
    ├── App.xaml
    ├── App.xaml.cs
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    ├── Assets/
    │   └── app.ico
    ├── Models/
    │   ├── CommandResult.cs
    │   ├── GroupItem.cs
    │   ├── SearchResultItem.cs
    │   ├── SenhaExpiraDisplay.cs
    │   ├── SenhaExpiraItem.cs
    │   └── UserInfo.cs
    ├── Services/
    │   ├── ActiveDirectorySearchService.cs
    │   ├── GroupService.cs
    │   └── NetUserService.cs
    └── Views/
        ├── ContasBloqueadasPage.xaml / .cs
        ├── ContasDesativadasPage.xaml / .cs
        ├── GrupoPage.xaml / .cs
        ├── LoginPage.xaml / .cs
        ├── NomePage.xaml / .cs
        ├── SenhasExpiradasPage.xaml / .cs
        ├── SobrePage.xaml / .cs
        └── UserInfoPanel.xaml / .cs
```

---

## 3. Configuração do Projeto

### `UserTrace.csproj`

| Propriedade | Valor | Descrição |
|---|---|---|
| `TargetFramework` | `net10.0-windows10.0.19041.0` | .NET 10, API mínima Win10 2004 |
| `TargetPlatformMinVersion` | `10.0.17763.0` | Suporte a partir do Win10 1809 |
| `WindowsPackageType` | `None` | Projeto não empacotado (unpackaged) |
| `WindowsAppSDKSelfContained` | `true` | Windows App SDK embutido no EXE |
| `SelfContained` | `true` | .NET runtime embutido |
| `PublishSingleFile` | `true` | Saída em arquivo único |
| `EnableCompressionInSingleFile` | `true` | Compressão do EXE final |
| `AllowUnsafeBlocks` | `true` | Necessário para P/Invoke com structs |
| `ApplicationIcon` | `Assets\app.ico` | Ícone embutido no EXE |

### Dependências NuGet

| Pacote | Versão |
|---|---|
| `Microsoft.WindowsAppSDK` | 1.8.260209005 |
| `Microsoft.Windows.SDK.BuildTools` | 10.0.26100.4654 |
| `WinUIEx` | 2.9.0 |
| `System.DirectoryServices` | 9.0.2 |
| `System.DirectoryServices.AccountManagement` | 9.0.2 |

### `app.manifest`

- `requireAdministrator` — eleva o processo para administrador local (necessário para `NetUserGetInfo` e acesso ao AD)
- `PerMonitorV2` DPI awareness — renderização nítida em monitores com DPI diferente
- `longPathAware` — suporte a caminhos longos no Windows

---

## 4. Models

### `CommandResult`

Encapsula o resultado de uma consulta de usuário.

```csharp
public sealed class CommandResult
{
    public UserInfo? UserInfo { get; init; }
    public string    Error    { get; init; } = string.Empty;
    public int       ExitCode { get; init; }
    public bool      Success  => ExitCode == 0;
}
```

### `UserInfo`

Modelo estruturado com todos os atributos de um usuário do AD.

| Propriedade | Tipo | Origem |
|---|---|---|
| `SamAccountName` | `string` | `USER_INFO_3.usri3_name` |
| `FullName` | `string` | `USER_INFO_3.usri3_full_name` |
| `Comment` | `string` | `USER_INFO_3.usri3_comment` |
| `UserComment` | `string` | `USER_INFO_3.usri3_usr_comment` |
| `AccountActive` | `bool` | flag `UF_ACCOUNTDISABLE` invertida |
| `AccountExpires` | `string` | `usri3_acct_expires` formatado |
| `PasswordLastSet` | `string` | `usri3_password_age` calculado |
| `PasswordNeverExpires` | `bool` | flag `UF_DONT_EXPIRE_PASSWD` |
| `PasswordExpired` | `bool` | flag `UF_PASSWORD_EXPIRED` |
| `PasswordRequired` | `bool` | flag `UF_PASSWD_NOTREQD` invertida |
| `PasswordChangeable` | `bool` | flag `UF_PASSWD_CANT_CHANGE` invertida |
| `SmartcardRequired` | `bool` | flag `UF_SMARTCARD_REQUIRED` |
| `LastLogon` | `string` | `usri3_last_logon` formatado |
| `LastLogoff` | `string` | `usri3_last_logoff` formatado |
| `Workstations` | `string` | `usri3_workstations` |
| `LogonScript` | `string` | `usri3_script_path` |
| `ProfilePath` | `string` | `usri3_profile` |
| `HomeDirectory` | `string` | `usri3_home_dir` |
| `LocalGroups` | `IReadOnlyList<string>` | `NetUserGetLocalGroups` |
| `GlobalGroups` | `IReadOnlyList<string>` | LDAP `memberOf` |
| `Domain` | `string` | `Environment.UserDomainName` |

### `SearchResultItem`

Item de resultado para listas de usuários (busca por nome ou membros de grupo).

```csharp
public sealed class SearchResultItem
{
    public string SamAccountName { get; init; }
    public string DisplayName    { get; init; }
}
```

### `SenhaExpiraDisplay`

Item de lista para a página Senhas Expiradas (com ou sem data de expiração).

```csharp
public sealed class SenhaExpiraDisplay
{
    public string SamAccountName { get; init; }
    public string DisplayName    { get; init; }
    public string DataExpira    { get; init; }  // formatada ou vazio para "próximo logon"
}
```

### `GroupItem`

Representa um grupo do AD na lista de grupos.

```csharp
public sealed class GroupItem
{
    public string Name        { get; init; }  // cn
    public string Description { get; init; }  // description
    public string GroupType   { get; init; }  // "Global" | "Local" | "Universal"
}
```

O `GroupType` é derivado do atributo `groupType` (bitmask):
- `0x2` → Global
- `0x4` → Local
- `0x8` → Universal

---

## 5. Services

### `NetUserService`

Responsável por obter detalhes completos de um usuário via P/Invoke.

**API Win32 utilizada:**
- `NetUserGetInfo(server, username, 3, out buffer)` — retorna `USER_INFO_3` com ~40 campos
- `NetUserGetLocalGroups(server, username, 0, LG_INCLUDE_INDIRECT, ...)` — grupos locais incluindo membros indiretos
- `NetApiBufferFree(buffer)` — libera buffer alocado pelo sistema

**Fluxo:**
```
GetUserDetailsAsync(sam, ct)
  └─ Task.Run → GetUserDetailsCore
       ├─ GetDomainController()          ← resolve DC via NetGetDCName
       ├─ NetUserGetInfo(level 3)        ← dados principais
       ├─ GetLocalGroups()               ← via NetUserGetLocalGroups
       ├─ GetGlobalGroupsViaLdap()       ← via DirectorySearcher, atributo memberOf
       └─ BuildUserInfo()                ← monta objeto UserInfo
```

**Tratamento de timestamps:**
- `usri3_last_logon` e `usri3_last_logoff` são segundos desde 01/01/1970 (Unix epoch)
- `usri3_password_age` é a idade da senha em segundos; a data é calculada como `DateTime.Now - TimeSpan.FromSeconds(age)`
- `usri3_acct_expires` é segundos desde 01/01/1970; valor `0` ou `TIMEQ_FOREVER` indica sem expiração

### `ActiveDirectorySearchService`

Busca usuários por nome parcial via LDAP.

**Filtro LDAP:**
```
(&(objectClass=user)(objectCategory=person)
  (|(displayName=*termo*)(cn=*termo*)(givenName=*termo*)(sn=*termo*)))
```

**Atributos retornados:** `sAMAccountName`, `displayName`

**Limite:** 100 resultados. **Ordenação:** alfabética por `displayName` (todas as listas de contas do app seguem esse critério).

**Resolução do domínio** (em ordem de prioridade):
1. `Domain.GetComputerDomain().Name`
2. Variável de ambiente `USERDNSDOMAIN`
3. `Environment.UserDomainName`

### `GroupService`

Gerencia listagem de grupos e membros via LDAP.

#### `GetAllGroupsAsync(filterName, ct)`

Lista grupos do domínio. Equivalente a `net group /domain`.

**Filtro LDAP:**
```
(&(objectClass=group)(objectCategory=group)(cn=*filtro*))
```

**Atributos retornados:** `cn`, `description`, `groupType`

**Limite:** 2000 grupos, `PageSize` 500 (usa paginação LDAP).

#### `GetGroupMembersAsync(groupName, ct)`

Retorna todos os membros de um grupo, incluindo membros de subgrupos (resolução recursiva).

**Passo 1:** Localiza o DN do grupo pelo `cn`:
```
(&(objectClass=group)(cn=nome_exato))
```

**Passo 2:** Busca usuários com o filtro de cadeia recursiva:
```
(&(objectClass=user)(objectCategory=person)
  (memberOf:1.2.840.113556.1.4.1941:=CN=Grupo,DC=...))
```

O OID `1.2.840.113556.1.4.1941` é o `LDAP_MATCHING_RULE_IN_CHAIN` — resolve membros de grupos aninhados recursivamente no AD, sem necessidade de percorrer a árvore manualmente.

**Atributos retornados:** `sAMAccountName`, `displayName`

**Limite:** 1000 membros, ordenados por `displayName`.

#### Contas bloqueadas e desativadas (LDAP por filtro)

O serviço expõe métodos que usam o mesmo núcleo `SearchByLdapFilterAsync` com filtros específicos:

- **GetLockedOutAccountsAsync(ct)** — contas bloqueadas por lockout (`lockoutTime>=1`, excluindo contas desativadas). Filtro: `(&(objectCategory=person)(objectClass=user)(lockoutTime>=1)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))`
- **GetMustChangePasswordAtNextLogonAsync(ct)** — contas obrigadas a trocar senha no próximo logon (`pwdLastSet=0`)
- **GetDisabledAccountsAsync(ct)** — contas desativadas (bit 2 de `userAccountControl`, `ADS_UF_ACCOUNTDISABLE`). Filtro: `(&(objectCategory=person)(objectClass=user)(userAccountControl:1.2.840.113556.1.4.803:=2))`

Todos retornam `List<SearchResultItem>` com `sAMAccountName` e `displayName`, ordenados por `DisplayName` (alfabético).

---

## 6. Views

### `MainWindow`

Janela principal herdando de `WindowEx` (WinUIEx).

**Configurações:**
- Tamanho inicial: 1160 × 740 px, centralizada na tela
- **Abertura maximizada:** em `App.OnLaunched`, após `Activate()`, o `DispatcherQueue` enfileira `OverlappedPresenter.Maximize()` para que a janela abra maximizada
- `ExtendsContentIntoTitleBar = true` — conteúdo sobe para trás da barra de título
- `MicaBackdrop { Kind = MicaKind.Base }` — efeito Mica ativado
- Botões da TitleBar com fundo transparente (`ButtonBackgroundColor = Colors.Transparent`)
- Ícone: `AppWindow.SetIcon(@"Assets\app.ico")`
- `ContentFrame.Navigated`: quando a página de destino é `LoginPage` com parâmetro (login), `NavView.SelectedItem` é sincronizado para "Busca por Login"; em `NavView_SelectionChanged`, se a página atual já for a do item selecionado, não navega de novo (evita sobrescrever o parâmetro)

**Navegação:**

| Tag | Página | Ícone (Segoe MDL2 Assets) |
|---|---|---|
| `Login` | `LoginPage` | `E77B` (pessoa) |
| `Nome` | `NomePage` | `E721` (lupa) |
| `Grupo` | `GrupoPage` | `E902` (grupo) |
| `SenhasExpiradas` | `SenhasExpiradasPage` | `E121` (cadeado/senha) |
| `ContasBloqueadas` | `ContasBloqueadasPage` | `E72E` (cadeado) |
| `ContasDesativadas` | `ContasDesativadasPage` | `E711` (proibido) |
| `Sobre` | `SobrePage` | `E946` (info) |

Transição de navegação: `EntranceNavigationTransitionInfo` (desliza de baixo para cima).

---

### `LoginPage`

Busca um usuário pelo `sAMAccountName` exato.

**Componentes:**
- `LoginTextBox` — campo de entrada do login
- `BuscarButton` — dispara `ExecutarBuscaAsync`
- `LimparButton` — limpa estado
- `ProgressRing` — exibido durante a consulta
- `InfoPanel` (`UserInfoPanel`) — exibe resultado

**Navegação com parâmetro:** quando a página é aberta via `Frame.Navigate(typeof(LoginPage), login)` (ex.: duplo clique em Senhas Expiradas, Contas Bloqueadas ou Contas Desativadas), `OnNavigatedTo` recebe o login, preenche `LoginTextBox` e dispara `ExecutarBuscaAsync()` — o usuário cai direto nos detalhes, sem precisar clicar em Buscar.

**Fluxo:**
```
OnNavigatedTo(Parameter = login) → LoginTextBox.Text = login; ExecutarBuscaAsync()
BuscarButton_Click / Enter
  └─ ExecutarBuscaAsync
       ├─ SetLoading(true)
       ├─ NetUserService.GetUserDetailsAsync(login, ct)
       ├─ result.Success → InfoPanel.ShowUser(result.UserInfo)
       └─ !result.Success → InfoPanel.ShowEmpty(result.Error)
```

---

### `NomePage`

Busca usuários por nome parcial com layout de 2 colunas.

**Componentes:**
- `NomeTextBox` — campo de entrada do termo
- `BuscarButton` / `LimparButton`
- `ResultadoListView` — lista de `SearchResultItem` com avatar circular e login
- `ContadorTextBlock` — exibe quantidade de resultados
- `DetalhesPanel` (`UserInfoPanel`) — detalhes do usuário selecionado

**Fluxo:**
```
BuscarButton_Click / Enter
  └─ ExecutarBuscaAsync
       └─ ActiveDirectorySearchService.SearchByNameAsync(termo, ct)
            └─ ResultadoListView.ItemsSource = items

ResultadoListView_SelectionChanged
  └─ NetUserService.GetUserDetailsAsync(item.SamAccountName, ct)
       └─ DetalhesPanel.ShowUser(result.UserInfo)
```

---

### `GrupoPage`

Busca grupos do domínio e membros, com layout de 3 colunas.

**Comportamento:** ao abrir a página (`Loaded`), `ListarGruposAsync()` é chamado automaticamente com filtro vazio — a lista de grupos já aparece. O botão principal é **Buscar**: com filtro preenchido, restringe por nome; com campo vazio, lista todos. **Limpar** zera o filtro e dispara novamente `ListarGruposAsync()` para recarregar a lista completa.

**Componentes:**
- `FiltroTextBox` — filtro opcional por nome de grupo (vazio = todos)
- `ListarButton` (texto "Buscar") / `LimparButton`
- `GruposListView` — lista de `GroupItem` com ícone, nome, tipo e descrição
- `GruposContadorText` — quantidade de grupos
- `MembrosListView` — lista de `SearchResultItem` com avatar e login
- `MembrosContadorText` / `GrupoSelecionadoText` — cabeçalho dinâmico
- `DetalhesPanel` (`UserInfoPanel`) — detalhes do membro selecionado

**Fluxo:**
```
Loaded → ListarGruposAsync()  (carregamento inicial)
ListarButton_Click / Enter
  └─ ListarGruposAsync
       └─ GroupService.GetAllGroupsAsync(filtro, ct)
            └─ GruposListView.ItemsSource = grupos

LimparButton_Click → limpa filtro e listas; ListarGruposAsync() (recarrega todos)

GruposListView_SelectionChanged
  └─ CarregarMembrosAsync(grupo)
       └─ GroupService.GetGroupMembersAsync(grupo.Name, ct)
            └─ MembrosListView.ItemsSource = membros

MembrosListView_SelectionChanged
  └─ CarregarDetalhesAsync(membro)
       └─ NetUserService.GetUserDetailsAsync(membro.SamAccountName, ct)
            └─ DetalhesPanel.ShowUser(result.UserInfo)
```

---

### `SenhasExpiradasPage`

Lista contas com senha expirando (política 180 dias) ou obrigadas a trocar no próximo logon.

**Tipos de busca (ComboBox):** Expirando em data específica, Expirando em intervalo de datas, Expirando hoje, Obrigado a trocar no próximo logon. Todas as listas são ordenadas alfabeticamente por `DisplayName`.

**Componentes:**
- `TipoBuscaCombo`, `DataInicioPicker` / `DataFimPicker` ou `DataEspecificaPicker` conforme o tipo
- `BuscarButton` / `LimparButton`
- `ResultadoListView` — lista de `SenhaExpiraDisplay` (nome, login, data de expiração)

**Duplo clique:** `ResultadoListView_DoubleTapped` → se o item é `SenhaExpiraDisplay`, navega para `LoginPage` com `SamAccountName`; a Busca por Login preenche o campo e executa a busca, exibindo os detalhes do usuário.

**Fluxo:** `BuscarButton_Click` → `ExecutarBuscaAsync()` conforme o tag do ComboBox; chama `GetPasswordExpiringInRangeAsync`, `GetPasswordExpiringOnDateAsync`, `GetPasswordExpiringTodayAsync` ou `GetMustChangePasswordAtNextLogonAsync` do `ActiveDirectorySearchService`.

---

### `ContasBloqueadasPage`

Lista contas bloqueadas por tentativa incorreta de senha (lockoutTime ≥ 1), em ordem alfabética por DisplayName.

**Comportamento:** ao abrir a página (`Loaded`), `ExecutarBuscaAsync()` é chamado e a lista é carregada automaticamente. O botão **Atualizar** (ícone E72C – Sync, estilo `AccentButtonStyle`) recarrega a lista.

**Componentes:**
- `AtualizarButton` — recarrega a lista (chama `ExecutarBuscaAsync`)
- `ResultadoListView` — lista de `SearchResultItem` (avatar, DisplayName, SamAccountName)
- `ContadorTextBlock` — quantidade de contas bloqueadas

**Duplo clique:** `ResultadoListView_DoubleTapped` → navega para `LoginPage` com `SamAccountName`; a Busca por Login preenche e executa a busca, exibindo os detalhes do usuário.

**Fluxo:** `Loaded` ou `AtualizarButton_Click` → `ExecutarBuscaAsync()` → `ActiveDirectorySearchService.GetLockedOutAccountsAsync(ct)`.

---

### `ContasDesativadasPage`

Lista contas de usuário desativadas no Active Directory (userAccountControl – bit 2, conta desabilitada), em ordem alfabética por DisplayName.

**Comportamento:** ao abrir a página (`Loaded`), `ExecutarBuscaAsync()` é chamado e a lista é carregada automaticamente. O botão **Atualizar** (ícone E72C – Sync, estilo `AccentButtonStyle`) recarrega a lista.

**Componentes:**
- `AtualizarButton` — recarrega a lista (chama `ExecutarBuscaAsync`)
- `ResultadoListView` — lista de `SearchResultItem` (avatar, DisplayName, SamAccountName)
- `ContadorTextBlock` — quantidade de contas desativadas

**Duplo clique:** `ResultadoListView_DoubleTapped` → navega para `LoginPage` com `SamAccountName`.

**Fluxo:** `Loaded` ou `AtualizarButton_Click` → `ExecutarBuscaAsync()` → `ActiveDirectorySearchService.GetDisabledAccountsAsync(ct)`.

---

### `UserInfoPanel`

UserControl reutilizável que exibe os dados de um `UserInfo` em cards visuais.

**Estados:**
- `ShowEmpty(message)` — exibe ícone + mensagem de placeholder
- `ShowUser(UserInfo u)` — popula e exibe todos os cards

**Cards exibidos:**

| Card | Conteúdo |
|---|---|
| Identidade | Avatar com inicial, `FullName`, `SamAccountName`, `Domain`, `Comment`, `UserComment` |
| Status da Conta | Badges coloridos: Conta Ativa/Inativa, Conta Expirada, Senha Expirada, Smartcard |
| Senha | `PasswordLastSet`, badges: Nunca Expira, Expirada, Alterável, Obrigatória |
| Logon | `LastLogon`, `LastLogoff`, Estações de Trabalho, Script de Logon, Perfil, Diretório Home |
| Grupos Locais | Chips com nome de cada grupo local |
| Grupos Globais | Chips com nome de cada grupo global (via LDAP `memberOf`) |

**Método `SetBadge`:**

```csharp
private void SetBadge(Border badge, FontIcon icon, TextBlock label,
    bool value, string trueText, string falseText, bool positiveGood)
```

- `positiveGood = true`: verde quando `value = true`, vermelho quando `false`
- `positiveGood = false`: vermelho quando `value = true`, verde quando `false`
- Usa `SystemFillColorSuccessBrush` (verde) e `SystemFillColorCriticalBrush` (vermelho)

---

### `SobrePage`

Página estática com informações do aplicativo, organizada em cards Mica:

- **O que é o UserTrace** — descrição e propósito
- **Como usar** — instruções de Busca por Login, Busca por Nome, Busca por Grupo, Senhas Expiradas, Contas Bloqueadas, Contas Desativadas e duplo clique para detalhes
- **Requisitos** — SO, domínio, privilégios, conectividade, Mica
- **Tecnologias** — chips com cada tecnologia utilizada
- **Compatível com EDR corporativo** — nota sobre ausência de processos filho e uso direto de APIs

---

## 7. Fluxo de Dados

```
Usuário digita login/nome/grupo
        │
        ▼
   Page (View)
        │  chama serviço assíncrono
        ▼
   Service (Task.Run)
        │
        ├─ NetUserService ──── P/Invoke ──► netapi32.dll ──► Domain Controller
        │                                                          │
        │                                                     USER_INFO_3
        │                                                     LocalGroups
        │
        ├─ ActiveDirectorySearchService ── LDAP ──► AD (busca por nome)
        │
        └─ GroupService ────────────────── LDAP ──► AD (grupos e membros)
                │
                ▼
           UserInfo / List<SearchResultItem> / List<GroupItem>
                │
                ▼
        UserInfoPanel.ShowUser(userInfo)
        ListView.ItemsSource = items
```

---

## 8. Sistema Visual Mica

O app implementa o sistema de camadas Mica do Fluent Design:

| Camada | Elemento | Recurso |
|---|---|---|
| Layer 0 | Janela raiz | `Background="Transparent"` + `MicaBackdrop` |
| Layer 1 | Painéis de conteúdo | `MicaContentPanelStyle` (`LayerFillColorDefaultBrush`) |
| Layer 2 | Cards elevados | `MicaCardStyle` (`CardBackgroundFillColorDefaultBrush`) |

**Boas práticas aplicadas:**

1. `Background="Transparent"` em todos os `Grid` e `Frame` raiz
2. `ExtendsContentIntoTitleBar = true` — Mica contínuo da TitleBar ao rodapé
3. `ButtonBackgroundColor = Colors.Transparent` — botões da TitleBar sem fundo opaco
4. `MicaKind.Base` na janela principal
5. `EntranceNavigationTransitionInfo` nas transições de página
6. `IsTitleBarAutoPaddingEnabled="False"` — controle manual do padding
7. `NavigationView.PaneHeader` customizado com logo integrado ao Mica

**Fallback automático:** em Windows 10 (sem suporte a Mica), o `MicaController.IsSupported()` retorna `false` e o WinUI usa a cor sólida do tema — o app funciona normalmente sem nenhum código adicional.

---

## 9. Build e Publicação

### `build.ps1`

Script PowerShell que publica o app como EXE standalone.

**Parâmetros:**
- `-Rid` (opcional): runtime identifier alvo. Padrão: todos (`win-x64`, `win-x86`, `win-arm64`)

**Para cada RID:**
1. Remove a pasta de saída anterior (`publish\<rid>\`)
2. Executa `dotnet publish` com os parâmetros:
   - `--self-contained true`
   - `-p:PublishSingleFile=true`
   - `-p:EnableCompressionInSingleFile=true`
3. Exibe o tamanho do EXE gerado

**Uso:**
```powershell
.\build.ps1              # gera win-x64 + win-x86 + win-arm64
.\build.ps1 -Rid win-x64 # apenas x64
```

**Saída:** `publish\win-x64\UserTrace.exe` (~85 MB)

### O que está embutido no EXE

- .NET 10 runtime completo
- Windows App SDK 1.8
- Visual C++ Redistributable
- WinUI 3 e WinUIEx assemblies
- Ícone (`app.ico`)

---

## 10. Segurança e Permissões

**UAC (`requireAdministrator`):** O app solicita elevação ao iniciar. Necessário para:
- `NetUserGetInfo` (level 3) — requer privilégios de administrador de domínio ou local
- `NetUserGetLocalGroups` com flag `LG_INCLUDE_INDIRECT`
- Acesso LDAP para leitura de atributos protegidos

**Escopo de leitura:** O app realiza apenas operações de **leitura** no AD. Nenhuma escrita, modificação ou exclusão é realizada.

**Credenciais:** O app usa as credenciais do usuário Windows logado (autenticação integrada Kerberos/NTLM). Nenhuma senha é solicitada ou armazenada.

**Dados em memória:** As informações consultadas existem apenas em memória durante a sessão. Nenhum dado é gravado em disco, log ou rede além das chamadas ao DC.
