# Requires PowerShell 7.4+ (Start-Process -Environment) and a Debug/x64 build.
# Uses an isolated database; never reads or changes the user's alarm database.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repoPath 'src/clients/TeamReminder.Windows/bin/x64/Debug/net10.0-windows10.0.19041.0/win-x64'
$exePath = Join-Path $outputPath 'TeamReminder.Windows.exe'
if (-not (Test-Path -LiteralPath $exePath)) { throw 'Compile Debug/x64 antes de executar este teste.' }
$existing = Get-Process -Name TeamReminder.Windows -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath }
if ($existing) { throw 'Feche o aplicativo antes de executar este teste isolado.' }

$testPath = Join-Path $repoPath ('artifacts/smoke/' + [Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $testPath -Force | Out-Null
$databasePath = Join-Path $testPath 'alarms.db'
$logPath = Join-Path $testPath 'delivery.jsonl'
Add-Type -Path (Join-Path $outputPath 'LiteDB.dll')
$due = [DateTime]::UtcNow.AddSeconds(3)
$database = [LiteDB.LiteDatabase]::new($databasePath)
try {
    $database.UtcDate = $true
    $document = [LiteDB.BsonDocument]::new()
    $document['_id'] = [LiteDB.BsonValue]::new([Guid]::NewGuid())
    $document['Title'] = [LiteDB.BsonValue]::new('Teste de alarme local')
    $document['ScheduledAtUtc'] = [LiteDB.BsonValue]::new($due)
    $document['CreatedAtUtc'] = [LiteDB.BsonValue]::new([DateTime]::UtcNow)
    $document['TimeZoneId'] = [LiteDB.BsonValue]::new([TimeZoneInfo]::Local.Id)
    $null = $database.GetCollection('alarms').Insert($document)
} finally { $database.Dispose() }

$process = Start-Process -FilePath $exePath -PassThru -WindowStyle Hidden -Environment @{
    TEAMREMINDER_TEST_DATA_FILE = $databasePath
    TEAMREMINDER_TEST_DELIVERY_LOG = $logPath
}
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $logPath)) {
        $process.Refresh()
        if ($process.HasExited) { throw ('Aplicativo encerrou antes do disparo: ' + $process.ExitCode) }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Alarme não disparou dentro do prazo de teste.' }
        Start-Sleep -Milliseconds 200
    }
    Start-Sleep -Seconds 3
    $lines = @(Get-Content -LiteralPath $logPath | Where-Object { $_.Trim() })
    if ($lines.Count -ne 1) { throw ('Esperado um disparo, recebidos: ' + $lines.Count) }
    $delivery = $lines[0] | ConvertFrom-Json
    if (-not $delivery.SoundAccepted) { throw 'O Windows não aceitou a reprodução do áudio.' }
    $delivery | Format-List
    if (-not $delivery.NotificationRegistered -or $delivery.NotificationId -eq 0) {
        Write-Warning 'Canal Windows indisponível neste ambiente; aviso local e áudio foram emitidos.'
    }
} finally {
    $process.Refresh()
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id }
    }
}

$database = [LiteDB.LiteDatabase]::new($databasePath)
try {
    $database.UtcDate = $true
    $stored = $database.GetCollection('alarms').FindAll() | Select-Object -First 1
    if ($stored['LastDeliveredOccurrenceUtc'].IsNull) { throw 'Ocorrência não foi registrada no banco.' }
} finally { $database.Dispose() }

$process = Start-Process -FilePath $exePath -PassThru -WindowStyle Hidden -Environment @{
    TEAMREMINDER_TEST_DATA_FILE = $databasePath
    TEAMREMINDER_TEST_DELIVERY_LOG = $logPath
}
try {
    Start-Sleep -Seconds 3
    $lines = @(Get-Content -LiteralPath $logPath | Where-Object { $_.Trim() })
    if ($lines.Count -ne 1) { throw 'Reabrir o aplicativo repetiu a ocorrência.' }
    $process.Refresh()
    if ($process.HasExited) { throw 'Aplicativo encerrou ao reabrir o banco.' }
} finally {
    $process.Refresh()
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id }
    }
}
Write-Output ('Disparo, áudio aceito, persistência e reinício sem repetição verificados. Evidências: ' + $testPath)
