using System;
using System.Collections.Generic;
using System.Linq;

namespace TeklaMcp.Core;

/// <summary>Options of <c>tekla_tidy_drawing_marks</c>.</summary>
public sealed class MarkTidyOptions
{
    /// <summary>
    /// View keys whose bolt/hole marks are deleted: <c>main</c> (the non-section view with the most
    /// parts), a view's own name (section letter, e.g. <c>A</c>) or <c>*</c> for every view.
    /// Empty = keep bolt marks everywhere.
    /// </summary>
    public List<string> BoltMarkViews { get; set; } = new List<string>();
    /// <summary>Delete marks whose content is empty (typically connection marks).</summary>
    public bool DeleteEmpty { get; set; } = true;
    /// <summary>Merge part marks with identical text inside the same view.</summary>
    public bool Merge { get; set; } = true;
}

/// <summary>One unmerged mark as the planner sees it (backend-agnostic).</summary>
public sealed class MarkTidyItem
{
    /// <summary>Backend handle (index into the backend's own list).</summary>
    public int Key { get; set; }
    /// <summary>Index of the view the mark belongs to.</summary>
    public int ViewIndex { get; set; }
    /// <summary><c>main</c> or the view name.</summary>
    public string ViewKey { get; set; } = "";
    /// <summary>Type name of the marked model object: Part, Bolt, Connection, … (empty if unknown).</summary>
    public string RelatedType { get; set; } = "";
    /// <summary>Null when the content could not be read — never treated as empty.</summary>
    public bool? IsEmpty { get; set; }
    public string Text { get; set; } = "";
}

public sealed class MarkTidyPlan
{
    public List<int> Delete { get; } = new List<int>();
    /// <summary>Groups of 2+ marks to merge (same view, Part marks, identical text).</summary>
    public List<List<int>> MergeGroups { get; } = new List<List<int>>();
    public int DeletedEmpty { get; set; }
    public int DeletedBolt { get; set; }
}

/// <summary>
/// Decides what <c>tekla_tidy_drawing_marks</c> does, shared by the mock and the live backend.
/// Rules: empty marks go (when <see cref="MarkTidyOptions.DeleteEmpty"/>); bolt marks go in the
/// listed views; the remaining non-empty Part marks with the same text in one view are merged
/// ("2x 2418"). A mark whose content could not be read is never deleted as empty.
/// </summary>
public static class MarkTidyPlanner
{
    public const string MainViewKey = "main";

    public static List<string> ParseViewList(string? text) =>
        (text ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s != "-")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool ViewListed(IEnumerable<string> views, string viewKey) =>
        views.Any(v => v == "*" || string.Equals(v, viewKey, StringComparison.OrdinalIgnoreCase));

    public static MarkTidyPlan Plan(IEnumerable<MarkTidyItem> items, MarkTidyOptions? options)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        var opts = options ?? new MarkTidyOptions();
        var plan = new MarkTidyPlan();
        var keep = new List<MarkTidyItem>();
        foreach (var item in items)
        {
            if (opts.DeleteEmpty && item.IsEmpty == true)
            {
                plan.Delete.Add(item.Key);
                plan.DeletedEmpty++;
                continue;
            }
            if (string.Equals(item.RelatedType, "Bolt", StringComparison.OrdinalIgnoreCase) &&
                ViewListed(opts.BoltMarkViews, item.ViewKey))
            {
                plan.Delete.Add(item.Key);
                plan.DeletedBolt++;
                continue;
            }
            keep.Add(item);
        }
        if (!opts.Merge) return plan;
        foreach (var group in keep
                     .Where(i => string.Equals(i.RelatedType, "Part", StringComparison.OrdinalIgnoreCase) &&
                                 i.IsEmpty == false && i.Text.Trim().Length > 0)
                     .GroupBy(i => i.ViewIndex + "\u0001" + i.Text.Trim())
                     .OrderBy(g => g.First().Key))
        {
            var keys = group.Select(i => i.Key).ToList();
            if (keys.Count >= 2) plan.MergeGroups.Add(keys);
        }
        return plan;
    }
}
