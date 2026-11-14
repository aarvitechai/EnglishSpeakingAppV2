using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SpeakMate.Application.Consents;
using SpeakMate.Application.Profiles;

namespace SpeakMate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<ProfileService>();
        services.AddScoped<ConsentService>();

        return services;
    }
}
