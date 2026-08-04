namespace HomeMind.Business.IServices.SmartHome;

public interface IConnectorSecretReferenceValidator
{
    Task<ConnectorSecretReferenceValidation> ValidateAsync(long tenantId, string credentialRef, CancellationToken cancellationToken = default);
}

public sealed record ConnectorSecretReferenceValidation(bool IsValid, bool IsVaultAvailable, string Message);
