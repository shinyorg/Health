namespace Shiny.Health;


public static class Extensions
{
    public static Permission ToPermission(this HealthMetric metric, PermissionType type = PermissionType.Read)
        => new Permission(metric, type);
}

