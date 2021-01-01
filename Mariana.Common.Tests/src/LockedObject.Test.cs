using System;
using System.Threading;
using Xunit;

namespace Mariana.Common.Tests {

    public class LockedObjectTest {

        [Fact]
        public void shouldTakeLockOnConstruct() {
            object obj = new object(), mutex = new object();
            using (var locked = new LockedObject<object>(obj, mutex)) {
                Assert.True(Monitor.IsEntered(mutex));
            }
        }

        [Fact]
        public void shouldReleaseLockOnDispose() {
            object obj = new object(), mutex = new object();
            using (var locked = new LockedObject<object>(obj, mutex)) {}
            Assert.False(Monitor.IsEntered(mutex));
        }

        [Fact]
        public void shouldUseObjectAsLockIfNoLockProvided() {
            object obj = new object();
            using (var locked = new LockedObject<object>(obj)) {
                Assert.True(Monitor.IsEntered(obj));
            }
            Assert.False(Monitor.IsEntered(obj));
        }

        [Fact]
        public void shouldGetObject() {
            object obj = new object(), mutex = new object();
            using (var locked = new LockedObject<object>(obj, mutex)) {
                Assert.Same(locked.value, obj);
            }
        }

    }

}
