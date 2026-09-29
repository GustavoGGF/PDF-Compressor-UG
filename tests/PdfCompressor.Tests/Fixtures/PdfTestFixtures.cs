using System.Text;

namespace PdfCompressor.Tests.Fixtures;

/// <summary>
/// Gerador de fixtures PDF sintéticas, seguras e em conformidade com LGPD/privacidade
/// para testes automatizados sem dependência de Ghostscript ou arquivos confidenciais.
/// </summary>
public static class PdfTestFixtures
{
    /// <summary>
    /// Gera um PDF válido de 1 página sintético.
    /// </summary>
    public static byte[] CreateOnePagePdf(string? customContent = null)
    {
        string text = customContent ?? "Documento sintético de teste - Página 1";
        string streamContent = $"BT /F1 12 Tf 72 712 Td ({text}) Tj ET";
        int streamLength = Encoding.ASCII.GetByteCount(streamContent);

        string pdf = $$"""
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
<< /Length {{streamLength}} >>
stream
{{streamContent}}
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
""";
        return Encoding.Latin1.GetBytes(pdf.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Gera um PDF válido de N páginas sintético.
    /// </summary>
    public static byte[] CreateMultiPagePdf(int pages)
    {
        if (pages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pages), "A quantidade de páginas deve ser >= 1.");
        }

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        sb.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        var kidRefs = new StringBuilder();
        for (int i = 0; i < pages; i++)
        {
            kidRefs.Append(System.Globalization.CultureInfo.InvariantCulture, $"{3 + i} 0 R ");
        }

        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"2 0 obj\n<< /Type /Pages /Kids [{kidRefs.ToString().Trim()}] /Count {pages} >>\nendobj\n");

        for (int i = 0; i < pages; i++)
        {
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"{3 + i} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n");
        }

        sb.Append("xref\n0 1\n0000000000 65535 f \n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"trailer\n<< /Size {3 + pages} /Root 1 0 R >>\nstartxref\n500\n%%EOF\n");

        return Encoding.Latin1.GetBytes(sb.ToString().Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Gera um PDF sintético com indicadores de assinatura digital (/ByteRange, /Type /Sig, /Contents).
    /// </summary>
    public static byte[] CreateSignedPdf()
    {
        string pdf = """
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
""";
        return Encoding.Latin1.GetBytes(pdf.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Gera um PDF sintético com dicionário /Encrypt (protegido por senha).
    /// </summary>
    public static byte[] CreateEncryptedPdf()
    {
        string pdf = """
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
""";
        return Encoding.Latin1.GetBytes(pdf.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Gera um arquivo com cabeçalho inválido/corrompido (%PDF- ausente).
    /// </summary>
    public static byte[] CreateCorruptHeaderFile()
    {
        return Encoding.UTF8.GetBytes("ESTE_ARQUIVO_NAO_E_UM_PDF_VALIDO_DADOS_CORROMPIDOS");
    }

    /// <summary>
    /// Gera um PDF truncado (possui cabeçalho %PDF- mas foi interrompido sem objetos válidos nem %%EOF).
    /// </summary>
    public static byte[] CreateTruncatedPdf()
    {
        return Encoding.UTF8.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R");
    }

    /// <summary>
    /// Gera um PDF com estrutura válida de cabeçalho e trailer, mas sem contagem de páginas resolúvel.
    /// </summary>
    public static byte[] CreateUnresolvablePageCountPdf()
    {
        string pdf = """
%PDF-1.4
1 0 obj
<< /Type /Catalog /Data (ObjStmSimulado) >>
endobj
xref
0 2
0000000000 65535 f 
trailer
<< /Size 2 /Root 1 0 R >>
startxref
120
%%EOF
""";
        return Encoding.Latin1.GetBytes(pdf.Replace("\r\n", "\n"));
    }
}
