using System;

namespace TraceForge
{
    /// <summary>
    /// Identifies the subsystem or domain that produced a log entry.
    /// Use predefined categories in <see cref="Categories"/> or create custom ones.
    /// </summary>
    public readonly struct LogCategory : IEquatable<LogCategory>
    {
        /// <summary>The category name, or null for a default-initialized value.</summary>
        public string Name { get; }

        /// <summary>Creates a category, converting a null name to an empty string.</summary>
        /// <param name="name">The category name.</param>
        public LogCategory(string name)
        {
            Name = name ?? string.Empty;
        }

        /// <summary>Compares category names using ordinal equality.</summary>
        /// <param name="other">The category to compare.</param>
        public bool Equals(LogCategory other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <summary>Returns whether the object is a category with the same name.</summary>
        /// <param name="obj">The object to compare.</param>
        public override bool Equals(object obj) => obj is LogCategory other && Equals(other);

        /// <summary>Returns the ordinal hash of the name, or zero for a null name.</summary>
        public override int GetHashCode() => Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;

        /// <summary>Returns whether two category names are equal.</summary>
        public static bool operator ==(LogCategory left, LogCategory right) => left.Equals(right);

        /// <summary>Returns whether two category names differ.</summary>
        public static bool operator !=(LogCategory left, LogCategory right) => !left.Equals(right);

        /// <summary>Returns the category name, or an empty string for a null name.</summary>
        public override string ToString() => Name ?? string.Empty;
    }
}
