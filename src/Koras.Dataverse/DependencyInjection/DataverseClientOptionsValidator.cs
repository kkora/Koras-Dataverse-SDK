using Microsoft.Extensions.Options;

namespace Koras.Dataverse.DependencyInjection;

/// <summary>Bridges <see cref="DataverseClientOptions.Validate"/> into the options-validation pipeline.</summary>
internal sealed class DataverseClientOptionsValidator : IValidateOptions<DataverseClientOptions>
{
    public ValidateOptionsResult Validate(string? name, DataverseClientOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(
                $"Dataverse client '{name ?? "(default)"}' configuration is invalid: {exception.Message}");
        }
    }
}
