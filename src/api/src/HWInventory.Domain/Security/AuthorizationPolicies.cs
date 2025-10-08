namespace HWInventory.Domain.Security;

public static class AuthorizationPolicies
{
    public const string ServersRead = "Servers.Read";
    public const string ServersManage = "Servers.Manage";

    public const string NetworkRead = "Network.Read";
    public const string NetworkManage = "Network.Manage";

    public const string WorkstationsRead = "Workstations.Read";
    public const string WorkstationsManage = "Workstations.Manage";

    public const string DictionariesManage = "Dictionaries.Manage";
    public const string AuditRead = "Audit.Read";
    public const string LabelsManage = "Labels.Manage";
    public const string ModulesManage = "Modules.Manage";
    public const string DashboardView = "Dashboard.View";
    public const string SecurityManage = "Security.Manage";
    public const string SessionsManage = "Security.Sessions.Manage";
    public const string ObservabilityManage = "Observability.Manage";
    public const string MaintenanceManage = "Maintenance.Manage";
    public const string ImportExportManage = "ImportExport.Manage";
}
