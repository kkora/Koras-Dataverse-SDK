namespace Koras.Dataverse;

/// <summary>The outcome of an upsert.</summary>
/// <param name="Id">The id of the created or updated row.</param>
/// <param name="Created"><c>true</c> when the row was created; <c>false</c> when an existing row was updated.</param>
public sealed record UpsertResult(Guid Id, bool Created);
