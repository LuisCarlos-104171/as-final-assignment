/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

namespace Piranha.Manager;

/// <summary>
/// The available workflow management permissions.
/// </summary>
public static class WorkflowManagementPermissions
{
    /// <summary>
    /// Permission to list workflow definitions.
    /// </summary>
    public const string WorkflowDefinitions = "PiranhaWorkflowDefinitions";

    /// <summary>
    /// Permission to add workflow definitions.
    /// </summary>
    public const string WorkflowDefinitionsAdd = "PiranhaWorkflowDefinitionsAdd";

    /// <summary>
    /// Permission to edit workflow definitions.
    /// </summary>
    public const string WorkflowDefinitionsEdit = "PiranhaWorkflowDefinitionsEdit";

    /// <summary>
    /// Permission to delete workflow definitions.
    /// </summary>
    public const string WorkflowDefinitionsDelete = "PiranhaWorkflowDefinitionsDelete";

    /// <summary>
    /// Gets all workflow management permissions.
    /// </summary>
    /// <returns>All workflow management permissions</returns>
    public static string[] All()
    {
        return new[]
        {
            WorkflowDefinitions,
            WorkflowDefinitionsAdd,
            WorkflowDefinitionsEdit,
            WorkflowDefinitionsDelete
        };
    }
}