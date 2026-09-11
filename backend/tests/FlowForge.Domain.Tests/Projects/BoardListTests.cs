using FlowForge.Domain.Projects;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Projects;

public class BoardListTests
{
    [Fact]
    public void Create_DefaultsIsDoneColumnToFalse()
    {
        var list = BoardList.Create(Guid.NewGuid(), Guid.NewGuid(), "To Do").Value;

        list.IsDoneColumn.Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_CanMarkAListAsTheDoneColumn()
    {
        var list = BoardList.Create(Guid.NewGuid(), Guid.NewGuid(), "Done").Value;

        var result = list.UpdateDetails("Done", "#10b981", null, isDoneColumn: true);

        result.IsSuccess.Should().BeTrue();
        list.IsDoneColumn.Should().BeTrue();
    }

    [Fact]
    public void Create_WithBlankName_Fails()
    {
        var result = BoardList.Create(Guid.NewGuid(), Guid.NewGuid(), "   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IsAtWipLimit_WithNoTasksAndNoLimit_IsFalse()
    {
        var list = BoardList.Create(Guid.NewGuid(), Guid.NewGuid(), "To Do").Value;

        list.IsAtWipLimit().Should().BeFalse();
    }
}
