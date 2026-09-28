# Changelog

Uma seção por versão. Correções, ajustes, refatorações e mudanças internas incrementam só o Build.

## 1.6.0 (Build 2)

- A expiração de senha segue `maxPwdAge` do domínio. O dashboard lê a política ao atualizar e usa uma consulta da semana para os cards de hoje e dos próximos dias.
- Senha já vencida mostra há quantos dias venceu.
- O painel do usuário vem do LDAP, sem pedir administrador. O último logon usa `lastLogonTimestamp`. O último logoff não é mais preenchido, porque o LDAP não guarda essa saída.
- Grupos do painel incluem associação aninhada e o grupo primário.
- A busca por nome também procura o login e lista contas ativas.
- As listas percorrem todas as páginas do Active Directory. O aviso de lista parcial só aparece acima de 100.000 itens.

## 1.5.0 (Build 3)

- Listas e o painel do usuário podem ser exportados em TXT, DOCX e PDF.
- Esc volta ao Dashboard quando não há um menu suspenso aberto.
- Ícones do aplicativo atualizados.

## 1.4.0 (Build 8)

- Os indicadores do dashboard e as colunas do gráfico abrem a lista correspondente.
- O painel mostra a unidade organizacional, no caminho até a OU Tribunal.
- Busca por Setor lista contas ativas sem setor preenchido.
- Contas bloqueadas mostram a data do bloqueio.
- Documentação do dashboard, da cópia de texto e da navegação.

## 1.3.0 (Build 2)

- Busca de usuários por setor.
- Setores que diferem só pelo acento permanecem distintos.

## 1.2.0 (Build 6)

- O painel do usuário mostra e-mail, ramal, setor, tentativas de senha inválida, expiração calculada e data de bloqueio.
- A solução volta a se chamar UserTrace depois de um rename interno para UserTracev2.
- Documentação alinhada a esses campos.

## 1.1.0 (Build 1)

- Dashboard com indicadores e gráfico de senhas a expirar.

## 1.0.0 (Build 5)

- Consulta de usuários e grupos no Active Directory: login, nome, grupo, senhas expiradas, contas bloqueadas e contas desativadas.
- Texto das páginas pode ser copiado. No painel, a cópia fica restrita aos valores dos campos.
- Contagem das listas e navegação por duplo clique centralizadas.
- Remoção do script de build.
