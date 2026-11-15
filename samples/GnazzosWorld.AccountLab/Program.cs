using GnazzosWorld.Core.Identity;

var directory = Path.Combine(Path.GetTempPath(), "gnazzo-account-lab-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    using var workspace = new AccountWorkspace(Path.Combine(directory, "workspace.db"), TimeProvider.System);
    var ownerA = new AccountOwner(new("https://fixture.auth0.com/"), "reader-a");
    var ownerB = new AccountOwner(new("https://fixture.auth0.com/"), "reader-b");
    var a = workspace.Activate(ownerA);
    workspace.SaveProfile(a, new("Anniversary reader", true, false), 0);
    Console.WriteLine($"A sync eligible: {workspace.CanSynchronize(a)}");
    var export = workspace.ExportLocalProfile(a);
    if (!export.Contains("Anniversary reader")) throw new InvalidOperationException("Missing local profile export.");
    var b = workspace.Activate(ownerB);
    Console.WriteLine($"B starts empty: {workspace.ReadProfile(b).Revision == 0}");
    Console.WriteLine($"Old A handle rejected: {!workspace.IsCurrent(a)}");
    a = workspace.Activate(ownerA);
    var operation = workspace.QueueRequest(a, LifecycleKind.Deletion);
    Console.WriteLine($"Pending removal blocks sync: {!workspace.CanSynchronize(a)}");
    Console.WriteLine($"Retry keeps operation: {operation.Id == workspace.QueueRequest(a, operation.Kind).Id}");
    workspace.ApplyReceipt(a, operation.Id, operation.Kind, LifecycleState.ReauthenticationRequired);
    Console.WriteLine("SIMULATED service: reauthentication required; no remote account changed.");
    workspace.ApplyReceipt(a, operation.Id, operation.Kind, LifecycleState.Accepted);
    Console.WriteLine($"SIMULATED accepted; local profile retained: {workspace.ReadProfile(a).Revision == 1}");
    workspace.ApplyReceipt(a, operation.Id, operation.Kind, LifecycleState.Completed);
    Console.WriteLine($"SIMULATED completed; local receipt: {workspace.GetOperation(a, operation.Kind)!.State}");
    b = workspace.Activate(ownerB);
    Console.WriteLine($"B unaffected: {workspace.ReadProfile(b).Revision == 0}");
    Console.WriteLine("No live identity, export service, provider removal or physical erasure was tested.");
}
finally { Directory.Delete(directory, recursive: true); }
