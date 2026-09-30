using PdfCompressor.Tests.Fixtures;

namespace PdfCompressor.Tests.Fixtures;

/// <summary>
/// Garante que os arquivos físicos de fixture documentados na Etapa 0 (seção 8)
/// estejam gerados e acessíveis em disco para inspeção ou testes de ponta a ponta.
/// </summary>
public static class SamplePdfFiles
{
    public static void EnsureFixturesCreated(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);

        string fix01 = Path.Combine(directoryPath, "fix-01-simple-text.pdf");
        if (!File.Exists(fix01))
        {
            File.WriteAllBytes(fix01, PdfTestFixtures.CreateMultiPagePdf(3));
        }

        string fix02 = Path.Combine(directoryPath, "fix-02-highres-images.pdf");
        if (!File.Exists(fix02))
        {
            File.WriteAllBytes(fix02, PdfTestFixtures.CreateImagePdf(300, 300));
        }

        string fix03 = Path.Combine(directoryPath, "fix-03-already-small.pdf");
        if (!File.Exists(fix03))
        {
            File.WriteAllBytes(fix03, PdfTestFixtures.CreateOnePagePdf("Documento pequeno de 1 página"));
        }

        string fix04 = Path.Combine(directoryPath, "fix-04-signature-marked.pdf");
        if (!File.Exists(fix04))
        {
            File.WriteAllBytes(fix04, PdfTestFixtures.CreateSignedPdf());
        }

        string fix05 = Path.Combine(directoryPath, "fix-05-password-protected.pdf");
        if (!File.Exists(fix05))
        {
            File.WriteAllBytes(fix05, PdfTestFixtures.CreateEncryptedPdf());
        }

        string fix06 = Path.Combine(directoryPath, "fix-06-corrupt-header.pdf");
        if (!File.Exists(fix06))
        {
            File.WriteAllBytes(fix06, PdfTestFixtures.CreateCorruptHeaderFile());
        }

        string fix08 = Path.Combine(directoryPath, "fix-08-unreachable-target.pdf");
        if (!File.Exists(fix08))
        {
            File.WriteAllBytes(fix08, PdfTestFixtures.CreateDenseVectorPdf(5));
        }
    }
}
