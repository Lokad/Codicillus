using System.Text;

namespace Lokad.Codicillus.Tools;

internal sealed class ApplyPatchEngine
{
    public static ApplyPatchResult Apply(string input, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ApplyPatchResult(false, "apply_patch input is empty", 0, 0, 0);
        }

        var lines = SplitLines(input);
        if (lines.Count == 0 || lines[0] != "*** Begin Patch")
        {
            return new ApplyPatchResult(false, "apply_patch missing Begin Patch header", 0, 0, 0);
        }

        var ops = new List<PatchOp>();
        var index = 1;
        var sawEndPatch = false;
        while (index < lines.Count)
        {
            var line = lines[index];
            if (line == "*** End Patch")
            {
                sawEndPatch = true;
                index++;
                break;
            }

            if (line.StartsWith("*** Add File: ", StringComparison.Ordinal))
            {
                var path = line["*** Add File: ".Length..].Trim();
                index++;
                var contentLines = new List<string>();
                while (index < lines.Count && !IsHunkHeader(lines[index]))
                {
                    var addLine = lines[index];
                    if (!addLine.StartsWith("+", StringComparison.Ordinal))
                    {
                        return new ApplyPatchResult(false, $"Invalid add line: {addLine}", 0, 0, 0);
                    }
                    contentLines.Add(addLine[1..]);
                    index++;
                }
                if (contentLines.Count == 0)
                {
                    return new ApplyPatchResult(false, $"Add file '{path}' has no content", 0, 0, 0);
                }
                ops.Add(new AddFileOp(path, contentLines));
                continue;
            }

            if (line.StartsWith("*** Delete File: ", StringComparison.Ordinal))
            {
                var path = line["*** Delete File: ".Length..].Trim();
                index++;
                ops.Add(new DeleteFileOp(path));
                continue;
            }

            if (line.StartsWith("*** Update File: ", StringComparison.Ordinal))
            {
                var path = line["*** Update File: ".Length..].Trim();
                index++;
                string? moveTo = null;
                if (index < lines.Count && lines[index].StartsWith("*** Move to: ", StringComparison.Ordinal))
                {
                    moveTo = lines[index]["*** Move to: ".Length..].Trim();
                    index++;
                }

                var blocks = new List<DiffBlock>();
                var current = new List<DiffLine>();
                bool? endsWithNewlineOverride = null;
                while (index < lines.Count && !IsHunkHeader(lines[index]))
                {
                    var updateLine = lines[index];
                    if (updateLine.StartsWith("@@", StringComparison.Ordinal))
                    {
                        if (current.Count > 0)
                        {
                            blocks.Add(new DiffBlock(current));
                            current = new List<DiffLine>();
                        }
                        index++;
                        continue;
                    }

                    if (updateLine == "*** End of File")
                    {
                        endsWithNewlineOverride = false;
                        index++;
                        continue;
                    }

                    if (updateLine.Length == 0)
                    {
                        return new ApplyPatchResult(false, "Unexpected empty line in update hunk", 0, 0, 0);
                    }

                    var kind = updateLine[0];
                    if (kind != ' ' && kind != '+' && kind != '-')
                    {
                        return new ApplyPatchResult(false, $"Invalid update line: {updateLine}", 0, 0, 0);
                    }
                    current.Add(new DiffLine(kind, updateLine[1..]));
                    index++;
                }
                if (current.Count > 0)
                {
                    blocks.Add(new DiffBlock(current));
                }
                ops.Add(new UpdateFileOp(path, moveTo, blocks, endsWithNewlineOverride));
                continue;
            }

            return new ApplyPatchResult(false, $"Unexpected line: {line}", 0, 0, 0);
        }

        if (!sawEndPatch)
        {
            return new ApplyPatchResult(false, "apply_patch missing End Patch footer", 0, 0, 0);
        }
        if (index < lines.Count)
        {
            return new ApplyPatchResult(false, "apply_patch contains trailing content", 0, 0, 0);
        }

        var baseDir = string.IsNullOrWhiteSpace(baseDirectory)
            ? Directory.GetCurrentDirectory()
            : baseDirectory;
        baseDir = Path.GetFullPath(baseDir);

        var added = 0;
        var updated = 0;
        var deleted = 0;

        foreach (var op in ops)
        {
            switch (op)
            {
                case AddFileOp add:
                    {
                        var path = ResolvePath(baseDir, add.Path);
                        if (File.Exists(path))
                        {
                            return new ApplyPatchResult(false, $"Add file '{add.Path}' already exists", 0, 0, 0);
                        }
                        var directory = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        var content = string.Join(Environment.NewLine, add.Lines);
                        File.WriteAllText(path, content, Encoding.UTF8);
                        added++;
                        break;
                    }
                case DeleteFileOp delete:
                    {
                        var path = ResolvePath(baseDir, delete.Path);
                        if (!File.Exists(path))
                        {
                            return new ApplyPatchResult(false, $"Delete file '{delete.Path}' not found", 0, 0, 0);
                        }
                        File.Delete(path);
                        deleted++;
                        break;
                    }
                case UpdateFileOp update:
                    {
                        var path = ResolvePath(baseDir, update.Path);
                        if (!File.Exists(path))
                        {
                            return new ApplyPatchResult(false, $"Update file '{update.Path}' not found", 0, 0, 0);
                        }

                        var file = LoadFile(path);
                        var linesList = file.Lines;
                        var cursor = 0;
                        foreach (var block in update.Blocks)
                        {
                            var before = new List<string>();
                            var after = new List<string>();
                            foreach (var diff in block.Lines)
                            {
                                if (diff.Kind == ' ')
                                {
                                    before.Add(diff.Text);
                                    after.Add(diff.Text);
                                }
                                else if (diff.Kind == '-')
                                {
                                    before.Add(diff.Text);
                                }
                                else if (diff.Kind == '+')
                                {
                                    after.Add(diff.Text);
                                }
                            }

                            var matchIndex = before.Count == 0
                                ? cursor
                                : FindMatch(linesList, before, cursor);
                            if (matchIndex < 0)
                            {
                                return new ApplyPatchResult(false, $"Update file '{update.Path}' context not found", 0, 0, 0);
                            }

                            if (before.Count > 0)
                            {
                                linesList.RemoveRange(matchIndex, before.Count);
                            }
                            if (after.Count > 0)
                            {
                                linesList.InsertRange(matchIndex, after);
                            }
                            cursor = matchIndex + after.Count;
                        }

                        var endsWithNewline = update.EndsWithNewlineOverride ?? file.EndsWithNewline;
                        WriteFile(path, linesList, file.Newline, endsWithNewline);

                        if (!string.IsNullOrWhiteSpace(update.MoveTo))
                        {
                            var target = ResolvePath(baseDir, update.MoveTo!);
                            if (File.Exists(target))
                            {
                                return new ApplyPatchResult(false, $"Move target '{update.MoveTo}' already exists", 0, 0, 0);
                            }
                            var targetDir = Path.GetDirectoryName(target);
                            if (!string.IsNullOrEmpty(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }
                            File.Move(path, target);
                        }

                        updated++;
                        break;
                    }
            }
        }

        var summary = $"Applied patch: added {added}, updated {updated}, deleted {deleted}.";
        return new ApplyPatchResult(true, summary, added, updated, deleted);
    }

    private static List<string> SplitLines(string input)
    {
        var normalized = input.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        return lines;
    }

    private static bool IsHunkHeader(string line)
    {
        return line == "*** End Patch"
            || line.StartsWith("*** Add File: ", StringComparison.Ordinal)
            || line.StartsWith("*** Delete File: ", StringComparison.Ordinal)
            || line.StartsWith("*** Update File: ", StringComparison.Ordinal);
    }

    private static string ResolvePath(string baseDir, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }
        return Path.GetFullPath(Path.Combine(baseDir, path));
    }

    private static FileSnapshot LoadFile(string path)
    {
        var content = File.ReadAllText(path);
        var endsWithNewline = content.EndsWith("\n", StringComparison.Ordinal)
            || content.EndsWith("\r", StringComparison.Ordinal);
        var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n').ToList();
        if (endsWithNewline && lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        return new FileSnapshot(lines, endsWithNewline, newline);
    }

    private static void WriteFile(string path, List<string> lines, string newline, bool endsWithNewline)
    {
        var content = string.Join(newline, lines);
        if (endsWithNewline)
        {
            content += newline;
        }
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    private static int FindMatch(List<string> lines, List<string> needle, int startIndex)
    {
        if (needle.Count == 0)
        {
            return startIndex;
        }
        for (var i = startIndex; i <= lines.Count - needle.Count; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Count; j++)
            {
                if (!string.Equals(lines[i + j], needle[j], StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return i;
            }
        }
        return -1;
    }

    private abstract record PatchOp;

    private sealed record AddFileOp(string Path, IReadOnlyList<string> Lines) : PatchOp;

    private sealed record DeleteFileOp(string Path) : PatchOp;

    private sealed record UpdateFileOp(
        string Path,
        string? MoveTo,
        IReadOnlyList<DiffBlock> Blocks,
        bool? EndsWithNewlineOverride) : PatchOp;

    private sealed record DiffBlock(IReadOnlyList<DiffLine> Lines);

    private sealed record DiffLine(char Kind, string Text);

    private sealed record FileSnapshot(List<string> Lines, bool EndsWithNewline, string Newline);
}

internal sealed record ApplyPatchResult(
    bool Success,
    string Message,
    int AddedFiles,
    int UpdatedFiles,
    int DeletedFiles);
