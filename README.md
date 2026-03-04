<div align="center">

# 🔍 UserTrace

**Consulta de usuários e grupos no Active Directory**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet)
![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4?style=for-the-badge&logo=windows)
![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-0078D4?style=for-the-badge&logo=windows11)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?style=for-the-badge&logo=windows)

*Ferramenta desktop moderna com Mica Backdrop para consulta rápida de contas no Active Directory*

</div>

---

## ✨ Funcionalidades

### 👤 Busca por Login
Informe o `sAMAccountName` de um usuário e visualize instantaneamente todas as informações da conta: nome completo, status, configurações de senha, último logon, grupos locais e grupos globais.

### 🔎 Busca por Nome
Pesquise usuários pelo nome parcial. O app lista os resultados encontrados no AD e, ao selecionar um, exibe o painel completo de detalhes.

### 🏢 Busca por Grupo
Ao abrir, a lista de grupos do domínio é carregada automaticamente. Use o campo de filtro e o botão **Buscar** para restringir por nome; deixe vazio e clique em **Buscar** (ou **Limpar**) para listar todos. Selecione um grupo para ver os membros — incluindo subgrupos (resolução recursiva). Selecione um membro para ver os detalhes completos da conta.

### 🔑 Senhas Expiradas
Consulte contas com senha expirando em data específica, em intervalo de datas, expirando hoje ou **obrigadas a trocar no próximo logon** (lista em ordem alfabética). Dê **dois cliques** em um usuário para ir direto para a Busca por Login com os detalhes desse usuário.

### 🔒 Contas Bloqueadas
Lista de contas bloqueadas por tentativa incorreta de senha (lockout). A lista é carregada automaticamente ao abrir a página. Use o botão **Atualizar** para recarregar. Dê **dois cliques** em um usuário para abrir os detalhes na Busca por Login.

### 🚫 Contas Desativadas
Lista de contas de usuário desativadas no Active Directory (userAccountControl – conta desabilitada). A lista é carregada automaticamente ao abrir a página. Use o botão **Atualizar** para recarregar. Dê **dois cliques** em um usuário para abrir os detalhes na Busca por Login.

---

## 🖥️ Interface

> Fluent Design com **Mica Backdrop** no Windows 11 — transparência e profundidade reais.
> Fallback automático para cor sólida do tema no Windows 10.

- 🪟 **Janela maximizada** ao abrir — restaure ou minimize pelos botões da barra de título se preferir
- 🎨 Efeito **Mica** contínuo da barra de título ao rodapé
- 🃏 Cards visuais com badges coloridos de status
- 🏷️ Chips de grupos locais e globais
- ⚡ Operações assíncronas — UI nunca trava durante consultas ao AD
- 👆 **Dois cliques** em um usuário (Senhas Expiradas, Contas Bloqueadas ou Contas Desativadas) abre os detalhes na Busca por Login

---

## ⚙️ Requisitos

| 📋 Item | ✅ Mínimo |
|---|---|
| 🪟 Sistema Operacional | Windows 10 versão 1809 (build 17763) ou superior |
| 🏗️ Arquitetura | x64 — também disponível x86 e arm64 |
| 🌐 Domínio | Máquina ingressada no Active Directory |
| 🔑 Privilégio | Administrador local (UAC solicitado ao abrir) |

> 💡 **Windows 11** → experiência completa com efeito Mica
> 💡 **Windows 10** → funciona normalmente, sem o efeito Mica (fallback automático)

---

## 🚀 Instalação

**Não requer instalação.** Copie o `UserTrace.exe` para qualquer pasta e execute.

Todos os runtimes necessários estão embutidos no EXE:

- ✅ .NET 10 runtime
- ✅ Windows App SDK 1.8
- ✅ Visual C++ Redistributable
- ✅ WinUI 3 e WinUIEx assemblies

---

## 🔨 Build

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 x64

### Compilar

```powershell
dotnet build --configuration Release
```

### Publicar EXE standalone

```powershell
# Gera win-x64, win-x86 e win-arm64 em publish\<rid>\
.\build.ps1

# Apenas uma arquitetura
.\build.ps1 -Rid win-x64
```

O EXE gerado em `publish\win-x64\UserTrace.exe` é **self-contained**, **comprimido** (~85 MB) e não requer nenhuma dependência instalada na máquina de destino.

---

## 🛠️ Tecnologias

| 🔧 Tecnologia | 📦 Versão |
|---|---|
| .NET | 10 |
| WinUI 3 / Windows App SDK | 1.8.260209005 |
| WinUIEx | 2.9.0 |
| Mica Backdrop | Windows 11 |
| System.DirectoryServices | 9.0.2 |
| P/Invoke `netapi32.dll` | Win32 API |

---

## 🔒 Segurança

- 🛡️ **Somente leitura** — nenhuma escrita, modificação ou exclusão no AD
- 🔐 **Autenticação integrada** — usa as credenciais Windows do usuário logado (Kerberos/NTLM), sem solicitar senhas
- 💾 **Sem persistência** — nenhum dado é gravado em disco, log ou rede
- ⚠️ **UAC obrigatório** — necessário para acesso ao nível 3 da API `NetUserGetInfo`

---

<div align="center">

Feito com ❤️ usando **WinUI 3** + **Mica Backdrop**

</div>
