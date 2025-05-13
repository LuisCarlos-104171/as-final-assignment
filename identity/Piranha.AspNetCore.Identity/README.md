# Piranha.AspNetCore.Identity

Security module for Piranha CMS using AspNetCore Identity.

## Overview

This module provides identity and authorization capabilities for Piranha CMS based on ASP.NET Core Identity. It includes user management, role-based access control, and integration with the Piranha Manager interface.

## Role-Based Access Control

The module includes several predefined roles with different permission levels:

### SysAdmin

- **Access Level**: Full system access
- **Permissions**: All permissions within the system
- **Use Case**: Site administrators who need complete control over the CMS

### Writer

- **Access Level**: Content creation and editing
- **Permissions**:
  - View pages and posts
  - Edit pages and posts (cannot publish)
  - Save content as draft
  - Upload and edit media
  - Preview content
- **Use Case**: Content creators who draft articles, blog posts, and page content

### Editor

- **Access Level**: Content management and organization
- **Permissions**:
  - All Writer permissions
  - Create new pages and posts
  - Delete pages and posts (cannot publish)
  - Manage media files and folders
  - Manage comments
  - Organize content
- **Use Case**: Content managers who need to organize and curate site content

### Approver

- **Access Level**: Content review and publishing
- **Permissions**:
  - View and edit pages and posts
  - Publish pages and posts
  - Approve comments
  - Review media
- **Use Case**: Content reviewers and publishers who control what content gets published

## Permission Structure

Permissions in Piranha CMS are organized hierarchically. Each role is assigned specific permissions that grant access to different areas and functions of the CMS:

- Content permissions (pages, posts)
- Media permissions
- Comment permissions
- System permissions

## Implementation

Roles are implemented using ASP.NET Core Identity roles with claims-based authorization. Each permission is represented as a claim that can be assigned to a role.

## Adding Users to Roles

To add a user to a role:

1. Navigate to the Manager interface (`~/manager`)
2. Go to System > Users
3. Edit a user or create a new one
4. Check the roles you want to assign to the user
5. Save

## Custom Roles

You can create custom roles with specific permission sets through the Manager interface:

1. Navigate to System > Roles
2. Click "Add" to create a new role
3. Provide a name for the role
4. Select the permissions to assign
5. Save

## Security Notes

- Always follow the principle of least privilege when assigning roles
- Regularly review user role assignments
- Consider using custom roles for specific workflows
- Update the default admin credentials for production environments