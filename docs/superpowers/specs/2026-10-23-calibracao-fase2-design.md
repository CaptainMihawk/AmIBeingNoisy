# LoudEnv — Fase 2: Calibração do limiar de grito

**Data:** 2026-10-23
**Status:** aguardando revisão

## Objetivo

Adicionar o modo `--calibrate`, que registra em CSV, a cada 100 ms, o pico e o RMS do microfone junto com o rótulo do que estava acontecendo. Uma sessão de calibração real, com essas faixas separadas, define o limiar inicial do alerta (Fase 3). Esta fase não alerta: só registra e analisa.

## Definições

- **Grito:** volume que acordaria alguém no quarto ao lado, à noite, em silêncio. À tarde não conta.
- **Setup:** microfone fifine, fones (sem caixa de som). É o mesmo setup do uso diário.
- **Limiar único:** calibrado para a noite. Limiar diferente de dia fica para a Fase 3.

## Critério de pronto

Existe um limiar inicial com número escolhido a partir dos dados da sessão, justificado pelo vão entre as classes (ou pela constatação de que não há vão, com o próximo passo definido). O número e o vão são registrados em `.superpowers/sdd/progress.md`.

## Pré-requisitos do ambiente

Antes da sessão oficial:

- Desativar no Windows e no driver do fifine: aprimoramentos, controle automático de ganho (AGC) e supressão de ruído.
- Verificar se o Voicemeeter está fora do caminho do áudio. Ele aparece na lista de apps que usaram o microfone.
- Ajustar o ganho só até os gritos pararem de bater em 1,000 e a fala normal ficar com folga acima do piso de ruído. Anotar o valor de ganho usado.
- Manter a mesma posição do microfone (distância e ângulo) em todos os blocos.
- Fazer a sessão à noite, no ambiente em que o alerta vai valer.

## Uso

```
LoudEnv --calibrate [--device <índice>] [--out <caminho.csv>]
```

- `--out` padrão: `calibracao-yyyyMMdd-HHmmss.csv` no diretório atual.
- Teclas durante a sessão:
  - `1` fala normal
  - `2` fala de jogo
  - `3` silêncio com jogo
  - `4` pausa (silêncio sem jogo). É o rótulo inicial.
  - `g` grito: as 2 s seguintes são rotuladas `grito`, depois volta ao rótulo de segmento atual.
- O console mostra o rótulo atual e a barra de pico, como na Fase 1.
- Ctrl+C encerra com `Encerrado.`, fecha o arquivo e sai com código 0.

## Protocolo da sessão

Ordem sugerida:

1. **Fala normal**, 5 min: conversa ou leitura em voz alta. Tecla `1`.
2. **Fala de jogo**, 5 min: jogo tocando nos fones, reações e comemoração moderada. Tecla `2`.
3. **Silêncio com jogo**, 2 min: jogo em volume normal, sem falar. Tecla `3`. É o controle de vazamento.
4. **Gritos**, 10 gritos no nível que acordaria alguém à noite. Pressionar `g` no início de cada um, com pausa de cerca de 20 s entre eles (tecla `4`).

Duração total: cerca de 20 min.

## Formato do CSV

- Separador `;`, decimal `.`, codificação UTF-8, com cabeçalho:

```
timestamp;elapsed_ms;device;label;peak;rms
```

- `timestamp`: data e hora local, ISO 8601 com milissegundos.
- `elapsed_ms`: milissegundos desde o início da sessão.
- `device`: nome do endpoint de captura.
- `label`: `fala_normal`, `fala_jogo`, `silencio_jogo`, `pausa` ou `grito`.
- `peak`, `rms`: valores lineares sem clamp, com 6 casas. Valores próximos de 1,000 indicam saturação.
- Uma linha por janela de 100 ms, cerca de 10 linhas por segundo.
- `peak` é o maior valor absoluto da janela. `rms` é a raiz da média dos quadrados de todas as amostras da janela.

## Componentes

- **`MicLevelMonitor`:** `RunAsync` passa a reportar um tipo com `Peak` e `Rms` por janela, em vez de um `float`. A Fase 1 continua exibindo só o `Peak`.
- **`CalibrationLabeler`** (novo): mapeia teclas para rótulos e aplica a janela de 2 s do grito. Não depende de áudio, então pode ser testado isoladamente. Acesso com `lock`, porque a tecla e o callback de leitura rodam em threads diferentes.
- **`CalibrationLog`** (novo): escreve o cabeçalho e as linhas do CSV, e fecha o arquivo no `Dispose`.
- **`Program`:** trata `--calibrate` e `--out`. `ParseDeviceIndex` passa a aceitar combinações de `--device`, `--calibrate` e `--out`. As teclas são lidas num loop em background com `Console.KeyAvailable` e `Console.ReadKey(true)`, sem bloquear o `RunAsync`.

## Análise pós-sessão

Feita por script (PowerShell ou Python) sobre o CSV. A saída tem:

- Tabela por rótulo com mínimo, p50, p95, p99 e máximo de `peak` e de `rms`.
- Os picos individuais dos 10 gritos.
- Para cada métrica: o vão entre o p99 das classes que não alertam (`fala_normal`, `fala_jogo`, `silencio_jogo`, `pausa`) e o mínimo dos gritos.
- Limiar proposto: ponto médio do vão da métrica escolhida, quando o vão é positivo.

Regras:

- Usar p99, e não o máximo, das classes que não alertam. Assim um clique isolado não define o limiar. O máximo fica na tabela para conferência.
- Escolher a métrica (`peak` ou `rms`) com o maior vão relativo.
- Se o vão for zero ou negativo, o limiar sozinho não separa. Nesse caso, o próximo passo volta para decisão com o usuário: tempo mínimo acima do limiar (por exemplo, 3 janelas seguidas) ou revisão do setup.
- Se os gritos baterem repetidamente em 1,000, ajustar o ganho e repetir só o bloco de gritos.

## Emenda — 2026-10-23: alerta de volume alto, não só grito

**Motivo:** na sessão `calibracao-noite-01.csv`, a fala que o usuário considera capaz de acordar alguém mediu 0,20 de RMS, abaixo da fala de jogo (até 0,405). O usuário decidiu que comemoração de jogo também deve alertar: o objetivo é ter consciência do volume e se controlar, não detectar exclusivamente grito.

**Decisão (aprovada pelo usuário):**

- O evento deixa de ser `grito` e passa a ser `volume alto`. No modo de teste, a linha do evento é `ALTO`.
- Limiar provisório: **RMS ≥ 0,20 por 3 janelas seguidas (300 ms)**. Não há tempo mínimo maior nesta fase; reações curtas de jogo entram de propósito.
- Na sessão gravada, esse limiar gera 0 eventos de fala normal, 0 de silêncio com jogo, 1 de fala de jogo e 8 de gritos/rajadas. A comemoração de jogo entra por decisão do usuário.
- O modo de teste é `--threshold 0,20` (aceita vírgula ou ponto).
- O número é provisório: depende do ganho do microfone e não veio da sessão completa de 10 gritos. Se o ganho mudar, recalibrar. A sessão completa continua recomendada antes da Fase 3.
- O critério de pronto da Fase 2 passa a ser este limiar provisório registrado com os números acima, mais a pendência da sessão completa anotada.

## Fora de escopo

Alerta sonoro ou visual, tempo mínimo acima do limiar (Fase 3), limiares diferentes para dia e noite, persistência de configuração, interface gráfica.

## Riscos

- **Processamento do Windows ou do driver comprimindo a dinâmica.** Grito e fala ficam parecidos. Mitigação: pré-requisito de desativar o processamento.
- **Sessão em horário diferente do uso real.** Mitigação: fazer a sessão à noite.
- **Só 10 gritos.** O mínimo dos gritos é um ponto só e pode ser frágil. Mitigação: olhar os 10 valores; se o vão for pequeno, repetir o bloco de gritos em outra noite.
- **Clique de teclado ou batida na mesa elevando o pico.** Mitigação: o RMS fica registrado e entra na análise.

## Testes e validação

Sem testes automatizados nesta fase, como na Fase 1. A validação é a própria sessão mais a análise, e cobre:

- `--calibrate` cria o arquivo com o cabeçalho esperado.
- As teclas trocam o rótulo, e `g` marca exatamente 2 s como `grito`.
- Ctrl+C fecha o arquivo e sai com código 0.
- `--out` é respeitado.
- Os valores de `peak` e `rms` batem com o que a barra mostra durante a sessão.
