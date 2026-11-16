using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Fulfillment;
using GnazzosWorld.Core.Operations;
using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Payments;

internal static class StagingSmoke
{
    private const string QueryCanary="PRIVATE_QUERY_CANARY",HeaderCanary="PRIVATE_HEADER_CANARY",BodyCanary="PRIVATE_BODY_CANARY",ExceptionCanary="SYNTHETIC_EXCEPTION_PRIVATE_CANARY";
    public static async Task Run(string apiDll,string originalWav,string evidenceFile)
    {
        apiDll=Path.GetFullPath(apiDll);originalWav=Path.GetFullPath(originalWav);evidenceFile=Path.GetFullPath(evidenceFile);
        if(!File.Exists(apiDll)||!File.Exists(originalWav)||File.Exists(evidenceFile))throw new InvalidOperationException("Supply published API, original sample and a new evidence file.");
        using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=budget.Token;
        var work=Path.Combine(Path.GetTempPath(),"gnazzo-operations-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);Process? process=null;
        var captured=new StringBuilder();var gate=new object();var overflow=false;
        void Capture(object? sender,DataReceivedEventArgs e){if(e.Data is null)return;lock(gate){if(captured.Length+e.Data.Length>262144){overflow=true;return;}captured.AppendLine(e.Data);}}
        try
        {
            var source=Path.Combine(work,"source.sqlite3");CommerceMaintenance.Initialize(source);var owner=new OrderOwner("https://fixture.auth0.com/","reader-a");
            VerifiedCheckoutEvent paidEvent;OrderSnapshot order;DeliveryDescriptor delivery;DeliveryGrant oldGrant;
            using(var store=new SqliteOrderStore(source,TimeProvider.System))
            {
                (order,paidEvent)=Seed(store,owner);delivery=store.PublishDeliveryForLab(order.Id,3);oldGrant=store.IssueDeliveryGrant(owner,order.Id,delivery.Id,4);
                CommerceMaintenance.Backup(source,Path.Combine(work,"backup"));store.RevokeDeliveryForLab(order.Id,delivery.Id,4);
            }
            var restored=Path.Combine(work,"restored");var restore=CommerceMaintenance.Restore(Path.Combine(work,"backup"),restored);var restoredDb=Path.Combine(restored,"commerce.sqlite3");
            using(var store=new SqliteOrderStore(restoredDb,TimeProvider.System))
            {
                if(store.TrackOrder(owner,order.Id).Order.Version!=4||store.ApplyVerifiedTestEvent(paidEvent).State!=TestPaymentState.Paid||store.ListOrders(owner).Items.Count!=1)throw new InvalidDataException("Restored business evidence failed.");
                try{store.AuthorizeDelivery(owner,order.Id,delivery.Id,oldGrant.Token);throw new InvalidDataException("Restored grant remained valid.");}catch(OrderRejectedException ex) when(ex.Problem==OrderProblem.Unavailable){}
            }
            var artifacts=Path.Combine(work,"private-artifacts");Directory.CreateDirectory(artifacts);File.Copy(originalWav,Path.Combine(artifacts,"delivery_sample.wav"));
            using var rsa=RSA.Create(2048);var certRequest=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(IPAddress.Loopback);certRequest.CertificateExtensions.Add(san.Build());
            certRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
            using var cert=certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));var fingerprint=SHA256.HashData(cert.RawData);var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));var pfx=Path.Combine(work,"loopback.pfx");File.WriteAllBytes(pfx,cert.Export(X509ContentType.Pfx,password));
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();var origin=new Uri($"https://127.0.0.1:{port}/");
            var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.GetDirectoryName(apiDll)!,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(apiDll);
            start.Environment.Clear();foreach(var key in new[]{"PATH","SystemRoot","TEMP","TMP","HOME","USERPROFILE","DOTNET_ROOT","DOTNET_MULTILEVEL_LOOKUP"})if(Environment.GetEnvironmentVariable(key) is { } value)start.Environment[key]=value;
            foreach(var pair in new Dictionary<string,string>{{"ASPNETCORE_ENVIRONMENT","Staging"},{"DOTNET_ENVIRONMENT","Staging"},{"ASPNETCORE_URLS",origin.AbsoluteUri},{"ASPNETCORE_Kestrel__Certificates__Default__Path",pfx},{"ASPNETCORE_Kestrel__Certificates__Default__Password",password},{"Operations__Enabled","true"},{"Operations__FailureProbe","true"},{"Operations__TracePercent","100"},{"Operations__ArtifactsRoot",artifacts},{"Commerce__Database",restoredDb},{"Commerce__Enabled","false"},{"Payments__Enabled","false"},{"Fulfillment__Enabled","false"}})start.Environment[pair.Key]=pair.Value;
            process=new Process{StartInfo=start};process.OutputDataReceived+=Capture;process.ErrorDataReceived+=Capture;if(!process.Start())throw new InvalidOperationException("Staging process did not start.");process.BeginOutputReadLine();process.BeginErrorReadLine();
            using var handler=new HttpClientHandler{AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(request,certificate,_,errors)=>request.RequestUri is {IsLoopback:true}&&certificate is not null&&(errors&~SslPolicyErrors.RemoteCertificateChainErrors)==0&&CryptographicOperations.FixedTimeEquals(fingerprint,SHA256.HashData(certificate.RawData))};
            using var client=new HttpClient(handler){BaseAddress=origin,Timeout=TimeSpan.FromSeconds(5)};
            var deadline=Stopwatch.StartNew();var live=false;
            while(deadline.Elapsed<TimeSpan.FromSeconds(40)&&!process.HasExited)
            {try{using var response=await client.GetAsync("health/live",ct);if(response.StatusCode==HttpStatusCode.OK){live=true;break;}}catch(HttpRequestException){}await Task.Delay(150,ct);}
            if(!live)throw new InvalidOperationException("Published HTTPS staging process failed to become live.");
            using(var held=await client.GetAsync("health/ready",ct))if(held.StatusCode!=HttpStatusCode.ServiceUnavailable)throw new InvalidDataException("Restore hold was not enforced.");
            CommerceMaintenance.ReleaseRestoredForLab(restoredDb,restore.Generation);
            using(var ready=await client.GetAsync("health/ready",ct))if(ready.StatusCode!=HttpStatusCode.OK)throw new InvalidDataException("Reviewed restore did not become ready.");
            using var failure=new HttpRequestMessage(HttpMethod.Post,"ops/lab-failure?query="+QueryCanary){Content=new StringContent(BodyCanary,Encoding.UTF8,"application/json")};failure.Headers.Add("X-Correlation-Id",HeaderCanary);failure.Headers.Add("X-Delivery-Grant",HeaderCanary);failure.Headers.Authorization=new("Bearer",HeaderCanary);
            using var failed=await client.SendAsync(failure,ct);if(failed.StatusCode!=HttpStatusCode.ServiceUnavailable)throw new InvalidDataException("Controlled failure was not observed.");var operation=failed.Headers.GetValues("X-Operation-Id").Single();if(!OrderContract.Id(operation))throw new InvalidDataException("Missing server-generated operation ID.");
            var problem=await failed.Content.ReadAsStringAsync(ct);foreach(var marker in new[]{QueryCanary,HeaderCanary,BodyCanary,ExceptionCanary})if(problem.Contains(marker,StringComparison.Ordinal))throw new InvalidDataException("Failure response leaked a private canary.");
            string? trace=null;
            for(var i=0;i<40;i++)
            {
                lock(gate)
                {
                    foreach(var line in captured.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries))
                    {
                        try{using var doc=JsonDocument.Parse(line);var r=doc.RootElement;if(r.GetProperty("EventId").GetInt32()!=3502)continue;var state=r.GetProperty("State");if(state.GetProperty("OperationId").GetString()==operation&&state.GetProperty("Area").GetString()=="failure_probe"&&state.GetProperty("Outcome").GetString()=="server_failure")trace=state.GetProperty("TraceId").GetString();}catch(JsonException){}catch(KeyNotFoundException){}
                    }
                }
                if(trace is not null)break;await Task.Delay(100,ct);
            }
            string raw;lock(gate){raw=captured.ToString();if(overflow)throw new InvalidDataException("Staging diagnostic capture exceeded its bound.");}
            foreach(var marker in new[]{QueryCanary,HeaderCanary,BodyCanary,ExceptionCanary,password,oldGrant.Token})if(raw.Contains(marker,StringComparison.Ordinal))throw new InvalidDataException("Diagnostic output leaked a private canary.");
            if(trace is null||trace.Length!=32||!trace.All(Uri.IsHexDigit))throw new InvalidDataException("No correlated safe failure trace.");
            using(var stillLive=await client.GetAsync("health/live",ct))if(stillLive.StatusCode!=HttpStatusCode.OK)throw new InvalidDataException("Recoverable probe stopped the process.");
            Directory.CreateDirectory(Path.GetDirectoryName(evidenceFile)!);await using(var file=new FileStream(evidenceFile,FileMode.CreateNew,FileAccess.Write))await JsonSerializer.SerializeAsync(file,new{schema=1,deployment="published-loopback-https-staging",databaseVersion=CommerceMaintenance.Version,backupRestore="verified-new-directory",restoredGrant="invalidated",paymentReplay="preserved",readiness="held-then-reviewed",controlledFailure=503,operationId=operation,traceId=trace,redaction="canaries-absent",realProviderOrDeviceEvidence=false},cancellationToken:ct);
            Console.WriteLine("Operations staging smoke passed: published pinned-loopback HTTPS, reviewed restore, invalidated grants, preserved replay and redacted correlated failure trace.");
        }
        finally
        {
            if(process is not null){if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));process.Dispose();}
            if(Directory.Exists(work))Directory.Delete(work,true);
        }
    }
    private static (OrderSnapshot,VerifiedCheckoutEvent) Seed(SqliteOrderStore store,OrderOwner owner)
    {
        var q=store.CreateQuote(owner,new SongRequest{Genre="Folk",Occasion="Invented anniversary",Story="SYNTHETIC_CUSTOMER_STORY",ContactName="Sample Reader",Email="reader@example.test"});var o=store.Accept(owner,new(q.Id,Guid.NewGuid().ToString("N"),q.Request));var r=store.ReserveTestCheckout(owner,o.Id,"stripe-test:acct_fixture","https://fixture.example/");store.AttachTestSession(r,new("cs_test_"+r.AttemptId,"https://checkout.stripe.com/c/"+r.AttemptId));
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new{id="evt_lab"+r.AttemptId,@object="event",livemode=false,type="checkout.session.completed",data=new{@object=new{id="cs_test_"+r.AttemptId,@object="checkout.session",mode="payment",livemode=false,client_reference_id=r.AttemptId,metadata=new{order_id=o.Id},currency="usd",amount_total=o.TotalMinor,payment_status="paid",payment_intent="pi_lab"+r.AttemptId}}});
        var t=DateTimeOffset.UtcNow.ToUnixTimeSeconds();var secret="whsec_"+Convert.ToHexString(RandomNumberGenerator.GetBytes(24));var signature="t="+t+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.ASCII.GetBytes(t+".").Concat(bytes).ToArray()));var e=new StripeTestEventVerifier(secret,r.Scope,TimeProvider.System).Verify(bytes,signature)!;
        store.ApplyVerifiedTestEvent(e);store.StartProductionForPaymentLab(o.Id,1);return (store.PublishReviewForLab(o.Id,2),e);
    }
}
