using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Linq.Expressions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Common;

internal static class InventoryQueryFilters
{
    private static readonly StringComparer FieldComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> StringOperators = new(new[] { "eq", "neq", "contains", "in" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> GuidOperators = new(new[] { "eq", "neq", "in" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> EnumOperators = new(new[] { "eq", "neq", "in" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> DateAfterOperators = new(new[] { "eq", "gte", "gt" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> DateBeforeOperators = new(new[] { "eq", "lte", "lt" }, StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<FilterCriterion> Parse(string? filterJson, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(filterJson))
        {
            return Array.Empty<FilterCriterion>();
        }

        var results = new List<FilterCriterion>();

        try
        {
            using var document = JsonDocument.Parse(filterJson);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!element.TryGetProperty("field", out var fieldElement) || fieldElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var field = fieldElement.GetString();
                    if (string.IsNullOrWhiteSpace(field))
                    {
                        continue;
                    }

                    var op = element.TryGetProperty("op", out var opElement) && opElement.ValueKind == JsonValueKind.String
                        ? opElement.GetString()
                        : "eq";

                    var values = element.TryGetProperty("value", out var valueElement)
                        ? ExtractValues(valueElement)
                        : Array.Empty<string>();

                    results.Add(new FilterCriterion(field, op ?? "eq", values));
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    var values = ExtractValues(property.Value);
                    results.Add(new FilterCriterion(property.Name, "eq", values));
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse report/export filters – ignoring invalid JSON");
        }

        return results;
    }

    internal static IReadOnlyList<string> ValidateForExportScope(ExportScope scope, IReadOnlyList<FilterCriterion> filters)
        => scope switch
        {
            ExportScope.Servers => ValidateServerFilters(filters, "export"),
            ExportScope.NetworkDevices => ValidateNetworkFilters(filters, "export"),
            ExportScope.Workstations => ValidateWorkstationFilters(filters, "export"),
            ExportScope.AuditLogs => ValidateAuditFilters(filters, "export"),
            _ => Array.Empty<string>()
        };

    internal static IReadOnlyList<string> ValidateForReportScope(ReportScope scope, IReadOnlyList<FilterCriterion> filters)
        => scope switch
        {
            ReportScope.Servers => ValidateServerFilters(filters, "report"),
            ReportScope.NetworkDevices => ValidateNetworkFilters(filters, "report"),
            ReportScope.Workstations => ValidateWorkstationFilters(filters, "report"),
            ReportScope.Audit => ValidateAuditFilters(filters, "report"),
            _ => Array.Empty<string>()
        };

    internal static IQueryable<Server> ApplyServerFilters(IQueryable<Server> query, IReadOnlyList<FilterCriterion> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.Values.Count == 0)
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];

            if (FieldComparer.Equals(filter.Field, "status") &&
                Enum.TryParse<EntityStatus>(firstValue, true, out var status))
            {
                if (TryParseEnumList<EntityStatus>(filter.Values, out var statuses))
                {
                    query = ApplyEnumFilter(query, statuses, op, x => x.Status);
                }
            }
            else if (FieldComparer.Equals(filter.Field, "environmentId") && TryParseGuidList(filter.Values, out var ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.EnvironmentId);
            }
            else if (FieldComparer.Equals(filter.Field, "wsusPriorityId") && TryParseGuidList(filter.Values, out ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.WsusPriorityId);
            }
            else if (FieldComparer.Equals(filter.Field, "operatingSystemId") && TryParseGuidList(filter.Values, out ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.OperatingSystemId);
            }
            else if (FieldComparer.Equals(filter.Field, "serverRoleId") && TryParseGuidList(filter.Values, out ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.ServerRoleId);
            }
            else if (FieldComparer.Equals(filter.Field, "locationId") && TryParseGuidList(filter.Values, out ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.LocationId);
            }
            else if (FieldComparer.Equals(filter.Field, "name"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Name);
            }
            else if (FieldComparer.Equals(filter.Field, "inventoryNumber"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.InventoryNumber);
            }
            else if (FieldComparer.Equals(filter.Field, "manufacturer"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Manufacturer);
            }
            else if (FieldComparer.Equals(filter.Field, "model"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Model);
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedAfter") && TryParseDate(firstValue, out var dateAfter))
            {
                query = query.Where(x => x.PurchasedAt != null && x.PurchasedAt >= dateAfter);
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedBefore") && TryParseDate(firstValue, out var dateBefore))
            {
                query = query.Where(x => x.PurchasedAt != null && x.PurchasedAt <= dateBefore);
            }
        }

        return query;
    }

    private static IReadOnlyList<string> ValidateServerFilters(IReadOnlyList<FilterCriterion> filters, string context)
    {
        var errors = new List<string>();

        foreach (var filter in filters)
        {
            if (!EnsureHasValues(filter, errors))
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];
            var handled = false;

            if (FieldComparer.Equals(filter.Field, "status"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, EnumOperators, errors))
                {
                    continue;
                }

                if (!TryParseEnumList<EntityStatus>(filter.Values, out _))
                {
                    errors.Add("Hodnoty filtru 'status' musí odpovídat stavům Active, Retired nebo Pending.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "environmentId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'environmentId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "wsusPriorityId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'wsusPriorityId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "operatingSystemId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'operatingSystemId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "serverRoleId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'serverRoleId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "locationId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'locationId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "name") ||
                     FieldComparer.Equals(filter.Field, "inventoryNumber") ||
                     FieldComparer.Equals(filter.Field, "manufacturer") ||
                     FieldComparer.Equals(filter.Field, "model"))
            {
                handled = true;
                EnsureOperator(filter.Field, op, StringOperators, errors);
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedAfter"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, DateAfterOperators, errors))
                {
                    continue;
                }

                if (!TryParseDate(firstValue, out _))
                {
                    errors.Add("Filtr 'purchasedAfter' musí obsahovat platné datum ve formátu RRRR-MM-DD.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedBefore"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, DateBeforeOperators, errors))
                {
                    continue;
                }

                if (!TryParseDate(firstValue, out _))
                {
                    errors.Add("Filtr 'purchasedBefore' musí obsahovat platné datum ve formátu RRRR-MM-DD.");
                }
            }

            if (!handled)
            {
                errors.Add($"Pole '{filter.Field}' není pro {context} serverů podporováno.");
            }
        }

        return errors;
    }

    internal static IQueryable<NetworkDevice> ApplyNetworkFilters(IQueryable<NetworkDevice> query, IReadOnlyList<FilterCriterion> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.Values.Count == 0)
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];

            if (FieldComparer.Equals(filter.Field, "status") &&
                Enum.TryParse<EntityStatus>(firstValue, true, out var status))
            {
                if (TryParseEnumList<EntityStatus>(filter.Values, out var statuses))
                {
                    query = ApplyEnumFilter(query, statuses, op, x => x.Status);
                }
            }
            else if (FieldComparer.Equals(filter.Field, "deviceTypeId") && TryParseGuidList(filter.Values, out var ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.DeviceTypeId);
            }
            else if (FieldComparer.Equals(filter.Field, "locationId") && TryParseGuidList(filter.Values, out ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.LocationId);
            }
            else if (FieldComparer.Equals(filter.Field, "name"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Name);
            }
            else if (FieldComparer.Equals(filter.Field, "inventoryNumber"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.InventoryNumber);
            }
            else if (FieldComparer.Equals(filter.Field, "manufacturer"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Manufacturer);
            }
            else if (FieldComparer.Equals(filter.Field, "model"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Model);
            }
        }

        return query;
    }

    private static IReadOnlyList<string> ValidateNetworkFilters(IReadOnlyList<FilterCriterion> filters, string context)
    {
        var errors = new List<string>();

        foreach (var filter in filters)
        {
            if (!EnsureHasValues(filter, errors))
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];
            var handled = false;

            if (FieldComparer.Equals(filter.Field, "status"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, EnumOperators, errors))
                {
                    continue;
                }

                if (!TryParseEnumList<EntityStatus>(filter.Values, out _))
                {
                    errors.Add("Hodnoty filtru 'status' musí odpovídat stavům Active, Retired nebo Pending.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "deviceTypeId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'deviceTypeId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "locationId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'locationId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "name") ||
                     FieldComparer.Equals(filter.Field, "inventoryNumber") ||
                     FieldComparer.Equals(filter.Field, "manufacturer") ||
                     FieldComparer.Equals(filter.Field, "model"))
            {
                handled = true;
                EnsureOperator(filter.Field, op, StringOperators, errors);
            }

            if (!handled)
            {
                errors.Add($"Pole '{filter.Field}' není pro {context} síťových prvků podporováno.");
            }
        }

        return errors;
    }

    internal static IQueryable<Workstation> ApplyWorkstationFilters(IQueryable<Workstation> query, IReadOnlyList<FilterCriterion> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.Values.Count == 0)
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];

            if (FieldComparer.Equals(filter.Field, "status") &&
                Enum.TryParse<EntityStatus>(firstValue, true, out var status))
            {
                if (TryParseEnumList<EntityStatus>(filter.Values, out var statuses))
                {
                    query = ApplyEnumFilter(query, statuses, op, x => x.Status);
                }
            }
            else if (FieldComparer.Equals(filter.Field, "operatingSystemId") && TryParseGuidList(filter.Values, out var ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.OperatingSystemId);
            }
            else if (FieldComparer.Equals(filter.Field, "workstationTypeId") && TryParseGuidList(filter.Values, out ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.WorkstationTypeId);
            }
            else if (FieldComparer.Equals(filter.Field, "locationId") && TryParseGuidList(filter.Values, out ids))
            {
                query = ApplyGuidFilter(query, ids, op, x => x.LocationId);
            }
            else if (FieldComparer.Equals(filter.Field, "ownerDepartment"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.OwnerDepartment, allowNull: true);
            }
            else if (FieldComparer.Equals(filter.Field, "ownerDisplayName"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.OwnerDisplayName, allowNull: true);
            }
            else if (FieldComparer.Equals(filter.Field, "name"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Name);
            }
            else if (FieldComparer.Equals(filter.Field, "inventoryNumber"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.InventoryNumber);
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedAfter") && TryParseDate(firstValue, out var after))
            {
                query = query.Where(x => x.PurchasedAt != null && x.PurchasedAt >= after);
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedBefore") && TryParseDate(firstValue, out var before))
            {
                query = query.Where(x => x.PurchasedAt != null && x.PurchasedAt <= before);
            }
        }

        return query;
    }

    private static IReadOnlyList<string> ValidateWorkstationFilters(IReadOnlyList<FilterCriterion> filters, string context)
    {
        var errors = new List<string>();

        foreach (var filter in filters)
        {
            if (!EnsureHasValues(filter, errors))
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];
            var handled = false;

            if (FieldComparer.Equals(filter.Field, "status"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, EnumOperators, errors))
                {
                    continue;
                }

                if (!TryParseEnumList<EntityStatus>(filter.Values, out _))
                {
                    errors.Add("Hodnoty filtru 'status' musí odpovídat stavům Active, Retired nebo Pending.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "operatingSystemId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'operatingSystemId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "workstationTypeId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'workstationTypeId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "locationId"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, GuidOperators, errors))
                {
                    continue;
                }

                if (!TryParseGuidList(filter.Values, out _))
                {
                    errors.Add("Filtr 'locationId' vyžaduje platné GUID hodnoty.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "ownerDepartment") ||
                     FieldComparer.Equals(filter.Field, "ownerDisplayName") ||
                     FieldComparer.Equals(filter.Field, "name") ||
                     FieldComparer.Equals(filter.Field, "inventoryNumber"))
            {
                handled = true;
                EnsureOperator(filter.Field, op, StringOperators, errors);
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedAfter"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, DateAfterOperators, errors))
                {
                    continue;
                }

                if (!TryParseDate(firstValue, out _))
                {
                    errors.Add("Filtr 'purchasedAfter' musí obsahovat platné datum ve formátu RRRR-MM-DD.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "purchasedBefore"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, DateBeforeOperators, errors))
                {
                    continue;
                }

                if (!TryParseDate(firstValue, out _))
                {
                    errors.Add("Filtr 'purchasedBefore' musí obsahovat platné datum ve formátu RRRR-MM-DD.");
                }
            }

            if (!handled)
            {
                errors.Add($"Pole '{filter.Field}' není pro {context} pracovních stanic podporováno.");
            }
        }

        return errors;
    }

    internal static IQueryable<AuditLog> ApplyAuditFilters(IQueryable<AuditLog> query, IReadOnlyList<FilterCriterion> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.Values.Count == 0)
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];

            if (FieldComparer.Equals(filter.Field, "entityType"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.EntityType);
            }
            else if (FieldComparer.Equals(filter.Field, "action"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.Action, allowNull: true);
            }
            else if (FieldComparer.Equals(filter.Field, "performedBy"))
            {
                query = ApplyStringFilter(query, filter.Values, op, x => x.PerformedBy, allowNull: true);
            }
            else if (FieldComparer.Equals(filter.Field, "performedAfter") && TryParseDate(firstValue, out var after))
            {
                query = query.Where(x => x.PerformedAtUtc >= after);
            }
            else if (FieldComparer.Equals(filter.Field, "performedBefore") && TryParseDate(firstValue, out var before))
            {
                query = query.Where(x => x.PerformedAtUtc <= before);
            }
        }

        return query;
    }

    private static IReadOnlyList<string> ValidateAuditFilters(IReadOnlyList<FilterCriterion> filters, string context)
    {
        var errors = new List<string>();

        foreach (var filter in filters)
        {
            if (!EnsureHasValues(filter, errors))
            {
                continue;
            }

            var op = NormalizeOperator(filter.Operator);
            var firstValue = filter.Values[0];
            var handled = false;

            if (FieldComparer.Equals(filter.Field, "entityType") ||
                FieldComparer.Equals(filter.Field, "action") ||
                FieldComparer.Equals(filter.Field, "performedBy"))
            {
                handled = true;
                EnsureOperator(filter.Field, op, StringOperators, errors);
            }
            else if (FieldComparer.Equals(filter.Field, "performedAfter"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, DateAfterOperators, errors))
                {
                    continue;
                }

                if (!TryParseDate(firstValue, out _))
                {
                    errors.Add("Filtr 'performedAfter' musí obsahovat platné datum ve formátu RRRR-MM-DD.");
                }
            }
            else if (FieldComparer.Equals(filter.Field, "performedBefore"))
            {
                handled = true;
                if (!EnsureOperator(filter.Field, op, DateBeforeOperators, errors))
                {
                    continue;
                }

                if (!TryParseDate(firstValue, out _))
                {
                    errors.Add("Filtr 'performedBefore' musí obsahovat platné datum ve formátu RRRR-MM-DD.");
                }
            }

            if (!handled)
            {
                errors.Add($"Pole '{filter.Field}' není pro {context} auditního logu podporováno.");
            }
        }

        return errors;
    }

    private static string NormalizeOperator(string? value)
        => string.IsNullOrWhiteSpace(value) ? "eq" : value.Trim().ToLowerInvariant();

    private static IReadOnlyList<string> ExtractValues(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                var list = new List<string>();
                foreach (var item in element.EnumerateArray())
                {
                    if (TryConvertToString(item, out var converted))
                    {
                        list.Add(converted);
                    }
                }
                return list;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return Array.Empty<string>();
            default:
                return TryConvertToString(element, out var single)
                    ? new[] { single }
                    : Array.Empty<string>();
        }
    }

    private static bool TryConvertToString(JsonElement element, out string value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                value = element.GetString() ?? string.Empty;
                return true;
            case JsonValueKind.Number:
                value = element.ToString();
                return true;
            case JsonValueKind.True:
            case JsonValueKind.False:
                value = element.GetBoolean().ToString();
                return true;
            default:
                value = element.GetRawText();
                return true;
        }
    }

    private static bool TryParseGuidList(IReadOnlyList<string> values, out List<Guid> guids)
    {
        guids = new List<Guid>();
        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                guids.Add(parsed);
            }
        }

        return guids.Count > 0;
    }

    private static bool TryParseDate(string value, out DateTime parsed)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed);

    private static IQueryable<TEntity> ApplyGuidFilter<TEntity>(IQueryable<TEntity> query, IReadOnlyList<Guid> values, string op, Expression<Func<TEntity, Guid>> selector)
    {
        var nonEmpty = values.Where(x => x != Guid.Empty).ToList();
        if (nonEmpty.Count == 0)
        {
            return query;
        }

        var parameter = selector.Parameters[0];
        Expression body;

        switch (op)
        {
            case "neq":
                body = Expression.NotEqual(selector.Body, Expression.Constant(nonEmpty[0]));
                break;
            case "in":
                var listGuid = Expression.Constant(nonEmpty);
                var containsGuid = typeof(List<Guid>).GetMethod(nameof(List<Guid>.Contains), new[] { typeof(Guid) })!;
                body = Expression.Call(listGuid, containsGuid, selector.Body);
                break;
            default:
                body = Expression.Equal(selector.Body, Expression.Constant(nonEmpty[0]));
                break;
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private static IQueryable<TEntity> ApplyEnumFilter<TEntity, TEnum>(IQueryable<TEntity> query, IReadOnlyList<TEnum> values, string op, Expression<Func<TEntity, TEnum>> selector)
        where TEnum : struct
    {
        if (values.Count == 0)
        {
            return query;
        }

        var parameter = selector.Parameters[0];
        Expression body;

        switch (op)
        {
            case "neq":
                body = Expression.NotEqual(selector.Body, Expression.Constant(values[0]));
                break;
            case "in":
                var listEnum = Expression.Constant(values.ToList());
                var containsEnum = typeof(List<TEnum>).GetMethod(nameof(List<TEnum>.Contains), new[] { typeof(TEnum) })!;
                body = Expression.Call(listEnum, containsEnum, selector.Body);
                break;
            default:
                body = Expression.Equal(selector.Body, Expression.Constant(values[0]));
                break;
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private static IQueryable<TEntity> ApplyStringFilter<TEntity>(IQueryable<TEntity> query, IReadOnlyList<string> values, string op, Expression<Func<TEntity, string?>> selector, bool allowNull = false)
    {
        if (values.Count == 0)
        {
            return query;
        }

        var firstValue = values[0];
        var parameter = selector.Parameters[0];
        var property = selector.Body;
        Expression body;

        switch (op)
        {
            case "neq":
                var equals = Expression.Equal(property, Expression.Constant(firstValue, typeof(string)));
                body = allowNull
                    ? Expression.OrElse(Expression.Equal(property, Expression.Constant(null, typeof(string))), Expression.Not(equals))
                    : Expression.Not(equals);
                break;
            case "contains":
                var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
                var containsCall = Expression.Call(property, containsMethod, Expression.Constant(firstValue));
                body = allowNull ? Expression.AndAlso(notNull, containsCall) : containsCall;
                break;
            case "in":
                var listString = Expression.Constant(values.ToList());
                var containsString = typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!;
                var inCall = Expression.Call(listString, containsString, property);
                body = allowNull
                    ? Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, typeof(string))), inCall)
                    : inCall;
                break;
            default:
                var equal = Expression.Equal(property, Expression.Constant(firstValue, typeof(string)));
                body = allowNull
                    ? Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, typeof(string))), equal)
                    : equal;
                break;
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private static bool TryParseEnumList<TEnum>(IReadOnlyList<string> values, out List<TEnum> parsed)
        where TEnum : struct, Enum
    {
        parsed = new List<TEnum>();
        foreach (var value in values)
        {
            if (Enum.TryParse<TEnum>(value, true, out var item))
            {
                parsed.Add(item);
            }
        }

        return parsed.Count > 0;
    }

    private static bool EnsureHasValues(FilterCriterion filter, List<string> errors)
    {
        if (filter.Values.Count > 0)
        {
            return true;
        }

        errors.Add($"Filtr '{filter.Field}' musí obsahovat alespoň jednu hodnotu.");
        return false;
    }

    private static bool EnsureOperator(string field, string op, HashSet<string> allowed, List<string> errors)
    {
        if (allowed.Contains(op))
        {
            return true;
        }

        var allowedList = string.Join(", ", allowed.OrderBy(x => x));
        errors.Add($"Operátor '{op}' není pro filtr '{field}' podporován. Povolené hodnoty: {allowedList}.");
        return false;
    }

    internal readonly record struct FilterCriterion(string Field, string Operator, IReadOnlyList<string> Values);
}
