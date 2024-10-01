﻿using Microsoft.Extensions.DependencyInjection;
using Mvx.ApiClient.Clients;
using Mvx.ApiClient.Enums;
using Mvx.ApiClient.Exceptions;
using Mvx.ApiClient.Interfaces;
using Mvx.ApiClient.Interfaces.Clients;
using System.Net.Mime;
using System.Text.Json;

namespace Mvx.ApiClient.ExtensionMethods;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MultiversX API client as a transient service to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="networkType">The type of network to connect to</param>
    /// <returns></returns>
    public static IServiceCollection AddMvxApiClient(this IServiceCollection services, NetworkType networkType)
    {
        services.AddTransient<ErrorHandler>();
        
        RegisterClient<INetworkClient, NetworkClient>(services, networkType);

        services.AddTransient<IMvxApiClient>(provider =>
        {
            var networkClient = provider.GetRequiredService<INetworkClient>();

            return new MvxApiClient(networkClient);
        });
        
        return services;
    }

    /// <summary>
    /// Registers a custom client
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="networkType">The type of network to connect to</param>
    /// <typeparam name="TClientInterface">The client interface</typeparam>
    /// <typeparam name="TClientImplementation">The client implementation</typeparam>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the type of network is not valid</exception>
    private static void RegisterClient<TClientInterface, TClientImplementation>(this IServiceCollection services, NetworkType networkType)
        where TClientInterface : class
        where TClientImplementation : class, TClientInterface
    {
        var baseAddress = networkType switch
        {
            NetworkType.Mainnet => Constants.BaseAddressMainnetApi,
            NetworkType.Testnet => Constants.BaseAddressTestnetApi,
            NetworkType.Devnet => Constants.BaseAddressDevnetApi,
            _ => throw new ArgumentOutOfRangeException(nameof(networkType), $"Unexpected network type: {networkType}")
        };

        services.AddHttpClient<TClientInterface, TClientImplementation>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.DefaultRequestHeaders.Add("accept", MediaTypeNames.Application.Json);
        })
        .AddHttpMessageHandler<ErrorHandler>();
    }

    private class ErrorHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                var errorDetails = JsonSerializer.Deserialize<MvxApiException>(content)!;
                
                throw new MvxApiException(errorDetails.Message, errorDetails.Error, errorDetails.StatusCode);
            }

            return response;
        }
    }
}
