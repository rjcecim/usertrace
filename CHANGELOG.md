# Changelog

## 1.1.0 (Build 1)

- A expiração de senha segue `maxPwdAge` do domínio. O dashboard lê a política ao atualizar e usa uma consulta da semana para os cards de hoje e dos próximos dias.
- Senha já vencida mostra há quantos dias venceu.
- O painel do usuário vem do LDAP, sem pedir administrador. O último logon usa `lastLogonTimestamp`. O último logoff não é mais preenchido, porque o LDAP não guarda essa saída.
- Grupos do painel incluem associação aninhada e o grupo primário.
- A busca por nome também procura o login e lista contas ativas.
- As listas percorrem todas as páginas do Active Directory. O aviso de lista parcial só aparece acima de 100.000 itens.
- Contas sem setor aparecem em Busca por Setor. Contas bloqueadas mostram a data do bloqueio.
- Listas e o painel do usuário podem ser exportados em TXT, DOCX e PDF.
- Esc volta ao Dashboard quando não há um menu suspenso aberto.
- Ícones do aplicativo atualizados.
