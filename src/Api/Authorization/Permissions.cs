namespace Api.Authorization;

/// <summary>
/// Permission codes issued by auth-service in the JWT "permissions" claim (<c>resource:action</c>).
/// Each code is also the name of an authorization policy, so controllers use <c>[Authorize(Policy = Permissions.OrdersView)]</c>.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permissions";

    public const string OrdersView = "orders:view";
    public const string OrdersCreate = "orders:create";
    public const string OrdersEdit = "orders:edit";
    public const string OrdersDelete = "orders:delete";

    /// <summary>System configuration (the order number prefix). auth-service grants it to admin only by default.</summary>
    public const string SettingsView = "settings:view";
    public const string SettingsEdit = "settings:edit";

    public static readonly string[] All = { OrdersView, OrdersCreate, OrdersEdit, OrdersDelete, SettingsView, SettingsEdit };
}
