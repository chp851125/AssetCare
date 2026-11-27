using System.Text.RegularExpressions;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Application.Services;

public partial class LocationService(
    IRepository<Location> locations,
    IRepository<Asset> assets,
    IRepository<AssetTransfer> transfers,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit) : ILocationService
{
    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{0,19}$")]
    private static partial Regex CodePattern();

    public IReadOnlyList<LocationNode> GetTree()
    {
        currentUser.RequireManager();
        var counts = assets.GetAll().Where(a => a.Status != AssetStatus.Scrapped)
            .CountBy(a => a.LocationId).ToDictionary();
        return new LocationHierarchy(locations.GetAll()).Ordered()
            .Select(n => new LocationNode(n.Location.Id, n.Location.Version, n.Location.Code, n.Location.Name, n.Depth,
                n.Location.IsActive, counts.GetValueOrDefault(n.Location.Id), DeleteBlocker(n.Location.Id) is null))
            .ToList();
    }

    public IReadOnlyList<Option> GetParentOptions(Guid? editingId)
    {
        currentUser.RequireManager();
        var hierarchy = new LocationHierarchy(locations.GetAll());
        var excluded = editingId is { } id ? hierarchy.SelfAndDescendants(id) : [];
        return hierarchy.Ordered().Where(n => !excluded.Contains(n.Location.Id))
            .Select(n => new Option(n.Location.Id, LocationHierarchy.Indent(n.Location.Name, n.Depth)))
            .ToList();
    }

    public LocationEditData GetForEdit(Guid id)
    {
        currentUser.RequireManager();
        var l = Find(id);
        return new LocationEditData(l.Id, l.Version,
            new LocationInput(l.Code, l.Name, l.ParentId, l.SortOrder, l.IsActive));
    }

    public void Create(LocationInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var location = new Location();
            Apply(location, input, isNew: true);
            locations.Add(location);
            audit.Record(AuditActions.Create, nameof(Location), location.Id, location.Name, "新增地點");
        });
    }

    public void Update(Guid id, int version, LocationInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var location = Find(id);
            Apply(location, input, isNew: false);
            location.Version = version;
            locations.Update(location);
            audit.Record(AuditActions.Update, nameof(Location), location.Id, location.Name, "修改地點");
        });
    }

    public void Delete(Guid id, int version)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var location = Find(id);
            if (DeleteBlocker(id) is { } reason) throw new BusinessRuleException(reason);
            location.Version = version;
            locations.Delete(location);
            audit.Record(AuditActions.Delete, nameof(Location), location.Id, location.Name, "刪除地點");
        });
    }

    private Location Find(Guid id) => locations.GetById(id) ?? throw new NotFoundException("找不到地點。");

    private string? DeleteBlocker(Guid id) =>
        locations.Query(l => l.ParentId == id).Count > 0 ? "此地點仍有下層地點，無法刪除。"
        : assets.Query(a => a.LocationId == id).Count > 0 ? "此地點已有資產使用，無法刪除，可改為停用。"
        : transfers.Query(t => t.FromLocationId == id || t.ToLocationId == id).Count > 0
            ? "此地點出現在轉移紀錄中，無法刪除，可改為停用。"
            : null;

    private void Apply(Location location, LocationInput input, bool isNew)
    {
        var code = InputText.Required(input.Code, "地點代碼", 20).ToUpperInvariant();
        if (!CodePattern().IsMatch(code)) throw new BusinessRuleException("地點代碼僅能使用英文、數字與連字號。");
        var name = InputText.Required(input.Name, "地點名稱", 50);
        var all = locations.GetAll();
        if (all.Any(l => l.Id != location.Id && string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException($"地點代碼 {code} 已存在。");
        if (input.SortOrder is < 0 or > 100000) throw new BusinessRuleException("排序值須為 0 到 100000。");

        if (input.ParentId is { } parentId)
        {
            var parent = all.FirstOrDefault(l => l.Id == parentId)
                         ?? throw new BusinessRuleException("找不到指定的上層地點。");
            if (!isNew && new LocationHierarchy(all).SelfAndDescendants(location.Id).Contains(parentId))
                throw new BusinessRuleException("上層地點不可為自身或下層地點。");
            if (input.IsActive && !parent.IsActive)
                throw new BusinessRuleException("上層地點已停用，下層地點無法啟用。");
        }

        if (!isNew && location.IsActive && !input.IsActive)
        {
            if (all.Any(l => l.ParentId == location.Id && l.IsActive))
                throw new BusinessRuleException("請先停用下層地點。");
            if (assets.Query(a => a.LocationId == location.Id && a.Status != AssetStatus.Scrapped).Count > 0)
                throw new BusinessRuleException("此地點仍有資產，請先轉移後再停用。");
        }

        location.Code = code;
        location.Name = name;
        location.ParentId = input.ParentId;
        location.SortOrder = input.SortOrder;
        location.IsActive = input.IsActive;
    }
}
