# LoudEnv — Fase 1: Esqueleto do medidor de nível do microfone

**Data:** 2026-10-22
**Status:** aguardando revisão

## Objetivo

Aplicação console .NET 8 que exibe continuamente o nível do microfone padrão (valor 0.0–1.0) usando o `AudioMeterInformation.MasterPeakValue` do WASAPI via NAudio. É o esqueleto do medidor que sustenta as fases seguintes do projeto.

## Critério de pronto

O valor sobe quando o usuário fala e desce no silêncio. Se ficar travado ou zerado, o problema é o dispositivo selecionado (headset USB vs mic onboard) — resolver a seleção de dispositivo antes de seguir para a Fase 2.

## Ambiente

- Windows; o projeto nasce na raiz do workspace (diretório vazio hoje).
- .NET 8 SDK instalado via winget (`Microsoft.DotNet.SDK.8`) — aprovado pelo usuário.
- Target framework `net8.0-windows`, pois NAudio usa Core Audio (WASAPI), exclusivo do Windows.

## Arquitetura e componentes

### Projeto console `LoudEnv`

- `net8.0-windows`, pacote NuGet `NAudio` (versão estável mais recente da linha 2.x).
- Arquivos: [MicLevelMonitor.cs](../../MicLevelMonitor.cs) e [Program.cs](../../Program.cs).

### `MicLevelMonitor`

Classe que encapsula a descoberta do endpoint e o polling do nível.

- Resolve o endpoint de captura padrão com `MMDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)` e expõe `DeviceName` para diagnóstico.
- Construtor/fábrica alternativo aceita índice explícito de endpoint (usado por `--device`), validando o índice contra a lista de captura disponível.
- `RunAsync(IProgress<float> level, CancellationToken ct)`: abre um stream de captura `WasapiCapture` no endpoint escolhido e calcula o pico de cada buffer recebido em `DataAvailable`; o loop assíncrono reporta o pico da janela de 100 ms (`Task.Delay(100, ct)`) via `IProgress<float>` (0.0–1.0). A leitura fica separada da renderização, o que deixa a porta aberta para testes automatizados nas fases seguintes.
- `static IReadOnlyList<(int Index, string Name)> ListCaptureEndpoints()` para o modo diagnóstico.
- Implementa `IDisposable`, liberando `MMDevice` e `MMDeviceEnumerator`.

### `Program`

- Sem argumentos: imprime o dispositivo escolhido no startup, inicia o monitor e renderiza cada leitura numa única linha atualizada in-place (`\r`): valor com 3 casas decimais + barra ASCII de 20 colunas (`#` para o nível, `-` para o restante).
- `--list`: enumera os endpoints de captura (índice + nome) e encerra com exit code 0.
- `--device <índice>`: usa o endpoint de captura correspondente em vez do padrão.
- Ctrl+C: `Console.CancelKeyPress` com `e.Cancel = true` + `CancellationTokenSource.Cancel()`; saída limpa com exit code 0.

## Fluxo de dados

Endpoint de captura (padrão ou `--device`) → stream `WasapiCapture` (`DataAvailable`) → pico por buffer → pico da janela de 100 ms → `IProgress<float>` → renderização no console.

## Tratamento de erros

- Nenhum dispositivo de captura disponível: mensagem clara na resolução do endpoint + exit code 1.
- Índice inválido em `--device`: mensagem listando os índices válidos + exit code 1.
- `--list` sem dispositivos: informa lista vazia + exit code 0.
- Cancelamento via Ctrl+C não é erro: exit code 0.

## Testes

Validação manual pelo critério de pronto: falar e calar enquanto se observa o valor no console, usando hardware real. Sem testes unitários nesta fase — o polling de `MasterPeakValue` depende de dispositivo de áudio. O limite entre leitura (`MicLevelMonitor`) e renderização (`Program`) existe justamente para permitir testes automatizados nas fases seguintes.

## Emenda — 2026-10-22: mecanismo de medição

**Motivo:** o mecanismo original (polling de `AudioMeterInformation.MasterPeakValue`) provou-se cego para o caso de uso da Fase 1. Evidência: (a) a documentação da API `IAudioMeterInformation` (Microsoft Learn) — sem peak meter de hardware, o meter em software só funciona no caminho do engine e "em exclusive mode um software peak meter sempre reporta 0.0"; `QueryHardwareSupport` dos endpoints de teste retornou apenas `Volume, Mute` (sem meter de hardware); (b) as issues do NAudio #160, #347 e #1188 relatam o mesmo sintoma (meter zerado em microfone, sem sessão ativa ou em device não-default); (c) a resposta do mantenedor do NAudio (markheath, jun/2025) na issue #1188: `MasterPeakValue` só reflete uma sessão ativa e na prática rastreia o device default — o caminho robusto é capturar do device e calcular o nível dos samples (no NAudio 2: `WasapiCapture(device)` + pico no `DataAvailable`).

**Decisão (aprovada pelo usuário):** o nível passa a ser o pico dos samples capturados do endpoint escolhido, com a mesma cadência de 100 ms e a mesma faixa 0.0–1.0. O critério de pronto e o restante da spec permanecem inalterados. Benefício adicional: `--device` passa a medir corretamente qualquer endpoint, não apenas o default.

## Fora de escopo

Alertas/limites de volume, GUI, configuração persistente, gravação de áudio, seleção de dispositivo por nome (apenas por índice nesta fase), notificações do sistema.

## Riscos

- O endpoint padrão pode apontar para o mic errado (headset USB vs mic onboard). Mitigação dentro da Fase 1: diagnosticar com `--list` e apontar o correto com `--device <índice>`.
- NAudio funciona apenas no Windows — aceito, pois o projeto é Windows-only por definição.
