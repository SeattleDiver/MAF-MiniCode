using System.Linq;
using System.Text;

namespace MiniCode.Tools;

/// <summary>
/// Condenses dotnet test output for Microsoft.Testing.Platform runners.
/// Passing tests are not itemized by the runner itself, only failing ones
/// are — so this keeps the summary counts and each failure's message and
/// source location, and drops the rest.
/// </summary>
public static class TestOutputParser
{
    private const int MaxFailures = 5;

    /// <summary>Extracts the summary line and up to five failures from raw output.</summary>
    public static string Summarize(string output)
    {
        string[] lines = output.ReplaceLineEndings("\n").Split('\n');
        var text = new StringBuilder();

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();
            if (line.StartsWith("Test run summary:") || trimmed.StartsWith("total:")
                || trimmed.StartsWith("failed:") || trimmed.StartsWith("succeeded:") || trimmed.StartsWith("error:"))
            {
                text.AppendLine(trimmed);
            }
        }

        int total = lines.Count(l => l.StartsWith("failed "));
        int shown = 0;
        for (int i = 0; i < lines.Length && shown < MaxFailures; i++)
        {
            if (!lines[i].StartsWith("failed "))
            {
                continue;
            }

            text.AppendLine().AppendLine(lines[i]);
            shown++;

            int j = i + 1;
            while (j < lines.Length && lines[j].StartsWith(' '))
            {
                string detail = lines[j].Trim();
                j++;
                if (detail.StartsWith("from ", StringComparison.Ordinal))
                {
                    continue;
                }

                text.AppendLine("  " + detail);
                if (detail.StartsWith("at ", StringComparison.Ordinal))
                {
                    break; // the first frame is the test itself; the rest is framework noise
                }
            }
        }

        if (total > shown)
        {
            text.AppendLine().AppendLine($"... {total - shown} more failing tests not shown.");
        }

        return text.ToString();
    }
}
