namespace Koras.Dataverse;

/// <summary>The response of the Dataverse <c>WhoAmI</c> function.</summary>
/// <param name="UserId">The systemuser id of the authenticated caller.</param>
/// <param name="BusinessUnitId">The caller's business unit id.</param>
/// <param name="OrganizationId">The environment's organization id.</param>
public sealed record WhoAmIResponse(Guid UserId, Guid BusinessUnitId, Guid OrganizationId);
