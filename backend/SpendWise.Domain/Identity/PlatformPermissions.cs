namespace SpendWise.Domain.Identity;

public enum PlatformPermission
{
    OperationsRead, SensitiveRead, ManageOrdinaryAdmins,
    ManageMaintenance, ManageConfiguration, ProvisionSuperAdmin
}

public static class PlatformPermissions
{
    public static bool Allows(SystemRole role, PlatformPermission permission) => permission switch
    {
        PlatformPermission.OperationsRead or PlatformPermission.SensitiveRead
            or PlatformPermission.ManageMaintenance => role == SystemRole.Admin,
        PlatformPermission.ManageOrdinaryAdmins => role == SystemRole.SuperAdmin,
        _ => false
    };
}
