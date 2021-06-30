using System;

namespace Mariana.AVM2.Tests.Helpers {

    /// <summary>
    /// Used to wrap objects in xUnit theory data. This object wrapper provides an overridden ToString
    /// implementation that does not call ToString on the underlying instance, so this can be used for
    /// objects whose ToString methods may fail (for example, with infinite recursion due to a cyclic
    /// object graph) when called by the test runner.
    /// </summary>
    public readonly struct ObjectWrapper<T> {

        public readonly T value;

        public ObjectWrapper(T value) => this.value = value;

        public static implicit operator ObjectWrapper<T>(T value) => new ObjectWrapper<T>(value);

        public override bool Equals(object obj) =>
            throw new InvalidOperationException("ObjectWrapper should not be tested for equality. Test the value instead.");

        public override int GetHashCode() =>
            throw new InvalidOperationException("ObjectWrapper should not be tested for equality. Test the value instead.");

        public override string ToString() => $"object {GetType()}";

    }

}
