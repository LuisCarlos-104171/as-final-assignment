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
/// The available workflow permissions for the content approval process.
/// </summary>
public static class WorkflowPermissions
{
    // Content workflow permissions
    public const string ContentSubmitForReview = "PiranhaContentSubmitForReview";
    public const string ContentReview = "PiranhaContentReview";
    public const string ContentApprove = "PiranhaContentApprove";
    public const string ContentReject = "PiranhaContentReject";
    
    // Page workflow permissions
    public const string PagesSubmitForReview = "PiranhaPagesSubmitForReview";
    public const string PagesReview = "PiranhaPagesReview";
    public const string PagesApprove = "PiranhaPagesApprove";
    public const string PagesReject = "PiranhaPagesReject";
    
    // Post workflow permissions
    public const string PostsSubmitForReview = "PiranhaPostsSubmitForReview";
    public const string PostsReview = "PiranhaPostsReview";
    public const string PostsApprove = "PiranhaPostsApprove"; 
    public const string PostsReject = "PiranhaPostsReject";

    public static string[] All()
    {
        return new[]
        {
            ContentSubmitForReview,
            ContentReview,
            ContentApprove,
            ContentReject,
            PagesSubmitForReview,
            PagesReview,
            PagesApprove,
            PagesReject,
            PostsSubmitForReview,
            PostsReview,
            PostsApprove,
            PostsReject
        };
    }
}