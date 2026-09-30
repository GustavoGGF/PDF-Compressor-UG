<#
.SYNOPSIS
    Script de diagnóstico de ambiente e apoio à validação funcional e visual no Windows (Etapa 8).
.DESCRIPTION
    Coleta informações do ambiente Windows (Build, DPI, Ghostscript, Hash do executável),
    verifica integridade dos artefatos e gera relatório pré-formatado para docs/plano-pdf-compressor/09-validacao-windows.md.
#>

param (
    [string]$PublishDir = ".\src\PdfCompressor\bin\Release\net8.0-windows\win-x64\publish",
    [string]$ReportPath = ".\windows-validation-report.txt"
)

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PDF Compressor - Diagnóstico e Verificação Windows" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$report = [System.Collections.Generic.List[string]]::new()
function Log-Info {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
    $report.Add($Message)
}

# 1. Informações do Sistema Operacional
$os = [System.Environment]::OSVersion
$is64 = [System.Environment]::Is64BitOperatingSystem
$regCurrentVersion = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion" -ErrorAction SilentlyContinue

$productName = if ($regCurrentVersion.ProductName) { $regCurrentVersion.ProductName } else { "Windows Desconhecido" }
$displayVersion = if ($regCurrentVersion.DisplayVersion) { $regCurrentVersion.DisplayVersion } else { $regCurrentVersion.ReleaseId }
$currentBuild = if ($regCurrentVersion.CurrentBuild) { $regCurrentVersion.CurrentBuild } else { $os.Version.Build }
$ubr = if ($regCurrentVersion.UBR) { $regCurrentVersion.UBR } else { "0" }

Log-Info "`n[1. SISTEMA OPERACIONAL]" "Yellow"
Log-Info "  Edição: $productName ($displayVersion)"
Log-Info "  Build: $currentBuild.$ubr"
Log-Info "  Arquitetura: $(if ($is64) { '64-bit (x64)' } else { '32-bit (x86)' })"

# 2. Escala DPI e Resolução de Tela
Log-Info "`n[2. RESOLUÇÃO E ESCALA DPI]" "Yellow"
try {
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    $primaryScreen = [System.Windows.Forms.Screen]::PrimaryScreen
    Log-Info "  Resolução Primária: $($primaryScreen.Bounds.Width) x $($primaryScreen.Bounds.Height)"
} catch {
    Log-Info "  Resolução Primária: Não foi possível obter via System.Windows.Forms"
}

try {
    $dpiReg = Get-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "AppliedDPI" -ErrorAction SilentlyContinue
    $appliedDpi = if ($dpiReg) { $dpiReg.AppliedDPI } else { 96 }
    $scalePercent = [Math]::Round(($appliedDpi / 96) * 100)
    Log-Info "  DPI Aplicado: $appliedDpi ($scalePercent%)"
} catch {
    Log-Info "  DPI Aplicado: Padrão (100%)"
}

# 3. Artefato Publicado (PdfCompressor.exe)
Log-Info "`n[3. ARTEFATO PUBLICADO (win-x64)]" "Yellow"
$exePath = Join-Path $PublishDir "PdfCompressor.exe"
if (Test-Path $exePath) {
    $fileInfo = Get-Item $exePath
    $fileSizeMb = [Math]::Round($fileInfo.Length / 1MB, 2)
    $hash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash
    Log-Info "  Caminho: $([System.IO.Path]::GetFullPath($exePath))" "Green"
    Log-Info "  Tamanho: $($fileInfo.Length) bytes (~$fileSizeMb MB)" "Green"
    Log-Info "  SHA-256: $hash" "Green"
} else {
    Log-Info "  [AVISO] Executável não encontrado em: $exePath" "Red"
    Log-Info "  Execute 'dotnet publish src/PdfCompressor/PdfCompressor.csproj -c Release -f net8.0-windows -r win-x64 --self-contained' primeiro." "Red"
}

# 4. Detecção do Ghostscript (gswin64c.exe)
Log-Info "`n[4. LOCALIZAÇÃO DO GHOSTSCRIPT (gswin64c.exe)]" "Yellow"
$gsFound = $null
$gsVersion = $null

# Procura em Program Files
$candidates = @(
    "C:\Program Files\gs",
    "C:\Program Files (x86)\gs",
    "$env:ProgramFiles\gs",
    "${env:ProgramFiles(x86)}\gs"
)

foreach ($cand in $candidates) {
    if (Test-Path $cand) {
        $bins = Get-ChildItem -Path $cand -Filter "gswin64c.exe" -Recurse -ErrorAction SilentlyContinue
        if ($bins.Count -gt 0) {
            $gsFound = $bins[0].FullName
            break
        }
    }
}

# Procura no PATH
if (-not $gsFound) {
    $inPath = (Get-Command "gswin64c.exe" -ErrorAction SilentlyContinue)
    if ($inPath) {
        $gsFound = $inPath.Source
    }
}

if ($gsFound) {
    Log-Info "  Status: Localizado" "Green"
    Log-Info "  Caminho: $gsFound" "Green"
    try {
        $versionOutput = & $gsFound --version 2>&1
        $gsVersion = $versionOutput.Trim()
        Log-Info "  Versão: $gsVersion" "Green"
    } catch {
        Log-Info "  Versão: Não foi possível obter com --version" "Yellow"
    }
} else {
    Log-Info "  Status: [NÃO INSTALADO OU NÃO ENCONTRADO NO CAMINHO PADRÃO]" "Red"
    Log-Info "  Observação: O teste do Caso 3 (Ghostscript Ausente) requer este estado." "Yellow"
}

# 5. Diretório de Diagnósticos e Logs Temporários
Log-Info "`n[5. LOGS E AMBIENTE TEMPORÁRIO]" "Yellow"
$tempLogsDir = Join-Path $env:TEMP "PdfCompressor\logs"
$diagLogPath = Join-Path $tempLogsDir "diagnostics.log"
Log-Info "  Diretório esperado de log: $tempLogsDir"
if (Test-Path $diagLogPath) {
    $logInfo = Get-Item $diagLogPath
    Log-Info "  Arquivo diagnostics.log: Presente ($($logInfo.Length) bytes, modificado em $($logInfo.LastWriteTime))" "Green"
} else {
    Log-Info "  Arquivo diagnostics.log: Ainda não gerado nesta máquina (será criado na 1ª execução)." "Gray"
}

# Salvar relatório
$report | Out-File -FilePath $ReportPath -Encoding utf8
Log-Info "`n[CONCLUÍDO] Diagnóstico gravado em: $([System.IO.Path]::GetFullPath($ReportPath))`n" "Cyan"
