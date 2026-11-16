using System.Security.Cryptography;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Operations;
using GnazzosWorld.Core.Orders;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class OperationsTests:IDisposable
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private readonly string root=Path.Combine(Path.GetTempPath(),"gnazzo-operations-tests-"+Guid.NewGuid().ToString("N"));private string Db=>Path.Combine(root,"commerce.sqlite3");
    private static readonly OrderOwner A=new("https://fixture.auth0.com/","reader-a");
    private static string Key()=>Guid.NewGuid().ToString("N");
    private static OrderSnapshot Accept(SqliteOrderStore s){var q=s.CreateQuote(A,new SongRequest{Genre="Folk",Occasion="Invented",Story="SYNTHETIC_PRIVATE_STORY",ContactName="Sample Reader",Email="reader@example.test"});return s.Accept(A,new(q.Id,Key(),q.Request));}
    [Fact] public void Explicit_initialization_and_legacy_migration_preserve_order_snapshot()
    {OrderSnapshot o;using(var legacy=new SqliteOrderStore(Db,TimeProvider.System))o=Accept(legacy);CommerceMaintenance.Upgrade(Db);Assert.False(CommerceMaintenance.ReadState(Db).RestorePending);CommerceMaintenance.Upgrade(Db);using var reopened=new SqliteOrderStore(Db,TimeProvider.System);Assert.Equal(o,reopened.Get(A,o.Id));Assert.Throws<IOException>(()=>CommerceMaintenance.Initialize(Db));}
    [Fact] public void Fresh_schema_initialization_is_versioned_and_ready_for_review()
    {CommerceMaintenance.Initialize(Db);Assert.False(CommerceMaintenance.ReadState(Db).RestorePending);Assert.Equal(1,Scalar(Db,"PRAGMA user_version"));}
    [Fact] public void Future_database_is_rejected_before_store_or_migration_changes_it()
    {CommerceMaintenance.Initialize(Db);Sql(Db,"PRAGMA user_version=2");var before=SHA256.HashData(File.ReadAllBytes(Db));Assert.Throws<InvalidDataException>(()=>new SqliteOrderStore(Db,TimeProvider.System));Assert.Throws<InvalidDataException>(()=>CommerceMaintenance.Upgrade(Db));Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(Db)));}
    [Fact] public void Unrelated_sqlite_file_is_not_silently_migrated()
    {Directory.CreateDirectory(root);Sql(Db,"CREATE TABLE unrelated(value TEXT)");Assert.Throws<InvalidDataException>(()=>CommerceMaintenance.Upgrade(Db));Assert.Equal(1,Scalar(Db,"SELECT COUNT(*) FROM sqlite_master WHERE type='table'"));}
    [Fact] public void Missing_empty_or_oversized_database_is_not_read_as_ready()
    {Assert.Throws<InvalidDataException>(()=>CommerceMaintenance.ReadState(Db));Directory.CreateDirectory(root);using(var file=File.Create(Db)){}Assert.Throws<InvalidDataException>(()=>CommerceMaintenance.ReadState(Db));using(var file=File.OpenWrite(Db))file.SetLength(CommerceMaintenance.MaxDatabaseBytes+1);Assert.Throws<InvalidDataException>(()=>CommerceMaintenance.ReadState(Db));}
    [Fact] public void Online_backup_reads_committed_WAL_and_restores_a_coherent_earlier_snapshot()
    {
        CommerceMaintenance.Initialize(Db);Sql(Db,"PRAGMA journal_mode=WAL");OrderSnapshot first,second;
        using(var active=new SqliteOrderStore(Db,TimeProvider.System)){first=Accept(active);var m=CommerceMaintenance.Backup(Db,Path.Combine(root,"backup"));Assert.Equal(1,m.DatabaseVersion);second=Accept(active);}
        var restore=CommerceMaintenance.Restore(Path.Combine(root,"backup"),Path.Combine(root,"restored"));var path=Path.Combine(root,"restored","commerce.sqlite3");Assert.True(restore.RestorePending);
        using(var reopened=new SqliteOrderStore(path,TimeProvider.System)){Assert.Equal(first,reopened.Get(A,first.Id));Assert.Null(reopened.Get(A,second.Id));Assert.Single(reopened.TrackOrder(A,first.Id).History);}Assert.True(CommerceMaintenance.ReadState(path).RestorePending);
    }
    [Fact] public void Restore_invalidates_grants_and_requires_exact_explicit_generation_release()
    {
        CommerceMaintenance.Initialize(Db);using(var s=new SqliteOrderStore(Db,TimeProvider.System)){var order=Accept(s);using var db=new SqliteConnection("Data Source="+Db+";Pooling=False");db.Open();using var c=db.CreateCommand();c.CommandText="INSERT INTO commerce_deliveries VALUES($delivery,$order,1,0); INSERT INTO commerce_delivery_grants VALUES($hash,$delivery,$issuer,$subject,1,$expires)";c.Parameters.AddWithValue("$delivery",Key());c.Parameters.AddWithValue("$order",order.Id);c.Parameters.AddWithValue("$hash",new string('a',64));c.Parameters.AddWithValue("$issuer",A.Issuer);c.Parameters.AddWithValue("$subject",A.Subject);c.Parameters.AddWithValue("$expires",DateTimeOffset.UtcNow.AddHours(1).UtcTicks);c.ExecuteNonQuery();}
        CommerceMaintenance.Backup(Db,Path.Combine(root,"backup"));var restored=CommerceMaintenance.Restore(Path.Combine(root,"backup"),Path.Combine(root,"restored"));var path=Path.Combine(root,"restored","commerce.sqlite3");Assert.Equal(0,Scalar(path,"SELECT COUNT(*) FROM commerce_delivery_grants"));Assert.Throws<InvalidOperationException>(()=>CommerceMaintenance.ReleaseRestoredForLab(path,Key()));Assert.True(CommerceMaintenance.ReadState(path).RestorePending);CommerceMaintenance.ReleaseRestoredForLab(path,restored.Generation);Assert.False(CommerceMaintenance.ReadState(path).RestorePending);Assert.Throws<InvalidOperationException>(()=>CommerceMaintenance.ReleaseRestoredForLab(path,restored.Generation));
    }
    [Theory][InlineData("bytes")][InlineData("manifest")]
    public void Changed_backup_is_rejected_without_publishing_restore(string changed)
    {CommerceMaintenance.Initialize(Db);var backup=Path.Combine(root,"backup");CommerceMaintenance.Backup(Db,backup);if(changed=="bytes"){using var stream=File.OpenWrite(Path.Combine(backup,"commerce.sqlite3"));stream.Position=40;stream.WriteByte(0xff);}else{var p=Path.Combine(backup,"backup.json");File.WriteAllText(p,File.ReadAllText(p).Replace("\"schema\":1","\"schema\":9"));}var destination=Path.Combine(root,"restored");Assert.Throws<InvalidDataException>(()=>CommerceMaintenance.Restore(backup,destination));Assert.False(Directory.Exists(destination));}
    [Fact] public void Existing_backup_or_restore_directory_is_never_overwritten()
    {CommerceMaintenance.Initialize(Db);var backup=Path.Combine(root,"backup");CommerceMaintenance.Backup(Db,backup);Assert.Throws<IOException>(()=>CommerceMaintenance.Backup(Db,backup));var destination=Path.Combine(root,"existing");Directory.CreateDirectory(destination);File.WriteAllText(Path.Combine(destination,"marker.txt"),"preserve");Assert.Throws<IOException>(()=>CommerceMaintenance.Restore(backup,destination));Assert.Equal("preserve",File.ReadAllText(Path.Combine(destination,"marker.txt")));}
    [Fact] public void Backup_manifest_does_not_contain_source_path_customer_or_credential_fields()
    {CommerceMaintenance.Initialize(Db);using(var s=new SqliteOrderStore(Db,TimeProvider.System))Accept(s);var dir=Path.Combine(root,"backup");CommerceMaintenance.Backup(Db,dir);var text=File.ReadAllText(Path.Combine(dir,"backup.json"));Assert.DoesNotContain(root,text);Assert.DoesNotContain("SYNTHETIC_PRIVATE_STORY",text);Assert.DoesNotContain("reader@example.test",text);Assert.Equal(5,JsonDocument.Parse(text).RootElement.EnumerateObject().Count());}
    [Fact] public void Local_diagnostics_default_off_and_disable_clears_bounded_projection()
    {var d=new SafeDiagnosticBuffer(TimeProvider.System);Assert.False(d.Record(DiagnosticArea.Storage,DiagnosticFault.Unexpected));Assert.Empty(d.Snapshot());d.SetConsent(true);for(var i=0;i<25;i++)Assert.True(d.Record(DiagnosticArea.Storage,DiagnosticFault.Unexpected));Assert.Equal(20,d.Snapshot().Count);d.SetConsent(false);Assert.Empty(d.Snapshot());Assert.False(d.Enabled);}
    [Fact] public void Local_diagnostics_accept_only_defined_categories_and_generated_identifiers()
    {var d=new SafeDiagnosticBuffer(TimeProvider.System,true);Assert.Throws<ArgumentOutOfRangeException>(()=>d.Record((DiagnosticArea)999,DiagnosticFault.Unexpected));Assert.True(d.Record(DiagnosticArea.Network,DiagnosticFault.Unavailable));var e=Assert.Single(d.Snapshot());Assert.True(OrderContract.Id(e.Id));Assert.DoesNotContain("Exception",JsonSerializer.Serialize(e));}
    [Fact] public async Task Newsletter_requires_separate_explicit_consent_and_pending_is_not_subscribed()
    {var gateway=new Gateway();var boundary=new NewsletterBoundary(gateway);var key=Key();await Assert.ThrowsAsync<InvalidOperationException>(()=>boundary.RequestAsync(key,"reader@example.test",false,Ct));Assert.Empty(gateway.Intents);Assert.Equal(NewsletterOutcome.PendingConfirmation,await boundary.RequestAsync(key,"reader@example.test",true,Ct));Assert.Equal(NewsletterOutcome.PendingConfirmation,await boundary.RequestAsync(key,"reader@example.test",true,Ct));Assert.Equal(gateway.Intents[0],gateway.Intents[1]);Assert.DoesNotContain("reader@example.test",gateway.Intents[0].ToString());}
    [Theory][InlineData("not-an-address")][InlineData("Display Name <reader@example.test>")][InlineData("reader@example.test\r\nprivate")][InlineData(null)]
    public async Task Newsletter_invalid_address_never_calls_gateway(string? address)
    {var gateway=new Gateway();await Assert.ThrowsAsync<InvalidDataException>(()=>new NewsletterBoundary(gateway).RequestAsync(Key(),address!,true,Ct));Assert.Empty(gateway.Intents);}
    [Fact] public async Task Newsletter_network_failure_remains_unconfirmed_without_raw_error_text()
    {var boundary=new NewsletterBoundary(new Gateway{Fail=true});Assert.Equal(NewsletterOutcome.Unconfirmed,await boundary.RequestAsync(Key(),"reader@example.test",true,Ct));}
    private sealed class Gateway:INewsletterGateway{public bool Fail;public List<NewsletterIntent> Intents{get;}=[];public Task<NewsletterOutcome> RequestConfirmationAsync(NewsletterIntent i,CancellationToken ct){Intents.Add(i);if(Fail)throw new HttpRequestException("PRIVATE_GATEWAY_ERROR");return Task.FromResult(NewsletterOutcome.PendingConfirmation);}}
    private static void Sql(string path,string sql){using var db=new SqliteConnection("Data Source="+path+";Pooling=False");db.Open();using var c=db.CreateCommand();c.CommandText=sql;c.ExecuteNonQuery();}
    private static long Scalar(string path,string sql){using var db=new SqliteConnection("Data Source="+path+";Pooling=False");db.Open();using var c=db.CreateCommand();c.CommandText=sql;return (long)c.ExecuteScalar()!;}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
