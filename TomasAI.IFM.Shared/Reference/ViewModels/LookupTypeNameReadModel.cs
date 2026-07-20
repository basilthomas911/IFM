using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Reference.ViewModels
{
    /// <summary>
    /// MessagePack-serializable view model representing a reference lookup type name.
    /// </summary>
    /// <remarks>
    /// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
    /// and a parameterless constructor for serializers.
    /// </remarks>
    [MessagePackObject(AllowPrivate = true)]
    public record LookupTypeNameReadModel
    {
        /// <summary>The lookup type name.</summary>
        [Key(0)]
        public string LookupTypeName { get; init; } = string.Empty;

        /// <summary>Parameterless constructor for serializers.</summary>
        public LookupTypeNameReadModel() { }

        /// <summary>Creates a new lookup type name view model.</summary>
        /// <param name="lookupTypeName">The lookup type name.</param>
        public LookupTypeNameReadModel(string lookupTypeName)
        {
            LookupTypeName = lookupTypeName ?? string.Empty;
        }

        /// <summary>Returns a compact JSON representation of the model.</summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}