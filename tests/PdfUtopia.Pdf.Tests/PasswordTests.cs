using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Tests der Passwortwahl beim Speichern (F22): behalten, ändern, entfernen.
/// </summary>
public sealed class PasswordTests : IDisposable
{
    private const string NewPassword = "neu-Ü2026";

    private readonly TempDirectory _temp = new();
    private readonly string _plain;
    private readonly string _protected;

    public PasswordTests()
    {
        _plain = TestPdfFactory.Create(_temp.File("offen.pdf"));
        _protected = TestPdfFactory.Create(_temp.File("geschützt.pdf"), TestPdfFactory.Password);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Keep_ProtectedSource_KeepsPassword()
    {
        var target = Save(_protected, TestPdfFactory.Password, new ExportOptions());

        AssertProtectedWith(target, TestPdfFactory.Password);
    }

    [Fact]
    public void Keep_PlainSource_StaysUnprotected()
    {
        var target = Save(_plain, null, new ExportOptions());

        AssertUnprotected(target);
    }

    [Fact]
    public void Remove_ProtectedSource_SavesWithoutPassword()
    {
        var target = Save(_protected, TestPdfFactory.Password, new ExportOptions { PasswordMode = PasswordMode.Remove });

        AssertUnprotected(target);
    }

    [Fact]
    public void Change_ProtectedSource_UsesNewPasswordOnly()
    {
        var target = Save(_protected, TestPdfFactory.Password, new ExportOptions { PasswordMode = PasswordMode.Change, NewPassword = NewPassword });

        AssertProtectedWith(target, NewPassword);
        Assert.Equal(PdfOpenError.PasswordRequired, Assert.Throws<PdfOpenException>(() => PdfSource.Open(target, TestPdfFactory.Password)).Error);
    }

    [Fact]
    public void Change_PlainSource_AddsProtection()
    {
        var target = Save(_plain, null, new ExportOptions { PasswordMode = PasswordMode.Change, NewPassword = NewPassword });

        AssertProtectedWith(target, NewPassword);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Change_WithoutNewPassword_ThrowsAndWritesNothing(string? password)
    {
        var target = _temp.File("ziel.pdf");
        using var source = PdfSource.Open(_plain);
        var options = new ExportOptions { PasswordMode = PasswordMode.Change, NewPassword = password };

        Assert.Throws<ArgumentException>(() => PdfExporter.Save(source, source.CreateDocument(), target, options));
        Assert.False(File.Exists(target));
    }

    private string Save(string path, string? password, ExportOptions options)
    {
        using var source = PdfSource.Open(path, password);
        var target = _temp.File($"ziel-{Guid.NewGuid():N}.pdf");
        PdfExporter.Save(source, source.CreateDocument(), target, options);
        return target;
    }

    private static void AssertProtectedWith(string path, string password)
    {
        Assert.Equal(PdfOpenError.PasswordRequired, Assert.Throws<PdfOpenException>(() => PdfSource.Open(path)).Error);
        using var source = PdfSource.Open(path, password);
        Assert.Equal(TestPdfFactory.PageCount, source.PageCount);
    }

    private static void AssertUnprotected(string path)
    {
        using var source = PdfSource.Open(path);
        Assert.Equal(TestPdfFactory.PageCount, source.PageCount);
    }
}
