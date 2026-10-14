using System.Text.Json;
using SQLite;

namespace ServoERP.Engineer;

public class CachedJob
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string ServerJson { get; set; } = "";
    public string DraftJson { get; set; } = "";
    public bool Authorized { get; set; } = true;
}
public class PendingOperation
{
    [PrimaryKey] public string Id { get; set; } = "";
    [Indexed] public string JobId { get; set; } = "";
    public long Sequence { get; set; }
    public string PayloadJson { get; set; } = "";
    public QueueState State { get; set; }
    public int RetryCount { get; set; }
    public long NextAttemptTicks { get; set; }
    public string Error { get; set; } = "";
    public string LocalPhotoPath { get; set; } = "";
    public string UploadJson { get; set; } = "";
    public string ReceiptJson { get; set; } = "";
}
public class CachedValue
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Json { get; set; } = "";
}
public sealed class LocalWorkspace : IDisposable
{
    private readonly SQLiteConnection _db;
    private readonly object _gate = new();
    public LocalWorkspace(string path)
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
        _db.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
        _db.CreateTable<CachedJob>(); _db.CreateTable<PendingOperation>(); _db.CreateTable<CachedValue>();
        _db.Execute("UPDATE PendingOperation SET State=? WHERE State=?", QueueState.Pending, QueueState.Syncing);
    }
    public List<JobSnapshot> Jobs()
    {
        lock (_gate) return _db.Table<CachedJob>().Where(j => j.Authorized).ToList().Select(j => JsonSerializer.Deserialize<JobSnapshot>(j.DraftJson)!).ToList();
    }
    public JobSnapshot Job(Guid id)
    {
        lock (_gate) return JsonSerializer.Deserialize<JobSnapshot>(_db.Find<CachedJob>(id.ToString())?.DraftJson ?? throw new FieldRuleException("Sync this job before opening it offline."))!;
    }
    public void MergeSnapshots(List<JobSnapshot> jobs)
    {
        lock (_gate) _db.RunInTransaction(() =>
        {
            foreach (var existing in _db.Table<CachedJob>().ToList())
            {
                existing.Authorized = jobs.Any(j => j.PublicId.ToString() == existing.Id); _db.Update(existing);
            }
            foreach (var job in jobs)
            {
                job.Report.JobPublicId = job.PublicId;
                var key = job.PublicId.ToString(); var row = _db.Find<CachedJob>(key) ?? new CachedJob { Id = key };
                row.Authorized = true; row.ServerJson = JsonSerializer.Serialize(job);
                if (!_db.Table<PendingOperation>().Where(o => o.JobId == key && o.State != QueueState.Synced).Any()) row.DraftJson = row.ServerJson;
                _db.InsertOrReplace(row);
            }
        });
    }
    public void SaveValue<T>(string key, T value) { lock (_gate) _db.InsertOrReplace(new CachedValue { Key = key, Json = JsonSerializer.Serialize(value) }); }
    public T? Value<T>(string key) { lock (_gate) { var row = _db.Find<CachedValue>(key); return row is null ? default : JsonSerializer.Deserialize<T>(row.Json); } }
    public PendingOperation Enqueue(MobileOperation op, int actorId, string photoPath = "")
    {
        lock (_gate)
        {
            PendingOperation? queued = null;
            _db.RunInTransaction(() =>
            {
                var row = _db.Find<CachedJob>(op.JobPublicId.ToString()) ?? throw new FieldRuleException("Job is not cached.");
                if (!row.Authorized) throw new FieldRuleException("This job is no longer assigned to you. Your draft is retained for office review.");
                var job = JsonSerializer.Deserialize<JobSnapshot>(row.DraftJson)!;
                var server = JsonSerializer.Deserialize<JobSnapshot>(row.ServerJson)!;
                var prior = _db.Table<PendingOperation>().Where(p => p.JobId == row.Id && p.State != QueueState.Synced).OrderByDescending(p => p.Sequence).FirstOrDefault();
                op.BaseRevision = server.Report.Revision; op.OfficeVersion = server.OfficeVersion; op.OfficeFingerprint = server.OfficeFingerprint; op.PredecessorId = prior is null ? null : Guid.Parse(prior.Id);
                FieldRules.Apply(job.Report, op, actorId);
                queued = new PendingOperation { Id = op.OperationId.ToString(), JobId = row.Id, Sequence = (_db.Table<PendingOperation>().OrderByDescending(p => p.Sequence).FirstOrDefault()?.Sequence ?? 0) + 1, PayloadJson = JsonSerializer.Serialize(op), LocalPhotoPath = photoPath };
                row.DraftJson = JsonSerializer.Serialize(job); _db.Update(row); _db.Insert(queued);
            });
            return queued!;
        }
    }
    public List<PendingOperation> Queue() { lock (_gate) return _db.Table<PendingOperation>().OrderBy(p => p.Sequence).ToList(); }
    public void Update(PendingOperation op) { lock (_gate) _db.Update(op); }
    public void Acknowledge(PendingOperation pending, OperationReceipt receipt)
    {
        lock (_gate) _db.RunInTransaction(() =>
        {
            pending.State = QueueState.Synced; pending.Error = ""; pending.ReceiptJson = JsonSerializer.Serialize(receipt); _db.Update(pending);
            var row = _db.Find<CachedJob>(pending.JobId)!; var job = JsonSerializer.Deserialize<JobSnapshot>(row.ServerJson)!; job.Report = receipt.Report;
            row.ServerJson = JsonSerializer.Serialize(job);
            if (!_db.Table<PendingOperation>().Where(p => p.JobId == row.Id && p.State != QueueState.Synced).Any()) row.DraftJson = row.ServerJson;
            _db.Update(row);
        });
    }
    public void Retry(Guid id)
    {
        lock (_gate)
        {
            var row = _db.Find<PendingOperation>(id.ToString())!;
            if (row.State is QueueState.Conflict or QueueState.Failed) throw new FieldRuleException("Office review is required. Retrying cannot overwrite the office version.");
            row.NextAttemptTicks = 0; _db.Update(row);
        }
    }
    public void Dispose() { lock (_gate) _db.Dispose(); }
}
