using System;

namespace TraceForge
{
    /// <summary>
    /// Identifies the subsystem or domain that produced a log entry.
    /// Use predefined categories in <see cref="Categories"/> or create custom ones.
    /// </summary>
    public readonly struct LogCategory : IEquatable<LogCategory>
    {
        public string Name { get; }

        public LogCategory(string name)
        {
            Name = name ?? string.Empty;
        }

        public bool Equals(LogCategory other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LogCategory other && Equals(other);

        public override int GetHashCode() => Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;

        public static bool operator ==(LogCategory left, LogCategory right) => left.Equals(right);

        public static bool operator !=(LogCategory left, LogCategory right) => !left.Equals(right);

        public override string ToString() => Name ?? string.Empty;
    }
}
