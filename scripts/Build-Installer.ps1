param([string]$CompilerPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Instale Inno Setup 6 ou informe -CompilerPath com o caminho para ISCC.exe.'
}
Push-Location $repoPath
try {
    dotnet publish src/clients/TeamReminder.Windows -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'Falha na publicação.' }
    & $CompilerPath installer/TeamReminder.iss
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação do instalador.' }
} finally { Pop-Location }
