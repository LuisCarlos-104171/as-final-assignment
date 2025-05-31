/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.Extensions.DependencyInjection;
using Piranha.Services;

namespace Piranha.Extensions;

/// <summary>
/// Extensions for registering dynamic workflow services.
/// </summary>
public static class DynamicWorkflowExtensions
{
    /// <summary>
    /// Adds the dynamic workflow service to the service collection.
    /// This enables configurable workflows that replace hardcoded role-based logic.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDynamicWorkflow(this IServiceCollection services)
    {
        return services.AddScoped<IDynamicWorkflowService, DynamicWorkflowService>();
    }

    /// <summary>
    /// Adds the dynamic workflow service with a custom implementation.
    /// </summary>
    /// <typeparam name="T">The dynamic workflow service implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDynamicWorkflow<T>(this IServiceCollection services)
        where T : class, IDynamicWorkflowService
    {
        return services.AddScoped<IDynamicWorkflowService, T>();
    }
}