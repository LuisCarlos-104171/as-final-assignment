/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Piranha.AspNetCore.Identity.Data;

namespace Piranha.AspNetCore.Identity;

public abstract class Db<T> :
    IdentityDbContext<User, Role, Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>,
    IDb
    where T : Db<T>
{
    /// <summary>
    ///     Gets/sets whether the db context as been initialized. This
    ///     is only performed once in the application lifecycle.
    /// </summary>
    private static volatile bool IsInitialized;

    /// <summary>
    ///     The object mutex used for initializing the context.
    /// </summary>
    private static readonly object Mutex = new object();

    /// <summary>
    ///     Default constructor.
    /// </summary>
    /// <param name="options">Configuration options</param>
    protected Db(DbContextOptions<T> options) : base(options)
    {
        if (IsInitialized)
        {
            return;
        }

        lock (Mutex)
        {
            if (IsInitialized)
            {
                return;
            }

            // Migrate database
            Database.Migrate();

            Seed();

            IsInitialized = true;
        }
    }

    /// <summary>
    ///     Creates and configures the data model.
    /// </summary>
    /// <param name="mb">The current model builder</param>
    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<User>().ToTable("Piranha_Users");
        mb.Entity<Role>().ToTable("Piranha_Roles");
        mb.Entity<IdentityUserClaim<Guid>>().ToTable("Piranha_UserClaims");
        mb.Entity<IdentityUserRole<Guid>>().ToTable("Piranha_UserRoles");
        mb.Entity<IdentityUserLogin<Guid>>().ToTable("Piranha_UserLogins");
        mb.Entity<IdentityRoleClaim<Guid>>().ToTable("Piranha_RoleClaims");
        mb.Entity<IdentityUserToken<Guid>>().ToTable("Piranha_UserTokens");
    }

    /// <summary>
    /// Seeds the default data.
    /// </summary>
    private void Seed()
    {
        SaveChanges();

        // Make sure we have a SysAdmin role
        var sysAdminRole = Roles.FirstOrDefault(r => r.NormalizedName == "SYSADMIN");
        if (sysAdminRole == null)
        {
            sysAdminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "SysAdmin",
                NormalizedName = "SYSADMIN"
            };
            Roles.Add(sysAdminRole);
        }

        // Make sure our SysAdmin role has all of the available claims
        //foreach (var claim in Piranha.Security.Permission.All())
        foreach (var permission in App.Permissions.GetPermissions())
        {
            var roleClaim = RoleClaims.FirstOrDefault(c =>
                c.RoleId == sysAdminRole.Id && c.ClaimType == permission.Name && c.ClaimValue == permission.Name);
            if (roleClaim == null)
            {
                RoleClaims.Add(new IdentityRoleClaim<Guid>
                {
                    RoleId = sysAdminRole.Id,
                    ClaimType = permission.Name,
                    ClaimValue = permission.Name
                });
            }
        }

        // Writer role - can create and edit content but not publish or delete
        var writerRole = Roles.FirstOrDefault(r => r.NormalizedName == "WRITER");
        if (writerRole == null)
        {
            writerRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Writer",
                NormalizedName = "WRITER"
            };
            Roles.Add(writerRole);
        }

        // Writer permissions
        var writerPermissions = new[]
        {
            Piranha.Security.Permission.PagePreview,
            Piranha.Security.Permission.PostPreview,
            Piranha.Manager.Permission.Pages,
            Piranha.Manager.Permission.PagesEdit,
            Piranha.Manager.Permission.PagesSave,
            Piranha.Manager.Permission.Posts,
            Piranha.Manager.Permission.PostsEdit,
            Piranha.Manager.Permission.PostsSave,
            Piranha.Manager.Permission.Media,
            Piranha.Manager.Permission.MediaAdd,
            Piranha.Manager.Permission.MediaEdit,
            Piranha.Manager.Permission.Content,
            Piranha.Manager.Permission.ContentEdit,
            Piranha.Manager.Permission.ContentSave,
            // Workflow permissions
            Piranha.Manager.WorkflowPermissions.ContentSubmitForReview,
            Piranha.Manager.WorkflowPermissions.PagesSubmitForReview,
            Piranha.Manager.WorkflowPermissions.PostsSubmitForReview
        };

        foreach (var permissionName in writerPermissions)
        {
            var writerClaim = RoleClaims.FirstOrDefault(c =>
                c.RoleId == writerRole.Id && c.ClaimType == permissionName && c.ClaimValue == permissionName);
            if (writerClaim == null)
            {
                RoleClaims.Add(new IdentityRoleClaim<Guid>
                {
                    RoleId = writerRole.Id,
                    ClaimType = permissionName,
                    ClaimValue = permissionName
                });
            }
        }

        // Editor role - can create, edit and delete content but not publish
        var editorRole = Roles.FirstOrDefault(r => r.NormalizedName == "EDITOR");
        if (editorRole == null)
        {
            editorRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Editor",
                NormalizedName = "EDITOR"
            };
            Roles.Add(editorRole);
        }

        // Editor permissions
        var editorPermissions = new[]
        {
            Piranha.Security.Permission.PagePreview,
            Piranha.Security.Permission.PostPreview,
            Piranha.Manager.Permission.Pages,
            Piranha.Manager.Permission.PagesAdd,
            Piranha.Manager.Permission.PagesEdit,
            Piranha.Manager.Permission.PagesSave,
            Piranha.Manager.Permission.PagesDelete,
            Piranha.Manager.Permission.Posts,
            Piranha.Manager.Permission.PostsAdd,
            Piranha.Manager.Permission.PostsEdit,
            Piranha.Manager.Permission.PostsSave,
            Piranha.Manager.Permission.PostsDelete,
            Piranha.Manager.Permission.Media,
            Piranha.Manager.Permission.MediaAdd,
            Piranha.Manager.Permission.MediaEdit,
            Piranha.Manager.Permission.MediaDelete,
            Piranha.Manager.Permission.MediaAddFolder,
            Piranha.Manager.Permission.MediaDeleteFolder,
            Piranha.Manager.Permission.Content,
            Piranha.Manager.Permission.ContentAdd,
            Piranha.Manager.Permission.ContentEdit,
            Piranha.Manager.Permission.ContentSave,
            Piranha.Manager.Permission.ContentDelete,
            Piranha.Manager.Permission.Comments,
            Piranha.Manager.Permission.CommentsApprove,
            Piranha.Manager.Permission.CommentsDelete,
            // Workflow permissions
            Piranha.Manager.WorkflowPermissions.ContentSubmitForReview,
            Piranha.Manager.WorkflowPermissions.PagesSubmitForReview,
            Piranha.Manager.WorkflowPermissions.PostsSubmitForReview,
            Piranha.Manager.WorkflowPermissions.ContentReview,
            Piranha.Manager.WorkflowPermissions.PagesReview,
            Piranha.Manager.WorkflowPermissions.PostsReview,
            Piranha.Manager.WorkflowPermissions.ContentReject,
            Piranha.Manager.WorkflowPermissions.PagesReject,
            Piranha.Manager.WorkflowPermissions.PostsReject
        };

        foreach (var permissionName in editorPermissions)
        {
            var editorClaim = RoleClaims.FirstOrDefault(c =>
                c.RoleId == editorRole.Id && c.ClaimType == permissionName && c.ClaimValue == permissionName);
            if (editorClaim == null)
            {
                RoleClaims.Add(new IdentityRoleClaim<Guid>
                {
                    RoleId = editorRole.Id,
                    ClaimType = permissionName,
                    ClaimValue = permissionName
                });
            }
        }

        // Approver role - can publish content but has limited editing capabilities
        var approverRole = Roles.FirstOrDefault(r => r.NormalizedName == "APPROVER");
        if (approverRole == null)
        {
            approverRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Approver",
                NormalizedName = "APPROVER"
            };
            Roles.Add(approverRole);
        }

        // Approver permissions
        var approverPermissions = new[]
        {
            Piranha.Security.Permission.PagePreview,
            Piranha.Security.Permission.PostPreview,
            Piranha.Manager.Permission.Pages,
            Piranha.Manager.Permission.PagesEdit,
            Piranha.Manager.Permission.PagesSave,
            Piranha.Manager.Permission.PagesPublish,
            Piranha.Manager.Permission.Posts,
            Piranha.Manager.Permission.PostsEdit,
            Piranha.Manager.Permission.PostsSave,
            Piranha.Manager.Permission.PostsPublish,
            Piranha.Manager.Permission.Media,
            Piranha.Manager.Permission.Content,
            Piranha.Manager.Permission.ContentEdit,
            Piranha.Manager.Permission.Comments,
            Piranha.Manager.Permission.CommentsApprove,
            // Workflow permissions
            Piranha.Manager.WorkflowPermissions.ContentApprove,
            Piranha.Manager.WorkflowPermissions.PagesApprove,
            Piranha.Manager.WorkflowPermissions.PostsApprove,
            Piranha.Manager.WorkflowPermissions.ContentReview,
            Piranha.Manager.WorkflowPermissions.PagesReview,
            Piranha.Manager.WorkflowPermissions.PostsReview
        };

        foreach (var permissionName in approverPermissions)
        {
            var approverClaim = RoleClaims.FirstOrDefault(c =>
                c.RoleId == approverRole.Id && c.ClaimType == permissionName && c.ClaimValue == permissionName);
            if (approverClaim == null)
            {
                RoleClaims.Add(new IdentityRoleClaim<Guid>
                {
                    RoleId = approverRole.Id,
                    ClaimType = permissionName,
                    ClaimValue = permissionName
                });
            }
        }

        SaveChanges();
    }
}
