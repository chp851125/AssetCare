using System.Net.Mail;
using System.Text.RegularExpressions;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public partial class VendorService(
    IRepository<Vendor> vendors,
    IRepository<WorkOrder> workOrders,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit) : IVendorService
{
    private const int PageSize = 10;

    [GeneratedRegex("^[0-9]{8}$")]
    private static partial Regex TaxIdPattern();

    public PagedResult<VendorListItem> Search(VendorQuery query)
    {
        currentUser.RequireManager();
        // 尚在外送中的工單數
        var outsourced = workOrders.GetAll()
            .Where(w => w.VendorId != null && w.ReturnedDate == null && WorkOrderStateMachine.IsActive(w.Status))
            .CountBy(w => w.VendorId!.Value).ToDictionary();

        IEnumerable<Vendor> source = vendors.GetAll();
        if (!query.IncludeInactive) source = source.Where(v => v.IsActive);
        if (query.Keyword?.Trim() is { Length: > 0 } keyword)
            source = source.Where(v => new[] { v.Name, v.TaxId, v.ContactName, v.Phone, v.ServiceScope }
                .Any(x => x?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));

        var items = source.OrderByDescending(v => v.IsActive).ThenBy(v => v.Name)
            .Select(v => new VendorListItem(v.Id, v.Name, v.TaxId, v.ContactName, v.Phone, v.Email, v.ServiceScope,
                v.IsActive, outsourced.GetValueOrDefault(v.Id)))
            .ToList();
        return PagedResult<VendorListItem>.From(items, query.Page, PageSize);
    }

    public VendorEditData GetForEdit(Guid id)
    {
        currentUser.RequireManager();
        var v = Find(id);
        return new VendorEditData(v.Id, v.Version, new VendorInput(v.Name, v.TaxId, v.ContactName, v.Phone, v.Email,
            v.Address, v.ServiceScope, v.IsActive));
    }

    public void Create(VendorInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var vendor = new Vendor();
            Apply(vendor, input);
            vendors.Add(vendor);
            audit.Record(AuditActions.Create, nameof(Vendor), vendor.Id, vendor.Name, "新增廠商");
        });
    }

    public void Update(Guid id, int version, VendorInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var vendor = Find(id);
            Apply(vendor, input);
            vendor.Version = version;
            vendors.Update(vendor);
            audit.Record(AuditActions.Update, nameof(Vendor), vendor.Id, vendor.Name, "修改廠商資料");
        });
    }

    private Vendor Find(Guid id) => vendors.GetById(id) ?? throw new NotFoundException("找不到廠商。");

    private void Apply(Vendor vendor, VendorInput input)
    {
        var name = InputText.Required(input.Name, "廠商名稱", 100);
        if (vendors.GetAll().Any(v => v.Id != vendor.Id && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException($"廠商「{name}」已存在。");
        var taxId = InputText.Optional(input.TaxId, "統一編號", 8);
        if (taxId is not null && !TaxIdPattern().IsMatch(taxId)) throw new BusinessRuleException("統一編號須為 8 碼數字。");
        var email = InputText.Optional(input.Email, "Email", 100);
        if (email is not null && (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
            throw new BusinessRuleException("Email 格式不正確。");

        vendor.Name = name;
        vendor.TaxId = taxId;
        vendor.ContactName = InputText.Optional(input.ContactName, "聯絡人", 50);
        vendor.Phone = InputText.Optional(input.Phone, "電話", 30);
        vendor.Email = email;
        vendor.Address = InputText.Optional(input.Address, "地址", 200);
        vendor.ServiceScope = InputText.Optional(input.ServiceScope, "服務範圍", 200);
        vendor.IsActive = input.IsActive;
    }
}
