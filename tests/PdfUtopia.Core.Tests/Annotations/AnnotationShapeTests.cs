using PdfUtopia.Core.Annotations;

namespace PdfUtopia.Core.Tests.Annotations;

/// <summary>
/// Tests der gemeinsamen Eckenrundung von Rechtecken und Kommentaren (A37).
/// </summary>
public sealed class AnnotationShapeTests
{
    [Theory]
    [InlineData(100, 50, AnnotationShape.CornerRadius)]
    [InlineData(6, 50, 3)]
    [InlineData(100, 2, 1)]
    [InlineData(0, 50, 0)]
    public void CornerRadiusFor_IsLimitedToHalfTheShorterSide(double width, double height, double expected) =>
        Assert.Equal(expected, AnnotationShape.CornerRadiusFor(width, height));
}
