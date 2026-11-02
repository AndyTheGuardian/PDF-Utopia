namespace PdfUtopia.Core.History;

/// <summary>
/// Rückgängig machbare Änderung aus zwei Aktionen.
/// </summary>
/// <param name="execute">Führt die Änderung aus.</param>
/// <param name="undo">Nimmt die Änderung zurück.</param>
internal sealed class DelegateCommand(Action execute, Action undo) : IUndoableCommand
{
    /// <inheritdoc />
    public void Execute() => execute();

    /// <inheritdoc />
    public void Undo() => undo();
}
