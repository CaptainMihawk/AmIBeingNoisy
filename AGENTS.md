# Convenções do projeto

## Commits: Conventional Commits

Preferência a partir de 2026-10-23: mensagens de commit seguem Conventional Commits.

Formato:

```
<tipo>(<escopo opcional>): <descrição>
```

Tipos usados: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `chore`.

Regras:

- Descrição no imperativo, curta, sem ponto final. Use inglês para acompanhar o histórico existente; o corpo e o rodapé podem ficar em português.
- Corpo opcional depois de uma linha em branco, explicando o porquê da mudança.
- Rodapé opcional para trailers (por exemplo `Co-authored-by`), separado por uma linha em branco.
- Vale para commits novos. Não reescreva o histórico existente só para aplicar o padrão.

Exemplos:

- `feat: add --log to the alert mode`
- `fix: flush the open episode on Ctrl+C`
- `docs: add commit conventions`
- `refactor(level-window): ignore non-finite samples`

## Outras convenções

- Mensagens de console e erros em português. Exit codes: `0` para sucesso e Ctrl+C; `1` para erro.
- Target `net8.0-windows`, NAudio 2.4.0.
- Validação manual nas fases atuais; sem projeto de testes automatizados.
- Não commitar: `.superpowers/`, `dist/`, `alertas*.csv`, `calibracao-*.csv`.
