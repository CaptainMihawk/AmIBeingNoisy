# LoudEnv

Medidor de volume do microfone com alerta sonoro e ícone na bandeja. Fora do horário configurado, a captura para.

## Instalar

Baixe o instalador mais recente em **Releases** (`LoudEnv-Setup-x.y.z.exe`). Ele instala em `%LOCALAPPDATA%\LoudEnv`, cria o atalho no menu Iniciar e pode iniciar o app ao final.

Para desenvolvimento, `publicar-tray.cmd` gera um executável portátil em `tray/`; ele não é publicado nos Releases.

## Uso

- O ícone fica na bandeja. Verde: abaixo de 75% do limiar. Amarelo: entre 75% e 100%. Vermelho: no limiar ou acima. Cinza: fora do horário, pausado ou sem microfone.
- Duplo clique (ou `Configurações...`) abre limiar, dispositivo, horário, log, som e a opção de iniciar com o Windows.
- Padrão: todos os dias, 22:00–07:00, limiar 0,20 de RMS, log em `%APPDATA%\LoudEnv\alertas.csv`.
- Fora do horário a captura para. O log grava episódios que geraram aviso.
- Configurações em `%APPDATA%\LoudEnv\settings.json`; são preservadas em atualizações.
- Feche o app de console antes de usar o tray: os dois não devem capturar o mesmo microfone ao mesmo tempo.

## Desenvolvimento

```powershell
dotnet build
dotnet build LoudEnv.Tray\LoudEnv.Tray.csproj
publicar-tray.cmd
tray.cmd
```

O console continua disponível para calibração e diagnóstico:

```powershell
dotnet run -- --list
dotnet run -- --calibrate --device 1 --out calibracao.csv
pwsh -File scripts/Analyze-Calibration.ps1 -Path calibracao.csv
dotnet run -- --alert --threshold 0.20 --device 1
```

## Release

O workflow `.github/workflows/build.yml` roda em pushes para `main`, pull requests e tags `v*`. Ele publica o executável self-contained, gera o instalador com Inno Setup e envia o instalador como artefato. Em tags, também cria a Release somente com o instalador.

Para gerar uma versão:

```powershell
git tag v1.0.0
git push origin v1.0.0
```
