namespace PlikShare.Core.Encryption;

/// <summary>
/// Marker on a string property that carries encrypted metadata envelope when deserialized
/// from a SQL-produced JSON aggregate. Picked up by the resolver modifier in
/// <see cref="EncryptedMetadataJsonOptions"/>, which swaps in a stateful converter that
/// decrypts each value against the per-request <c>WorkspaceEncryptionSession</c>.
///
/// Non-marked string properties deserialize as raw strings. Does not affect protobuf or
/// any other serializer — this is a <c>System.Text.Json</c> contract only.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class EncryptedMetadataAttribute : Attribute;
