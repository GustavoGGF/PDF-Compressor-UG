using PdfCompressor.Models;
using Xunit;

namespace PdfCompressor.Tests.Models;

public sealed class PdfInfoTests
{
    [Fact]
    public void FileSizeMb_CalculatesUsingDecimalMbStandard()
    {
        // DEC-01: 1 MB = 1.000.000 bytes
        var info = new PdfInfo(
            FilePath: "sample.pdf",
            FileSizeBytes: 5_250_000,
            PageCount: 12,
            HasLikelySignature: false,
            IsEncrypted: false,
            IsValid: true
        );

        Assert.Equal(5.25, info.FileSizeMb);
        Assert.Equal(PdfAnalysisStatus.Success, info.Status);
    }

    [Fact]
    public void FileSizeMb_ZeroBytes_ReturnsZero()
    {
        var info = new PdfInfo(
            FilePath: "empty.pdf",
            FileSizeBytes: 0,
            PageCount: 0,
            HasLikelySignature: false,
            IsEncrypted: false,
            IsValid: true
        );

        Assert.Equal(0.0, info.FileSizeMb);
        Assert.Equal(PdfAnalysisStatus.Success, info.Status);
    }

    [Fact]
    public void ErrorMessage_PreservedWhenInvalid()
    {
        var info = new PdfInfo(
            FilePath: "corrupted.pdf",
            FileSizeBytes: 100,
            PageCount: null,
            HasLikelySignature: false,
            IsEncrypted: false,
            IsValid: false,
            ErrorMessage: "Arquivo corrompido"
        );

        Assert.False(info.IsValid);
        Assert.Null(info.PageCount);
        Assert.Equal("Arquivo corrompido", info.ErrorMessage);
        Assert.Equal(PdfAnalysisStatus.Failed, info.Status);
    }

    [Fact]
    public void WarningStatus_WhenSignatureDetected()
    {
        var info = PdfInfo.Warning(
            filePath: "signed.pdf",
            fileSizeBytes: 2_000_000,
            pageCount: 3,
            hasLikelySignature: true,
            warningMessage: PdfInfo.DefaultSignatureWarningMessage
        );

        Assert.True(info.IsValid);
        Assert.True(info.HasLikelySignature);
        Assert.Equal(PdfAnalysisStatus.Warning, info.Status);
        Assert.Equal(PdfInfo.DefaultSignatureWarningMessage, info.WarningMessage);
    }

    [Fact]
    public void FailedFactory_ProducesExpectedFailedRecord()
    {
        var info = PdfInfo.Failed("error.pdf", "Arquivo não encontrado", 0);

        Assert.False(info.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, info.Status);
        Assert.Equal("Arquivo não encontrado", info.ErrorMessage);
        Assert.Null(info.PageCount);
    }

    [Fact]
    public void SuccessFactory_ProducesExpectedSuccessRecord()
    {
        var info = PdfInfo.Success("valid.pdf", 1_500_000, 4);

        Assert.True(info.IsValid);
        Assert.Equal(PdfAnalysisStatus.Success, info.Status);
        Assert.Equal(4, info.PageCount);
        Assert.False(info.HasLikelySignature);
        Assert.False(info.IsEncrypted);
    }
}
