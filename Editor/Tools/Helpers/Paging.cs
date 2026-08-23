// Copyright (C) KitWright. Licensed under MIT.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KitWright.Editor.Tools.Helpers
{
    // One cursor shape for every tool that caps a list.
    internal static class Paging
    {
        // nextCursor is 0 when the page reached the end, so callers can treat it as "no more".
        internal static List<T> Page<T>(IList<T> items, int cursor, int pageSize, out int nextCursor)
        {
            cursor = Mathf.Clamp(cursor, 0, items.Count);
            var page = items.Skip(cursor).Take(Mathf.Max(pageSize, 1)).ToList();
            nextCursor = cursor + page.Count < items.Count ? cursor + page.Count : 0;
            return page;
        }

        // Empty while everything fits, so an unpaged response reads exactly as it did before.
        internal static string Suffix(int cursor, int shown, int total, int nextCursor)
        {
            cursor = Mathf.Max(cursor, 0);
            if (nextCursor > 0)
                return $" Showing {cursor + 1}-{cursor + shown} of {total}; pass cursor={nextCursor} for the next page.";
            if (cursor <= 0)
                return string.Empty;
            return shown > 0
                ? $" Showing {cursor + 1}-{cursor + shown} of {total}; end of the list."
                : $" cursor={cursor} is past the end of {total} item(s).";
        }

        internal const string CursorParam =
            "Resume at this index, as reported by a previous call's next_cursor. 0 starts at the beginning.";
    }
}
