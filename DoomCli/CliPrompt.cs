using System.ComponentModel.DataAnnotations;
using Sharprompt;

namespace DoomCli;

public record Selection<T>(T Value, string Text);

public static class CliPrompt
{
    public static bool Confirm(string message, bool defaultValue = false)
        => Prompt.Confirm(message, defaultValue);

    public static string Input(string message, Func<string, ValidationResult?>? validator = null)
        => Prompt.Input<string>(message,
            validators: validator != null ? [v => v is string s ? validator(s) : ValidationResult.Success] : null);

    public static T Select<T>(string message, IEnumerable<Selection<T>> items, T? defaultValue = default)
    {
        Selection<T>? defaultSelection = null;
        if (defaultValue != null)
        {
            var itemsList = items as IReadOnlyList<Selection<T>> ?? items.ToList();
            var comparer = EqualityComparer<T>.Default;
            defaultSelection = itemsList.FirstOrDefault(i => comparer.Equals(i.Value, defaultValue))
                ?? throw new InvalidOperationException("Default value not found in items list");
            items = itemsList;
        }

        return Prompt.Select(message, items, defaultValue: defaultSelection, textSelector: i => i.Text).Value;
    }

    public static IEnumerable<T> MultiSelect<T>(string message, IEnumerable<Selection<T>> items,
        IEnumerable<T>? defaultValues = null)
    {
        IEnumerable<Selection<T>>? defaults = null;
        if (defaultValues != null)
        {
            var itemsList = items as IReadOnlyList<Selection<T>> ?? items.ToList();
            var comparer = EqualityComparer<T>.Default;
            defaults = defaultValues
                .Select(v => itemsList.FirstOrDefault(i => comparer.Equals(i.Value, v))
                             ?? throw new InvalidOperationException("Default value not found in items list"));
            items = itemsList;
        }

        return Prompt.MultiSelect(message, items, minimum: 1, defaultValues: defaults, textSelector: i => i.Text)
            .Select(i => i.Value);
    }
}