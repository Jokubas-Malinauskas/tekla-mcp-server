using System.Collections.Generic;
using System.Linq;
using TeklaMcp.Core;
using Xunit;

namespace TeklaMcp.Tests;

/// <summary>tekla_tidy_drawing_marks decisions (drawing workflow of the J.M fork).</summary>
public class MarkTidyPlannerTests
{
    private static MarkTidyItem M(int key, int view, string viewKey, string related, bool? empty, string text = "") =>
        new MarkTidyItem { Key = key, ViewIndex = view, ViewKey = viewKey, RelatedType = related, IsEmpty = empty, Text = text };

    [Fact]
    public void DeletesEmptyAndListedBoltMarks_MergesSameTextPerView()
    {
        var items = new List<MarkTidyItem>
        {
            M(0, 0, "main", "Connection", true),
            M(1, 0, "main", "Bolt", false, "2Ø18"),
            M(2, 1, "B", "Bolt", false, "2Ø18"),
            M(3, 0, "main", "Part", false, "2418"),
            M(4, 0, "main", "Part", false, "2418"),
            M(5, 1, "B", "Part", false, "2418"),
            M(6, 0, "main", "Part", false, "2419"),
        };
        var plan = MarkTidyPlanner.Plan(items, new MarkTidyOptions
        {
            BoltMarkViews = MarkTidyPlanner.ParseViewList("main,A"),
        });
        Assert.Equal(new[] { 0, 1 }, plan.Delete);
        Assert.Equal(1, plan.DeletedEmpty);
        Assert.Equal(1, plan.DeletedBolt);
        Assert.Single(plan.MergeGroups);
        Assert.Equal(new[] { 3, 4 }, plan.MergeGroups[0]);
    }

    [Fact]
    public void UnreadableContentIsNeverDeletedAsEmpty()
    {
        var plan = MarkTidyPlanner.Plan(new[] { M(0, 0, "main", "Connection", null) }, new MarkTidyOptions());
        Assert.Empty(plan.Delete);
    }

    [Fact]
    public void StarDeletesBoltMarksEverywhere_MergeCanBeOff()
    {
        var items = new[]
        {
            M(0, 0, "main", "Bolt", false, "4Ø18"),
            M(1, 2, "C", "Bolt", false, "4Ø18"),
            M(2, 0, "main", "Part", false, "2420"),
            M(3, 0, "main", "Part", false, "2420"),
        };
        var plan = MarkTidyPlanner.Plan(items, new MarkTidyOptions
        {
            BoltMarkViews = MarkTidyPlanner.ParseViewList("*"),
            Merge = false,
        });
        Assert.Equal(new[] { 0, 1 }, plan.Delete);
        Assert.Empty(plan.MergeGroups);
    }

    [Fact]
    public void ParseViewList_IgnoresDashAndBlanks()
    {
        Assert.Empty(MarkTidyPlanner.ParseViewList("-"));
        Assert.Equal(new[] { "main", "A" }, MarkTidyPlanner.ParseViewList(" main ; A ,,a"));
    }
}
