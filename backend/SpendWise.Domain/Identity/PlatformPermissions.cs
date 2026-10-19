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
        PlatformPermission.OperationsRead or PlatformPermission.SensitiveRead =>
            role is SystemRole.Admin or SystemRole.SuperAdmin,
        PlatformPermission.ManageOrdinaryAdmins or PlatformPermission.ManageMaintenance
            or PlatformPermission.ManageConfiguration => role == SystemRole.SuperAdmin,
        // Owner verification has no approved bootstrap contract. No platform role substitutes for it.
        _ => false
    };
}
