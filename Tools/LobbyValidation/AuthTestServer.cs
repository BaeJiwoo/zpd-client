using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

// Test-only HTTP peer. Nothing in this fixture is shipped in Assets.
public sealed class AuthTestServer : IDisposable
{
    public const string Password = " test-password ";
    public sealed class Request { public string Path, Method, Body, Authorization, Key; }
    readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
    public readonly ConcurrentQueue<Request> Requests = new ConcurrentQueue<Request>();
    public volatile int LoginStatus = 200, LoginDelay, ProfileStatus = 200, ProfileDelay;
    public volatile string LoginBody, ProfileBody;
    public string Root { get; }
    public AuthTestServer()
    {
        listener.Start(); Root = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/api/v1";
        _ = Task.Run(async () => {
            try { while (true) { var client = await listener.AcceptTcpClientAsync(); _ = Serve(client); } }
            catch (SocketException) { } catch (ObjectDisposedException) { }
        });
    }
    async Task Serve(TcpClient client)
    {
        using(client)
        try
        {
            var stream = client.GetStream();
            var bytes = new MemoryStream(); int previous = 0, matched = 0;
            // Read bytes, not chars: Content-Length is measured in UTF-8 bytes.
            while(matched < 4)
            {
                int value = stream.ReadByte(); if(value < 0) return; bytes.WriteByte((byte)value);
                matched = value == (matched % 2 == 0 ? 13 : 10) ? matched + 1 : 0;
                previous++; if(previous > 32768) return;
            }
            string headers = Encoding.UTF8.GetString(bytes.ToArray()); var lines = headers.Split(new[]{"\r\n"},StringSplitOptions.None);
            var first = lines[0].Split(' '); var request = new Request { Method=first[0], Path=first[1] }; int length=0;
            foreach(var line in lines)
            {
                if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) length=int.Parse(line.Substring(15).Trim());
                if(line.StartsWith("Authorization:",StringComparison.OrdinalIgnoreCase)) request.Authorization=line.Substring(14).Trim();
                if(line.StartsWith("Idempotency-Key:",StringComparison.OrdinalIgnoreCase)) request.Key=line.Substring(16).Trim();
            }
            var body = new byte[length]; int offset=0;
            while(offset<length) { int count=await stream.ReadAsync(body,offset,length-offset); if(count==0) return; offset+=count; }
            request.Body=Encoding.UTF8.GetString(body); Requests.Enqueue(request);
            int status=200, delay=0; string json;
            if(request.Path.EndsWith("/auth/login"))
            {
                // IDs in this fixture are ASCII; production parsing remains Unity JsonUtility on the main thread.
                string id=request.Body.Split('"')[3]; status=request.Body.Contains("\"password\":\"" + Password + "\"") ? LoginStatus : 401; delay=LoginDelay;
                json=LoginBody ?? "{\"data\":{\"playerId\":\""+id+"\",\"accessToken\":\"token-"+id+"\",\"expiresAtUtc\":\""+DateTime.UtcNow.AddHours(1).ToString("O")+"\"}}";
            }
            else if(request.Authorization == null) { status=401; json="{}"; }
            else if(request.Path.EndsWith("/me"))
            {
                status=ProfileStatus; delay=ProfileDelay;
                json=ProfileBody ?? "{\"data\":{\"revision\":\"1\",\"displayName\":\"API Player\",\"level\":1,\"stats\":{\"wins\":0,\"losses\":0},\"recentMatches\":[]}}";
            }
            else if(request.Path.EndsWith("/me/inventory")) json="{\"data\":{\"revision\":\"1\",\"items\":[]}}";
            else
            {
                string runId = request.Body.Split(new[]{"\"runId\":\""},StringSplitOptions.None)[1].Split('"')[0];
                json=request.Path.EndsWith("/rewards") ? "{\"status\":\"settled\",\"runId\":\""+runId+"\",\"settlementId\":\"test-settlement\",\"earnedExperience\":1}" :
                    "{\"status\":\"stored\",\"runId\":\""+runId+"\",\"resultId\":\"test-result\"}";
            }
            if(delay>0) await Task.Delay(delay);
            var output=Encoding.UTF8.GetBytes(json);
            var header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" Result\r\nContent-Type: application/json\r\nContent-Length: "+output.Length+"\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header,0,header.Length); await stream.WriteAsync(output,0,output.Length);
        }
        catch(IOException) { } catch(ObjectDisposedException) { }
    }
    public void Dispose() { listener.Stop(); }
}
