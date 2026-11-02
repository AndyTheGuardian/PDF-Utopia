using PdfUtopia.Core.History;

namespace PdfUtopia.Core.Tests.History;

/// <summary>
/// Tests des Verlaufs für Rückgängig/Wiederholen mit einstellbarer Schrittzahl (F8/F14).
/// </summary>
public sealed class UndoHistoryTests
{
    /// <summary>
    /// Zählt einen Wert hoch bzw. herunter.
    /// </summary>
    private sealed class Counter
    {
        public int Value { get; set; }

        public IUndoableCommand Increment() => new DelegateCommand(() => Value++, () => Value--);
    }

    [Fact]
    public void ExecuteUndoRedo_RestoresValues()
    {
        var counter = new Counter();
        var history = new UndoHistory();

        history.Execute(counter.Increment());
        history.Execute(counter.Increment());
        Assert.Equal(2, counter.Value);

        Assert.True(history.Undo());
        Assert.Equal(1, counter.Value);
        Assert.True(history.Redo());
        Assert.Equal(2, counter.Value);
    }

    [Fact]
    public void UndoRedo_OnEmptyHistory_ReturnsFalse()
    {
        var history = new UndoHistory();

        Assert.False(history.Undo());
        Assert.False(history.Redo());
    }

    [Fact]
    public void Execute_AfterUndo_DiscardsRedo()
    {
        var counter = new Counter();
        var history = new UndoHistory();
        history.Execute(counter.Increment());
        history.Undo();

        history.Execute(counter.Increment());

        Assert.False(history.CanRedo);
        Assert.Equal(1, history.UndoCount);
    }

    [Fact]
    public void Execute_BeyondLimit_DropsOldestSteps()
    {
        var counter = new Counter();
        var history = new UndoHistory(limit: 3);

        for (var i = 0; i < 5; i++)
        {
            history.Execute(counter.Increment());
        }

        Assert.Equal(3, history.UndoCount);
        while (history.Undo())
        {
        }

        Assert.Equal(2, counter.Value);
    }

    [Fact]
    public void Limit_Reduced_TrimsOldestSteps()
    {
        var counter = new Counter();
        var history = new UndoHistory();
        for (var i = 0; i < 10; i++)
        {
            history.Execute(counter.Increment());
        }

        history.Limit = 4;

        Assert.Equal(4, history.UndoCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(50, 50)]
    [InlineData(501, 500)]
    public void Limit_IsClampedToAllowedRange(int requested, int expected)
    {
        Assert.Equal(expected, new UndoHistory(requested).Limit);
        Assert.Equal(expected, new UndoHistory { Limit = requested }.Limit);
    }

    [Fact]
    public void Default_IsFiftySteps() => Assert.Equal(50, new UndoHistory().Limit);
}
