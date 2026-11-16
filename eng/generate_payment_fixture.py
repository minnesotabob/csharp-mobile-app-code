"""Original synthetic raw-body signature vector, independently produced with Python."""
from pathlib import Path
import json,hmac,hashlib,datetime
root=Path(__file__).resolve().parents[1]/'tests/GnazzosWorld.Core.Tests/Fixtures'
e={'id':'evt_pythonfixture','object':'event','livemode':False,'type':'checkout.session.completed','data':{'object':{'id':'cs_test_pythonfixture','object':'checkout.session','mode':'payment','livemode':False,'client_reference_id':'a'*32,'metadata':{'order_id':'b'*32},'currency':'usd','amount_total':17900,'payment_status':'paid','payment_intent':'pi_pythonfixture'}}}
raw=(json.dumps(e,indent=2)+'\n').encode();t=int(datetime.datetime(2026,11,16,6,tzinfo=datetime.timezone.utc).timestamp())
sig=hmac.new(b'whsec_synthetic_fixture_secret_0123456789',str(t).encode()+b'.'+raw,hashlib.sha256).hexdigest()
(root/'payment-event.json').write_bytes(raw);(root/'payment-event.signature.txt').write_text(f't={t},v1={sig}\n')
