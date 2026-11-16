using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Fulfillment;
using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Payments;

// LOCAL SYNTHETIC FIXTURE ONLY. Direct database access is trusted developer authority.
// Never target customer/production data; this is not an authenticated admin service.
var clock=TimeProvider.System;
if(args is ["smoke"])
{
    var folder=Path.Combine(Path.GetTempPath(),"gnazzo-fulfillment-smoke-"+Guid.NewGuid().ToString("N"));
    try
    {
        using var store=new SqliteOrderStore(Path.Combine(folder,"synthetic.db"),clock);var owner=new OrderOwner("https://fixture.auth0.com/","reader-a");var review=Seed(store,owner);
        var revision=store.RequestRevision(owner,review.Id,new(review.Version,Guid.NewGuid().ToString("N"),"Invented lyric adjustment"));
        store.PublishReviewForLab(review.Id,revision.AppliedVersion);var d=store.PublishDeliveryForLab(review.Id,revision.AppliedVersion+1);var grant=store.IssueDeliveryGrant(owner,review.Id,d.Id,d.Version);
        if(store.AuthorizeDelivery(owner,review.Id,d.Id,grant.Token)!=d||store.TrackOrder(owner,review.Id).Notices[^1].Kind!=OrderNoticeKind.Delivered)throw new InvalidDataException("Synthetic journey failed.");
        Console.WriteLine("Synthetic fulfillment smoke passed: test-paid review, revision, delivery and owned grant. No provider/admin/listening claim.");
    }
    finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    return;
}
if(Environment.GetEnvironmentVariable("GNAZZO_SYNTHETIC_FIXTURES")!="1")throw new InvalidOperationException("Direct fixture writes require GNAZZO_SYNTHETIC_FIXTURES=1 and a disposable synthetic database.");
if(args.Length<4){Console.Error.WriteLine("SYNTHETIC ONLY: seed-review DB ISSUER SUBJECT | review DB ORDER VERSION | deliver DB ORDER VERSION | revoke DB ORDER DELIVERY VERSION | smoke");Environment.ExitCode=2;return;}
using(var store=new SqliteOrderStore(args[1],clock))
{
    switch(args[0])
    {
        case "seed-review" when args.Length==4:
            var review=Seed(store,new(args[2],args[3]));Console.WriteLine($"Synthetic order {review.Id}: {review.State}, v{review.Version}.");break;
        case "review" when args.Length==4:
            var next=store.PublishReviewForLab(args[2],long.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture));Console.WriteLine($"Synthetic order {next.Id}: {next.State}, v{next.Version}.");break;
        case "deliver" when args.Length==4:
            var delivery=store.PublishDeliveryForLab(args[2],long.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture));Console.WriteLine($"Synthetic order {delivery.OrderId}: delivery {delivery.Id}, v{delivery.Version}.");break;
        case "revoke" when args.Length==5:
            store.RevokeDeliveryForLab(args[2],args[3],long.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture));Console.WriteLine("Synthetic delivery revoked for future reads; no received-copy erasure claim.");break;
        default:Console.Error.WriteLine("Invalid synthetic fixture arguments.");Environment.ExitCode=2;break;
    }
}
OrderSnapshot Seed(SqliteOrderStore store,OrderOwner owner)
{
    var quote=store.CreateQuote(owner,new SongRequest{Genre="Folk",Occasion="Invented anniversary",Story="Synthetic fixture story only.",ContactName="Sample Reader",Email="reader@example.test"});
    var order=store.Accept(owner,new(quote.Id,Guid.NewGuid().ToString("N"),quote.Request));var r=store.ReserveTestCheckout(owner,order.Id,"stripe-test:acct_fixture","https://fixture.example/");
    store.AttachTestSession(r,new("cs_test_"+r.AttemptId,"https://checkout.stripe.com/c/"+r.AttemptId));
    var raw=JsonSerializer.SerializeToUtf8Bytes(new{id="evt_lab"+r.AttemptId,@object="event",livemode=false,type="checkout.session.completed",data=new{@object=new{id="cs_test_"+r.AttemptId,@object="checkout.session",mode="payment",livemode=false,client_reference_id=r.AttemptId,metadata=new{order_id=order.Id},currency="usd",amount_total=order.TotalMinor,payment_status="paid",payment_intent="pi_lab"+r.AttemptId}}});
    var timestamp=clock.GetUtcNow().ToUnixTimeSeconds();var secret="whsec_"+Convert.ToHexString(RandomNumberGenerator.GetBytes(24));var signature="t="+timestamp+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.ASCII.GetBytes(timestamp+".").Concat(raw).ToArray()));
    store.ApplyVerifiedTestEvent(new StripeTestEventVerifier(secret,r.Scope,clock).Verify(raw,signature)!);store.StartProductionForPaymentLab(order.Id,1);return store.PublishReviewForLab(order.Id,2);
}
