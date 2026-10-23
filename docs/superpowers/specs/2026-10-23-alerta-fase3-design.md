# LoudEnv — Fase 3: Alerta de volume alto

**Data:** 2026-10-23
**Status:** aprovado pelo usuário (console agora; GUI no futuro)

## Objetivo

Avisar em tempo real quando o volume da fala passar do limiar calibrado, para o usuário se controlar antes de acordar alguém. O aviso é um tom curto no fone e uma linha no console. Sem GUI nesta fase.

## Comportamento

- O alerta usa o RMS da janela de 100 ms e o limiar informado na CLI.
- Dispara quando o RMS fica no limiar ou acima por 3 janelas seguidas (300 ms).
- Enquanto continuar acima, repete a cada 5 s.
- Ao cair abaixo do limiar, zera a contagem. A próxima subida acima de 3 janelas dispara de novo, sem esperar os 5 s.
- O console mostra o estado (`ACIMA`/`abaixo`) e, a cada disparo, imprime uma linha nova `ALTO <hora> rms <valor>`.

## Uso

```
LoudEnv --alert --threshold <0-1> [--device <índice>]
```

- `--alert` exige `--threshold`; sem ele, erro de uso com código 1.
- `--alert` não combina com `--calibrate`; erro de uso com código 1.
- `--threshold` continua aceitando vírgula ou ponto. O modo sem `--alert` continua sendo só teste (sem som).

## Som

- Tom senoidal de 880 Hz por 150 ms, ganho 0,25, gerado pelo NAudio e tocado no dispositivo de saída padrão (o fone).
- A reprodução é assíncrona e não bloqueia a captura.
- Se a saída falhar (sem dispositivo, driver, etc.), imprime uma única vez `Aviso: não foi possível tocar o alerta sonoro: ...` e segue medindo. O alerta visual continua funcionando.

## Argumentos e erros

- Mensagens em português, exit codes 0 (sucesso e Ctrl+C) e 1 (uso, dispositivo, arquivo).
- O parser atual continua igual para `--list`, `--device`, `--calibrate` e `--out`.

## Fora de escopo

GUI, overlay, volume do tom configurável, salvar o limiar entre execuções, limiar por horário, histórico de alertas.

## Riscos

- **Tom não ouvido:** volume do sistema baixo, saída trocada para outro dispositivo ou jogo em fullscreen com áudio exclusivo. Mitigação nesta fase: o console também mostra `ALTO`. Na fase de GUI, o aviso visual resolve.
- **Falso positivo por pico curto:** mitigado pelas 3 janelas seguidas.
- **Spam:** mitigado pela repetição a cada 5 s.
- **Ganho do microfone alterado:** o limiar fica inválido. Recalibrar.

## Testes e validação

Manual, como nas fases anteriores:

- Falar normal: nenhum tom, nenhuma linha `ALTO`, estado `abaixo`.
- Falar alto: tom + linha `ALTO` ao cruzar.
- Continuar alto: novo aviso a cada 5 s.
- Cair abaixo e subir de novo: dispara na nova subida.
- `--alert` sem `--threshold` e `--alert --calibrate`: erro em português, exit 1.
- Sem dispositivo de saída: aviso uma vez, medição continua.
