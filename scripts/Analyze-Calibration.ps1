param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$inv = [Globalization.CultureInfo]::InvariantCulture
$naoAlerta = @('fala_normal', 'fala_jogo', 'silencio_jogo', 'pausa')

$rows = @(Import-Csv -Path $Path -Delimiter ';' -Encoding UTF8 | ForEach-Object {
    [pscustomobject]@{
        ElapsedMs = [long]$_.elapsed_ms
        Label     = $_.label
        Peak      = [double]::Parse($_.peak, $inv)
        Rms       = [double]::Parse($_.rms, $inv)
    }
})

function Get-Percentile([double[]] $Values, [double] $P) {
    $sorted = @($Values | Sort-Object)
    $rank = [int][math]::Max(0, [math]::Ceiling($P * $sorted.Count) - 1)
    return $sorted[$rank]
}

Write-Host "`nResumo por rótulo (janelas de 100 ms):"
$rows | Group-Object Label | Sort-Object Name | ForEach-Object {
    $peaks = [double[]]@($_.Group | ForEach-Object { $_.Peak })
    $rms = [double[]]@($_.Group | ForEach-Object { $_.Rms })
    [pscustomobject]@{
        Rotulo  = $_.Name
        Janelas = $_.Count
        PeakP50 = [math]::Round((Get-Percentile $peaks 0.50), 4)
        PeakP99 = [math]::Round((Get-Percentile $peaks 0.99), 4)
        PeakMax = [math]::Round(($peaks | Measure-Object -Maximum).Maximum, 4)
        RmsP50  = [math]::Round((Get-Percentile $rms 0.50), 4)
        RmsP99  = [math]::Round((Get-Percentile $rms 0.99), 4)
        RmsMax  = [math]::Round(($rms | Measure-Object -Maximum).Maximum, 4)
    }
} | Format-Table -AutoSize

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
$shouts | Format-Table -AutoSize

Write-Host "`nVão entre não-alerta (p99) e grito (mínimo):"
$gaps = foreach ($metric in 'Peak', 'Rms') {
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
}
$gaps | Format-Table -AutoSize

$positivos = @($gaps | Where-Object { $_.Vao -gt 0 } | Sort-Object VaoRelativo -Descending)
if ($positivos.Count -gt 0) {
    $melhor = $positivos[0]
    Write-Host "`nMétrica recomendada: $($melhor.Metrica) (maior vão relativo). Limiar inicial sugerido: $($melhor.LimiarSugerido)"
}
else {
    Write-Host "`nNenhuma métrica tem vão positivo. Não há limiar que separe só por esse critério: voltar para decisão com o usuário."
}

$saturados = @($shoutRows | Where-Object { $_.Peak -ge 0.99 }).Count
Write-Host "Janelas de grito com pico >= 0.99: $saturados de $($shoutRows.Count) (muitas indicam saturação: ajustar ganho)"
