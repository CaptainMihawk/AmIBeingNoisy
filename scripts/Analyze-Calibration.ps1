param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Arquivo não encontrado: $Path"
}

$inv = [Globalization.CultureInfo]::InvariantCulture
$cultura = [Globalization.CultureInfo]::CurrentCulture
$naoAlerta = @('fala_normal', 'fala_jogo', 'silencio_jogo', 'pausa')

try {
    $rows = @(Import-Csv -Path $Path -Delimiter ';' -Encoding UTF8 | ForEach-Object {
        [pscustomobject]@{
            ElapsedMs = [long]$_.elapsed_ms
            Label     = $_.label
            Peak      = [double]::Parse($_.peak, [Globalization.NumberStyles]::Float, $inv)
            Rms       = [double]::Parse($_.rms, [Globalization.NumberStyles]::Float, $inv)
        }
    })
}
catch {
    throw "CSV inválido: valores de peak e rms devem usar ponto como decimal (ex.: 0.123456). Erro: $($_.Exception.Message)"
}

function Get-Percentile([double[]] $Values, [double] $P) {
    $sorted = @($Values | Sort-Object)
    $rank = [int][math]::Max(0, [math]::Ceiling($P * $sorted.Count) - 1)
    return $sorted[$rank]
}

function Format-Numero($Valor) {
    if ($Valor -is [double]) { $Valor.ToString('0.0000', $cultura) } else { $Valor }
}

$titulos = @{ Peak = 'Peak (pico):'; Rms = 'RMS:' }
Write-Host "`nResumo por rótulo (janelas de 100 ms):"
foreach ($metric in 'Peak', 'Rms') {
    Write-Host $titulos[$metric]
    $rows | Group-Object Label | Sort-Object Name | ForEach-Object {
        $valores = [double[]]@($_.Group | ForEach-Object { $_.$metric })
        [pscustomobject]@{
            Rotulo  = $_.Name
            Janelas = $_.Count
            Min     = [math]::Round(($valores | Measure-Object -Minimum).Minimum, 4)
            P50     = [math]::Round((Get-Percentile $valores 0.50), 4)
            P95     = [math]::Round((Get-Percentile $valores 0.95), 4)
            P99     = [math]::Round((Get-Percentile $valores 0.99), 4)
            Max     = [math]::Round(($valores | Measure-Object -Maximum).Maximum, 4)
        }
    } | Select-Object Rotulo, Janelas,
        @{ N = 'Min'; E = { Format-Numero $_.Min } },
        @{ N = 'P50'; E = { Format-Numero $_.P50 } },
        @{ N = 'P95'; E = { Format-Numero $_.P95 } },
        @{ N = 'P99'; E = { Format-Numero $_.P99 } },
        @{ N = 'Max'; E = { Format-Numero $_.Max } } |
        Format-Table -AutoSize
}

$shoutRows = @($rows | Where-Object { $_.Label -eq 'grito' })
$shouts = @()
$current = $null
foreach ($r in $shoutRows) {
    if ($null -eq $current -or ($r.ElapsedMs - $current.FimMs) -gt 300) {
        $current = [pscustomobject]@{ InicioMs = $r.ElapsedMs; FimMs = $r.ElapsedMs; Peak = $r.Peak; Rms = $r.Rms }
        $shouts += $current
    }
    else {
        $current.FimMs = $r.ElapsedMs
        $current.Peak = [math]::Max($current.Peak, $r.Peak)
        $current.Rms = [math]::Max($current.Rms, $r.Rms)
    }
}
Write-Host "`nGritos detectados: $($shouts.Count)"
$shouts | Select-Object InicioMs, FimMs,
    @{ N = 'Peak'; E = { Format-Numero $_.Peak } },
    @{ N = 'Rms'; E = { Format-Numero $_.Rms } } |
    Format-Table -AutoSize

if ($shoutRows.Count -eq 0) {
    Write-Host "`nNenhum grito no CSV. Marque cada grito com a tecla g durante a calibração."
}
else {
    Write-Host "`nVão entre não-alerta (p99) e grito (mínimo):"
    $gaps = @(foreach ($metric in 'Peak', 'Rms') {
        $na = [double[]]@($rows | Where-Object { $naoAlerta -contains $_.Label } | ForEach-Object { $_.$metric })
        $gr = [double[]]@($rows | Where-Object { $_.Label -eq 'grito' } | ForEach-Object { $_.$metric })
        if ($na.Count -eq 0 -or $gr.Count -eq 0) { continue }
        $p99 = Get-Percentile $na 0.99
        $minGrito = ($gr | Measure-Object -Minimum).Minimum
        $vao = $minGrito - $p99
        $vaoRel = if ($minGrito -gt 0) { $vao / $minGrito } else { 0 }
        [pscustomobject]@{
            Metrica        = $metric
            NaoAlertaP99   = [math]::Round($p99, 4)
            GritoMinimo    = [math]::Round($minGrito, 4)
            Vao            = [math]::Round($vao, 4)
            VaoRelativo    = [math]::Round($vaoRel, 3)
            LimiarSugerido = $(if ($vao -gt 0) { [math]::Round($p99 + $vao / 2, 4) } else { 'sem vão' })
        }
    })

    if ($gaps.Count -eq 0) {
        Write-Host "`nDados insuficientes para calcular o vão: faltam janelas de grito ou de não-alerta."
    }
    else {
        $gaps | Select-Object Metrica,
            @{ N = 'NaoAlertaP99'; E = { Format-Numero $_.NaoAlertaP99 } },
            @{ N = 'GritoMinimo'; E = { Format-Numero $_.GritoMinimo } },
            @{ N = 'Vao'; E = { Format-Numero $_.Vao } },
            @{ N = 'VaoRelativo'; E = { Format-Numero $_.VaoRelativo } },
            @{ N = 'LimiarSugerido'; E = { Format-Numero $_.LimiarSugerido } } |
            Format-Table -AutoSize

        $positivos = @($gaps | Where-Object { $_.Vao -gt 0 } | Sort-Object VaoRelativo -Descending)
        if ($positivos.Count -gt 0) {
            $melhor = $positivos[0]
            Write-Host "`nMétrica recomendada: $($melhor.Metrica) (maior vão relativo). Limiar inicial sugerido: $(Format-Numero $melhor.LimiarSugerido)"
        }
        else {
            Write-Host "`nNenhuma métrica tem vão positivo. Não há limiar que separe só por esse critério: voltar para decisão com o usuário."
        }
    }
}

if ($shoutRows.Count -gt 0) {
    $saturados = @($shoutRows | Where-Object { $_.Peak -ge 0.99 }).Count
    Write-Host "Janelas de grito com pico >= 0.99: $saturados de $($shoutRows.Count) (muitas indicam saturação: ajustar ganho)"
}
else {
    Write-Host "Sem janelas de grito: saturação não verificada."
}
