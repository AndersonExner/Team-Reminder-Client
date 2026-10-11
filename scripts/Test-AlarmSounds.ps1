param()
# PowerShell 7.4+, Debug/x64. Tests actual Windows codecs using an isolated database.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoPath 'src/clients/TeamReminder.Windows/bin/x64/Debug/net10.0-windows10.0.19041.0/win-x64/TeamReminder.Windows.exe'
if (-not (Test-Path -LiteralPath $exePath)) { throw 'Compile Debug/x64 antes de executar.' }
if (Get-Process -Name TeamReminder.Windows -ErrorAction SilentlyContinue) { throw 'Encerre o app pelo menu Sair antes de testar.' }
$testPath = Join-Path $repoPath ('artifacts/audio-smoke/' + [Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $testPath -Force | Out-Null
$reportPath = Join-Path $testPath 'report.json'
$process = Start-Process -FilePath $exePath -ArgumentList '--tray' -PassThru -WindowStyle Hidden -Environment @{
    TEAMREMINDER_TEST_DATA_FILE = (Join-Path $testPath 'alarms.db')
    TEAMREMINDER_TEST_AUDIO_REPORT = $reportPath
}
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(55)
    while (-not (Test-Path -LiteralPath $reportPath)) {
        $process.Refresh()
        if ($process.HasExited) { throw 'App encerrou antes de verificar áudio.' }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Verificação de áudio excedeu o prazo.' }
        Start-Sleep -Milliseconds 200
    }
    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if (-not $report.Success) { throw $report.Error }
    $report.Checks | Write-Output
    Write-Output ('Relatório: ' + $reportPath)
} finally {
    $process.Refresh()
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
