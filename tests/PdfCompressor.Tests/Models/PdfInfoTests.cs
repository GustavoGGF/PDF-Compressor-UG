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
    }
}
