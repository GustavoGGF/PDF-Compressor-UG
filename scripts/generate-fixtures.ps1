<#
.SYNOPSIS
    Gera todas as fixtures sintéticas necessárias para a validação no Windows 10 e Windows 11.
.DESCRIPTION
    Cria arquivos PDF e binários sintéticos sem dados sensíveis (LGPD compliant)
    para execução da Matriz Funcional (casos 1 a 20) do PDF Compressor.
#>

param (
    [string]$OutputDir = ".\fixtures"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PDF Compressor - Gerador de Fixtures para Windows" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$absOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (-not (Test-Path $absOutputDir)) {
    New-Item -ItemType Directory -Path $absOutputDir -Force | Out-Null
}
Write-Host "Diretório de saída: $absOutputDir" -ForegroundColor Yellow

# Função auxiliar para gravar bytes
function Save-FixtureBytes {
    param(
        [string]$FileName,
        [byte[]]$Bytes
    )
    $targetPath = Join-Path $absOutputDir $FileName
    [System.IO.File]::WriteAllBytes($targetPath, $Bytes)
    $sizeKb = [Math]::Round($Bytes.Length / 1024, 2)
    Write-Host "  [+] Criado: $FileName ($sizeKb KB)" -ForegroundColor Green
}

# 1. fix-01-simple-text.pdf (3 páginas de texto simples)
$fix01Text = @"
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
4 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
5 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
xref
0 1
0000000000 65535 f
trailer
<< /Size 6 /Root 1 0 R >>
startxref
500
%%EOF
"@
Save-FixtureBytes "fix-01-simple-text.pdf" ([System.Text.Encoding]::Latin1.GetBytes($fix01Text.Replace("`r`n", "`n")))

# 2. fix-02-highres-images.pdf (PDF com bitmap RGB não comprimido para testar redução real)
$width = 300
$height = 300
$pixelDataLength = $width * $height * 3
$rgbData = New-Object byte[] $pixelDataLength
for ($i = 0; $i -lt $pixelDataLength; $i++) {
    $rgbData[$i] = [byte](($i * 17 + 43) % 256)
}

$fix02Header = @"
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im1 4 0 R >> >> /Contents 5 0 R >>
endobj
4 0 obj
<< /Type /XObject /Subtype /Image /Width $width /Height $height /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length $pixelDataLength >>
stream

"@
$fix02Footer = @"

endstream
endobj
5 0 obj
<< /Length 40 >>
stream
q 200 0 0 200 100 300 cm /Im1 Do Q
endstream
endobj
xref
0 6
0000000000 65535 f
trailer
<< /Size 6 /Root 1 0 R >>
startxref
450
%%EOF
"@
$fix02HeaderBytes = [System.Text.Encoding]::Latin1.GetBytes($fix02Header.Replace("`r`n", "`n"))
$fix02FooterBytes = [System.Text.Encoding]::Latin1.GetBytes($fix02Footer.Replace("`r`n", "`n"))
$fix02Total = New-Object byte[] ($fix02HeaderBytes.Length + $rgbData.Length + $fix02FooterBytes.Length)
[System.Buffer]::BlockCopy($fix02HeaderBytes, 0, $fix02Total, 0, $fix02HeaderBytes.Length)
[System.Buffer]::BlockCopy($rgbData, 0, $fix02Total, $fix02HeaderBytes.Length, $rgbData.Length)
[System.Buffer]::BlockCopy($fix02FooterBytes, 0, $fix02Total, ($fix02HeaderBytes.Length + $rgbData.Length), $fix02FooterBytes.Length)
Save-FixtureBytes "fix-02-highres-images.pdf" $fix02Total

# 3. fix-03-already-small.pdf (1 página simples ~1 KB)
$fix03Text = @"
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << >> >>
endobj
4 0 obj
<< /Length 50 >>
stream
BT /F1 12 Tf 72 712 Td (Documento pequeno de 1 pagina) Tj ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000218 00000 n
trailer
<< /Size 5 /Root 1 0 R >>
startxref
340
%%EOF
"@
Save-FixtureBytes "fix-03-already-small.pdf" ([System.Text.Encoding]::Latin1.GetBytes($fix03Text.Replace("`r`n", "`n")))

# 4. fix-04-signature-marked.pdf (PDF com marcadores de assinatura /Sig e /ByteRange)
$fix04Text = @"
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /SigFlags 3 >> >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>
endobj
4 0 obj
<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 1000 2000 500] /Contents <3082020a06092a864886f70d010702a08201fb> >>
endobj
5 0 obj
<< /Type /Annot /Subtype /Widget /FT /Sig /T (AssinaturaTeste) /V 4 0 R >>
endobj
xref
0 6
0000000000 65535 f
trailer
<< /Size 6 /Root 1 0 R >>
startxref
450
%%EOF
"@
Save-FixtureBytes "fix-04-signature-marked.pdf" ([System.Text.Encoding]::Latin1.GetBytes($fix04Text.Replace("`r`n", "`n")))

# 5. fix-05-password-protected.pdf (PDF criptografado com dicionário /Encrypt)
$fix05Text = @"
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
4 0 obj
<< /Filter /Standard /V 2 /R 3 /O <1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef> /U <1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef> /P -1028 >>
endobj
xref
0 5
0000000000 65535 f
trailer
<< /Size 5 /Root 1 0 R /Encrypt 4 0 R >>
startxref
400
%%EOF
"@
Save-FixtureBytes "fix-05-password-protected.pdf" ([System.Text.Encoding]::Latin1.GetBytes($fix05Text.Replace("`r`n", "`n")))

# 6. fix-06-corrupt-header.pdf (Cabeçalho corrompido sem %PDF-)
$fix06Bytes = [System.Text.Encoding]::UTF8.GetBytes("DADOS_CORROMPIDOS_ESTE_ARQUIVO_NAO_E_UM_PDF_VALIDO")
Save-FixtureBytes "fix-06-corrupt-header.pdf" $fix06Bytes

# 7. fix-08-unreachable-target.pdf (PDF denso de texto/vetor para forçar BestEffortAboveTarget)
$fix08Text = @"
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R 6 0 R 7 0 R] /Count 5 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
4 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
5 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
6 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
7 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj
xref
0 1
0000000000 65535 f
trailer
<< /Size 8 /Root 1 0 R >>
startxref
600
%%EOF
"@
Save-FixtureBytes "fix-08-unreachable-target.pdf" ([System.Text.Encoding]::Latin1.GetBytes($fix08Text.Replace("`r`n", "`n")))

# 8. Fixture em pasta com acentos e espaços (Caso 13)
$unicodeFolder = Join-Path $absOutputDir "Pasta com Acentuação e Espaço"
if (-not (Test-Path $unicodeFolder)) {
    New-Item -ItemType Directory -Path $unicodeFolder -Force | Out-Null
}
$unicodeFilePath = Join-Path $unicodeFolder "peça jurídica com espaços.pdf"
[System.IO.File]::WriteAllBytes($unicodeFilePath, $fix02Total)
Write-Host "  [+] Criado: Pasta com Acentuação e Espaço\peça jurídica com espaços.pdf" -ForegroundColor Green

Write-Host "`nTodas as fixtures foram geradas com sucesso em: $absOutputDir" -ForegroundColor Cyan
