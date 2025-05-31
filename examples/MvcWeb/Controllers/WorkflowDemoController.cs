/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MvcWeb.Services;

namespace MvcWeb.Controllers;

/// <summary>
/// Demonstration controller that shows how the dynamic workflow system
/// replaces hardcoded role-based logic with configurable workflows.
/// </summary>
[Authorize]
public class WorkflowDemoController : Controller
{
    private readonly DynamicWorkflowTestService _testService;

    public WorkflowDemoController(DynamicWorkflowTestService testService)
    {
        _testService = testService;
    }

    /// <summary>
    /// Shows the main demo page explaining the dynamic workflow system.
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Runs a basic test of the dynamic workflow system.
    /// </summary>
    public async Task<IActionResult> RunBasicTest()
    {
        var result = await _testService.RunWorkflowTestAsync();
        return Json(result);
    }

    /// <summary>
    /// Runs a complete workflow scenario test.
    /// </summary>
    public async Task<IActionResult> RunCompleteScenario()
    {
        var result = await _testService.RunCompleteWorkflowScenarioAsync();
        return Json(result);
    }

    /// <summary>
    /// Shows a comparison between the old hardcoded system and the new dynamic system.
    /// </summary>
    public IActionResult Comparison()
    {
        return View();
    }
}