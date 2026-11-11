namespace TomasAI.IFM.Application.Storage.SchemaDb;

/// <summary>Contains technical provider-error classification for additive schema application.</summary>
internal static class SchemaDbContextExtensions
{
    extension(Exception schemaError)
    {
        /// <summary>Matches only the provider fragments declared by the checked-in additive definition.</summary>
        internal bool IsAlreadyApplied(SchemaObjectDefinition definition)
        {
            var fragments = definition.AlreadyAppliedErrorFragments;
            if (fragments is null || fragments.Count == 0) return false;
            var text = schemaError.ToString();
            return fragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}
