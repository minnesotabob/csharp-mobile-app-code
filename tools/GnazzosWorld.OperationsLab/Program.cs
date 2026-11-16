using GnazzosWorld.Core.Operations;

if(args is ["staging-smoke",var api,var artifact,var evidence]){await StagingSmoke.Run(api,artifact,evidence);return;}
if(Environment.GetEnvironmentVariable("GNAZZO_SYNTHETIC_FIXTURES")!="1")throw new InvalidOperationException("Explicit local synthetic fixture opt-in is required.");
switch(args)
{
    case ["init",var path]:CommerceMaintenance.Initialize(path);Console.WriteLine("Synthetic database initialized at schema 1.");break;
    case ["migrate",var path]:CommerceMaintenance.Upgrade(path);Console.WriteLine("Synthetic database migration verified.");break;
    case ["backup",var source,var destination]:var backup=CommerceMaintenance.Backup(source,destination);Console.WriteLine($"Synthetic backup verified: schema {backup.DatabaseVersion}, {backup.Length} bytes.");break;
    case ["restore",var source,var destination]:var state=CommerceMaintenance.Restore(source,destination);Console.WriteLine($"Restored into new directory; readiness held. Review generation {state.Generation}.");break;
    case ["release-restored",var path,var generation]:CommerceMaintenance.ReleaseRestoredForLab(path,generation);Console.WriteLine("Synthetic restore hold released after explicit local review.");break;
    default:Console.Error.WriteLine("Synthetic operations: init DB | migrate DB | backup DB NEW_DIR | restore BACKUP_DIR NEW_DIR | release-restored DB GENERATION | staging-smoke API_DLL ORIGINAL_WAV NEW_EVIDENCE_JSON");Environment.ExitCode=2;break;
}
