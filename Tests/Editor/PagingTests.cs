// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using System.Linq;
using KitWright.Editor.Tools.Helpers;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class PagingTests
    {
        private static List<int> Numbers(int count) => Enumerable.Range(0, count).ToList();

        [Test]
        public void PagesJoinBackIntoTheWholeList([Values(1, 3, 7, 10)] int pageSize)
        {
            var source = Numbers(10);
            var walked = new List<int>();
            var cursor = 0;
            var guard = 0;

            do
            {
                walked.AddRange(Paging.Page(source, cursor, pageSize, out cursor));
                Assert.Less(guard++, 20, "the cursor stopped advancing");
            } while (cursor > 0);

            CollectionAssert.AreEqual(source, walked);
        }

        [Test]
        public void NextCursorIsZeroOnTheLastPage()
        {
            Paging.Page(Numbers(10), 5, 5, out var next);
            Assert.AreEqual(0, next);

            Paging.Page(Numbers(10), 5, 4, out next);
            Assert.AreEqual(9, next, "one item still left, so the walk is not over");
        }

        [Test]
        public void CursorPastTheEndYieldsAnEmptyPageRatherThanWrapping()
        {
            var page = Paging.Page(Numbers(3), 99, 10, out var next);

            CollectionAssert.IsEmpty(page);
            Assert.AreEqual(0, next);
        }

        [Test]
        public void NegativeCursorAndZeroPageSizeAreClamped()
        {
            var page = Paging.Page(Numbers(3), -5, 0, out var next);

            CollectionAssert.AreEqual(new[] { 0 }, page, "a page size below one must still make progress");
            Assert.AreEqual(1, next);
        }

        [Test]
        public void SuffixIsEmptyWhenEverythingFitsOnPageOne()
        {
            Assert.AreEqual(string.Empty, Paging.Suffix(cursor: 0, shown: 3, total: 3, nextCursor: 0));
        }

        [Test]
        public void SuffixNamesTheCursorToPassBack()
        {
            var suffix = Paging.Suffix(cursor: 0, shown: 50, total: 200, nextCursor: 50);

            StringAssert.Contains("Showing 1-50 of 200", suffix);
            StringAssert.Contains("cursor=50", suffix);
        }

        [Test]
        public void SuffixSaysWhereTheWalkEnded()
        {
            var suffix = Paging.Suffix(cursor: 50, shown: 10, total: 60, nextCursor: 0);

            StringAssert.Contains("Showing 51-60 of 60", suffix);
            StringAssert.Contains("end of the list", suffix);
            Assert.That(suffix, Does.Not.Contain("pass cursor="));
        }

        [Test]
        public void SuffixExplainsACursorPastTheEnd()
        {
            StringAssert.Contains("past the end", Paging.Suffix(cursor: 99, shown: 0, total: 3, nextCursor: 0));
        }
    }
}
