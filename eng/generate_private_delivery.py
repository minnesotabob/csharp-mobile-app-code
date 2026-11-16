"""Original three-second teaching tone. Public source artifact; protected delivery route is an authorization lab."""
from pathlib import Path
import io,wave,math,struct,hashlib
root=Path(__file__).resolve().parents[1]
output=io.BytesIO()
with wave.open(output,'wb') as wav:
 wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(16000)
 wav.writeframes(b''.join(struct.pack('<h',round(8000*math.sin(2*math.pi*(330 if i<16000 else 440 if i<32000 else 550)*i/16000)*min(1,i/400,(48000-i)/400))) for i in range(48000)))
raw=output.getvalue();(root/'src/GnazzosWorld.Api/Fixtures/delivery_sample.wav').write_bytes(raw)
(root/'src/GnazzosWorld.Core/Fulfillment/PrivateDeliveryFixture.cs').write_text('namespace GnazzosWorld.Core.Fulfillment;\n\n/// <summary>Original synthetic sample only; never a customer file descriptor authority.</summary>\npublic static class PrivateDeliveryFixture\n{\n    public const long Length='+str(len(raw))+';\n    public const string Sha256="'+hashlib.sha256(raw).hexdigest()+'";\n}\n')
print(len(raw),hashlib.sha256(raw).hexdigest())
