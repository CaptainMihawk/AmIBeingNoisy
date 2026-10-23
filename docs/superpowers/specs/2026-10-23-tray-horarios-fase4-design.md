# LoudEnv — Fase 4: Bandeja com horários

**Data:** 2026-10-23
**Status:** aprovado pelo usuário

## Objetivo

Transformar o LoudEnv em um aplicativo de bandeja (tray) que mede o microfone e alerta apenas dentro do horário configurado. Fora do horário, a captura para. O console continua existindo para calibração e diagnóstico.

## Decisões do usuário

- Horário inicial: todos os dias, 22:00–07:00 (atravessa a meia-noite).
- Fora do horário ativo: parar a captura e mostrar o ícone cinza.
- Configuração por uma janela no tray, salva em `%APPDATA%\LoudEnv\settings.json`.
- Threshold inicial 0,20 de RMS (ajuste fino dentro da janela).
- Iniciar com o Windows: opção no instalador (marcada por padrão) e na janela de configurações.

## Arquitetura

- Novo projeto `LoudEnv.Tray` (WinForms, `WinExe`), referenciando o projeto console `LoudEnv.csproj`. O console não muda de comportamento e continua sendo o executável principal para `--calibrate`, `--threshold` e `--alert`.
- O console exclui `LoudEnv.Tray/**/*.cs` da compilação para os dois projetos conviverem.
- O tray reutiliza `MicLevelMonitor`, `ThresholdDetector`, `AlertSound`, `AlertLog`, `InlineProgress` e `LevelReading`.
- `MicLevelMonitor` ganha resolução por nome (substring, sem diferenciar maiúsculas) e um método para o nome do dispositivo padrão. O tray guarda o nome, não o índice, para resistir à mudança de ordem dos dispositivos.

## Interface

### Ícone

Quatro estados, desenhados em código (círculo colorido):

- **Cinza:** fora do horário, pausado ou sem microfone.
- **Verde:** ativo e RMS abaixo de 75% do limiar.
- **Amarelo:** ativo e RMS entre 75% e 100% do limiar.
- **Vermelho:** ativo e RMS no limiar ou acima.

Tooltip curto (limite de 63 caracteres): `LoudEnv: ativo 0.123`, `LoudEnv: fora do horário`, `LoudEnv: pausado`, `LoudEnv: sem microfone`.

### Menu

- `Configurações...`
- `Pausar` / `Retomar`
- `Abrir log`
- `Sair`

Duplo clique abre as configurações. Na primeira execução, um balão informa o estado atual.

### Configurações

- Limiar (0,01 a 0,99, passo 0,01).
- Dispositivo de captura (lista de endpoints ativos; seleção por nome).
- `Usar horário` (checkbox). Início e fim (`DateTimePicker` de hora).
- Caminho do log (`SaveFileDialog` para escolher).
- `Som do alerta` (checkbox).
- `Salvar` / `Cancelar`.

Validações: início diferente do fim quando o horário está ativo; dispositivo selecionado quando houver (sem dispositivo, salva vazio e o tray tenta resolver o padrão); caminho de log não vazio; criar a pasta do log se necessário. Erros em português, sem fechar a janela.

## Configuração persistida

`%APPDATA%\LoudEnv\settings.json`:

```json
{
  "threshold": 0.2,
  "deviceName": "Microfone (fifine Microphone)",
  "scheduleEnabled": true,
  "start": "22:00:00",
  "end": "07:00:00",
  "logPath": "C:\\Users\\<usuario>\\AppData\\Roaming\\LoudEnv\\alertas.csv",
  "alertSound": true
}
```

- `TimeOnly` para início e fim.
- Se o arquivo não existir, usar os padrões acima; o dispositivo padrão é resolvido na primeira execução.
- Arquivo corrompido: avisar uma vez e usar os padrões; não apagar o arquivo sem aviso.

## Agenda

- Uma janela diária, com opção de desligar (sempre ativo).
- Início menor que fim: ativo no mesmo dia. Início maior que fim: atravessa a meia-noite (22:00–07:00 = 22:00–23:59 e 00:00–06:59).
- Início igual a fim: inválido quando a agenda está ligada.
- Um timer de 5 s reavalia a agenda e liga/desliga a captura. A janela de erro é de até 5 s no início/fim do horário.

## Ciclo de captura

- Ao entrar no horário (ou ao iniciar dentro dele) e não estando pausado, abre o microfone e o `AlertLog` e começa a medir.
- Ao sair do horário, pausar ou sair do app: cancela a captura, fecha o log (gravando o episódio aberto) e libera o microfone.
- Pausa manual: para a captura e deixa o ícone cinza; retomar reavalia a agenda.
- Uma instância só (`Mutex`). A segunda instância avisa e sai.
- Falha ao abrir o microfone ou o log: balão em português, ícone cinza, nova tentativa a cada 30 s (sem repetir o mesmo balão).

## Log

- Mesmo formato da Fase 3.1, em append.
- Só grava enquanto a captura está ativa dentro do horário. Episódios abertos são fechados ao parar a captura.
- `Abrir log` abre o arquivo no aplicativo padrão; se não existir, avisa.

## Publicação e atalhos

- `publicar-tray.cmd`: publica o tray em `tray/`.
- `tray.cmd`: inicia `tray\LoudEnv.Tray.exe`.
- `tray/` e `alertas*.csv` ficam fora do git.
- O console continua com `publicar.cmd` e `alerta.cmd`.

## Iniciar com o Windows

- Checkbox `Iniciar com o Windows` na janela de configurações.
- Implementado pelo valor `LoudEnv` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, apontando para o executável atual entre aspas. Sem admin e sem tarefa agendada.
- O instalador oferece a mesma opção, marcada por padrão. O desinstalador remove o valor mesmo que ele tenha sido criado depois pela janela de configurações.
- Se o executável mudar de caminho, o checkbox aparece desmarcado e pode ser religado.

## Fora de escopo

- Vários intervalos por dia, horários diferentes por dia da semana, exceções e feriados.
- Histórico/gráfico no tray, notificações do Windows.
- Alterar o threshold automaticamente.

## Riscos

- **Horário do sistema:** a agenda usa a hora local; mudanças de fuso ou horário de verão afetam a janela.
- **Ícone não visível em jogo fullscreen exclusivo:** o som continua sendo o aviso principal.
- **Dois áudios concorrentes:** o tray e o console não devem capturar o mesmo microfone ao mesmo tempo; o `Mutex` evita duas instâncias do tray, mas não impede o console aberto em paralelo. Documentar: feche o console antes de usar o tray.
- **Log aberto no Excel:** a abertura falha; o aviso em português explica e a captura não inicia, com nova tentativa a cada 30 s.

## Testes e validação

Manual:

- Tray inicia, mostra ícone e tooltip do estado.
- Dentro do horário: mede, alerta e grava no log.
- Fora do horário: ícone cinza e sem captura (verificar que o microfone não está em uso).
- Alterar o horário para incluir o momento atual e salvar: a captura começa sem reiniciar o app.
- Pausar/retomar.
- Fechar e reabrir: configurações persistidas.
- Segunda instância: aviso e saída.
- Log aberto no Excel: aviso e medição continua.
