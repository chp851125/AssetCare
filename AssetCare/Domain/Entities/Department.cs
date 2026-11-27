namespace AssetCare.Domain.Entities;

public class Department : BaseEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = true;
}
