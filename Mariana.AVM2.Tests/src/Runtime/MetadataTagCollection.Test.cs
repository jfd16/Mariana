using System;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class MetadataTagCollectionTest {

        [Fact]
        public void emptyCollectionShouldBeEmpty() {
            MetadataTagCollection empty;

            empty = MetadataTagCollection.empty;

            Assert.Equal(0, empty.getTags().length);
            Assert.Null(empty.getTag(""));
            Assert.Null(empty.getTag("a"));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => empty.getTag(null));

            empty = new MetadataTagCollection(default);

            Assert.Equal(0, empty.getTags().length);
            Assert.Null(empty.getTag(""));
            Assert.Null(empty.getTag("a"));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => empty.getTag(null));
        }

        [Fact]
        public void getTagsMethodTest() {
            var tags = new MetadataTag[] {
                new MetadataTag("A", default, default),
                new MetadataTag("A", default, default),
                new MetadataTag("B", default, default),
                new MetadataTag("C", default, default),
                new MetadataTag("A", default, default),
            };

            var collection = new MetadataTagCollection(tags);
            var collectionTags = collection.getTags();

            Assert.Equal(collectionTags.length, tags.Length);
            Assert.True(collectionTags.asSpan() != tags.AsSpan());

            for (int i = 0; i < tags.Length; i++)
                Assert.Same(tags[i], collectionTags[i]);
        }

        [Fact]
        public void getTagMethodTest() {
            var tags = new MetadataTag[] {
                new MetadataTag("A", default, default),
                new MetadataTag("A", default, default),
                new MetadataTag("B", default, default),
                new MetadataTag("C", default, default),
                new MetadataTag("B", default, default),
                new MetadataTag("A", default, default),
                new MetadataTag("", default, default),
            };

            var collection = new MetadataTagCollection(tags);

            Assert.Same(tags[0], collection.getTag("A"));
            Assert.Same(tags[2], collection.getTag("B"));
            Assert.Same(tags[3], collection.getTag("C"));
            Assert.Same(tags[6], collection.getTag(""));

            Assert.Null(collection.getTag("a"));
            Assert.Null(collection.getTag("D"));
            Assert.Null(collection.getTag("d"));
            Assert.Null(collection.getTag("abc"));

            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => collection.getTag(null));
        }

    }

}
