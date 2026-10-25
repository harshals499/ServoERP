using System.Security.Cryptography;
using System.Text;

namespace ServoERP.Engineer;

public record FormPermission(bool CanView, bool CanCreate, bool CanEdit);
public interface IFormWorkspace
{
    IReadOnlyList<DemoForm> FormCatalog { get; }
    IReadOnlyDictionary<string, FormPermission> Permissions { get; }
    bool IsOffice { get; }
    string Caption { get; }
    List<DemoRecord> Records(string form);
    string Label(string id);
    Task Refresh(CancellationToken cancellationToken = default);
    Task<DemoRecord> SaveAsync(DemoRecord record, Guid operationId, CancellationToken cancellationToken = default);
}
public sealed class OfficeWorkspaceSnapshot
{
    public string CompanyName { get; set; } = "";
    public int CompanyId { get; set; }
    public List<DemoForm> Forms { get; set; } = [];
    public List<DemoRecord> Records { get; set; } = [];
    public Dictionary<string, FormPermission> Permissions { get; set; } = [];
}
public record OfficeSaveRequest(Guid OperationId, DemoRecord Record);
public static class WorkspaceIds
{
    public static Guid FromDatabase(string form, int id)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("ServoERP/workspace/" + form))[..16];
        BitConverter.GetBytes(id).CopyTo(bytes, 0); return new Guid(bytes);
    }
    public static int ToDatabase(string form, string value)
    {
        if (!Guid.TryParse(value, out var guid)) throw new FieldRuleException("Select a valid " + form + " record.");
        int id = BitConverter.ToInt32(guid.ToByteArray(), 0);
        if (id <= 0 || FromDatabase(form, id) != guid) throw new FieldRuleException("Select a valid " + form + " record.");
        return id;
    }
}
public sealed class OfficeFormWorkspace(EngineerTransport transport) : IFormWorkspace
{
    private sealed record SnapshotState(OfficeWorkspaceSnapshot Data,Dictionary<string,DemoRecord> ById);
    private SnapshotState _state = new(new(),new(StringComparer.OrdinalIgnoreCase));
    private readonly SemaphoreSlim _refresh = new(1, 1);
    public IReadOnlyList<DemoForm> FormCatalog => _state.Data.Forms;
    public IReadOnlyDictionary<string, FormPermission> Permissions => _state.Data.Permissions;
    public bool IsOffice => true;
    public string Caption => "OFFICE · " + _state.Data.CompanyName;
    public List<DemoRecord> Records(string form) => _state.Data.Records.Where(r => r.Form == form).Select(r => r.Copy()).ToList();
    public string Label(string id) => _state.ById.TryGetValue(id,out var record)?record.Title:"Not assigned";
    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        await _refresh.WaitAsync(cancellationToken);
        try { var snapshot=await transport.ReadAsync<OfficeWorkspaceSnapshot>("workspace", cancellationToken);_state=new(snapshot,snapshot.Records.ToDictionary(r=>r.Id.ToString(),StringComparer.OrdinalIgnoreCase)); }
        finally { _refresh.Release(); }
    }
    public async Task<DemoRecord> SaveAsync(DemoRecord record, Guid operationId, CancellationToken cancellationToken = default)
    {
        // Administrative saves are online-only. Failed requests are never silently queued as successful.
        var saved = await transport.WriteAsync<DemoRecord>("workspace/records", new OfficeSaveRequest(operationId, record), cancellationToken);
        await Refresh(cancellationToken); return saved;
    }
    public Task<byte[]> Document(int id, CancellationToken ct = default) => transport.ReadBytesAsync("workspace/documents/" + id, ct);
    public async Task Receive(DemoRecord record,Guid operationId,CancellationToken ct=default)
    { await transport.WriteAsync<DemoRecord>("workspace/receive",new OfficeSaveRequest(operationId,record),ct); await Refresh(ct); }
}
