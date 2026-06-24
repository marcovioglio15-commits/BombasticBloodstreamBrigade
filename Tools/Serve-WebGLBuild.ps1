param(
    [string]$BuildPath = "Builds/WebGL/BombasticBloodstreamBrigade_Prototype_WebGLBuild",
    [int]$Port = 8123
)

$serverSource = @'
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public sealed class WebGlStaticServer : IDisposable
{
    private readonly string rootPath;
    private readonly string rootPrefix;
    private readonly TcpListener listener;
    private Thread acceptThread;
    private volatile bool running;

    public WebGlStaticServer(string rootPath, int port)
    {
        this.rootPath = Path.GetFullPath(rootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        rootPrefix = this.rootPath + Path.DirectorySeparatorChar;
        listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start()
    {
        listener.Start();
        running = true;
        acceptThread = new Thread(AcceptLoop);
        acceptThread.IsBackground = true;
        acceptThread.Name = "Bombastic WebGL local server";
        acceptThread.Start();
    }

    public void Stop()
    {
        running = false;
        listener.Stop();

        if (acceptThread != null && acceptThread.IsAlive)
        {
            acceptThread.Join(2000);
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void AcceptLoop()
    {
        while (running)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(HandleClient, client);
            }
            catch (SocketException)
            {
                if (running)
                {
                    throw;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void HandleClient(object state)
    {
        using (TcpClient client = (TcpClient)state)
        {
            client.ReceiveTimeout = 10000;
            client.SendTimeout = 60000;

            try
            {
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(
                    stream,
                    Encoding.ASCII,
                    false,
                    1024,
                    true))
                {
                    string requestLine = reader.ReadLine();
                    string headerLine;

                    do
                    {
                        headerLine = reader.ReadLine();
                    }
                    while (headerLine != null && headerLine.Length > 0);

                    if (String.IsNullOrWhiteSpace(requestLine))
                    {
                        return;
                    }

                    string[] requestParts = requestLine.Split(' ');

                    if (requestParts.Length < 2)
                    {
                        SendTextResponse(stream, 400, "Bad Request", "Bad request", true);
                        return;
                    }

                    string method = requestParts[0].ToUpperInvariant();

                    if (method != "GET" && method != "HEAD")
                    {
                        SendTextResponse(
                            stream,
                            405,
                            "Method Not Allowed",
                            "Method not allowed",
                            true);
                        return;
                    }

                    string requestPath = requestParts[1];
                    int queryIndex = requestPath.IndexOf('?');

                    if (queryIndex >= 0)
                    {
                        requestPath = requestPath.Substring(0, queryIndex);
                    }

                    string relativePath = Uri.UnescapeDataString(
                        requestPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                    if (String.IsNullOrWhiteSpace(relativePath))
                    {
                        relativePath = "index.html";
                    }

                    string fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));

                    if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(fullPath))
                    {
                        SendTextResponse(
                            stream,
                            404,
                            "Not Found",
                            "Not found",
                            method != "HEAD");
                        return;
                    }

                    SendFileResponse(stream, fullPath, method != "HEAD");
                }
            }
            catch (IOException)
            {
                // Browsers can cancel speculative or superseded requests.
            }
            catch (SocketException)
            {
                // The client closed the connection before the response completed.
            }
        }
    }

    private static void SendFileResponse(
        NetworkStream stream,
        string fullPath,
        bool sendBody)
    {
        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        string logicalPath = fullPath;
        string contentEncoding = null;

        if (extension == ".gz")
        {
            contentEncoding = "gzip";
            logicalPath = Path.GetFileNameWithoutExtension(fullPath);
            extension = Path.GetExtension(logicalPath).ToLowerInvariant();
        }
        else if (extension == ".br")
        {
            contentEncoding = "br";
            logicalPath = Path.GetFileNameWithoutExtension(fullPath);
            extension = Path.GetExtension(logicalPath).ToLowerInvariant();
        }

        FileInfo file = new FileInfo(fullPath);
        WriteHeaders(
            stream,
            200,
            "OK",
            GetContentType(extension),
            contentEncoding,
            file.Length);

        if (!sendBody)
        {
            return;
        }

        using (FileStream fileStream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65536,
            FileOptions.SequentialScan))
        {
            fileStream.CopyTo(stream, 65536);
        }
    }

    private static void SendTextResponse(
        NetworkStream stream,
        int statusCode,
        string reason,
        string body,
        bool sendBody)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        WriteHeaders(
            stream,
            statusCode,
            reason,
            "text/plain; charset=utf-8",
            null,
            bodyBytes.Length);

        if (sendBody)
        {
            stream.Write(bodyBytes, 0, bodyBytes.Length);
        }
    }

    private static void WriteHeaders(
        NetworkStream stream,
        int statusCode,
        string reason,
        string contentType,
        string contentEncoding,
        long contentLength)
    {
        StringBuilder headers = new StringBuilder();
        headers.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reason).Append("\r\n");
        headers.Append("Content-Type: ").Append(contentType).Append("\r\n");
        headers.Append("Content-Length: ").Append(contentLength).Append("\r\n");
        headers.Append("Cache-Control: no-cache\r\n");
        headers.Append("Connection: close\r\n");

        if (!String.IsNullOrWhiteSpace(contentEncoding))
        {
            headers.Append("Content-Encoding: ").Append(contentEncoding).Append("\r\n");
        }

        headers.Append("\r\n");
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers.ToString());
        stream.Write(headerBytes, 0, headerBytes.Length);
    }

    private static string GetContentType(string extension)
    {
        switch (extension)
        {
            case ".html":
                return "text/html; charset=utf-8";
            case ".js":
                return "application/javascript";
            case ".wasm":
                return "application/wasm";
            case ".data":
                return "application/octet-stream";
            case ".json":
                return "application/json";
            case ".css":
                return "text/css";
            case ".png":
                return "image/png";
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".ico":
                return "image/x-icon";
            default:
                return "application/octet-stream";
        }
    }
}
'@

if ($null -eq ("WebGlStaticServer" -as [type])) {
    Add-Type -TypeDefinition $serverSource -Language CSharp
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$resolvedBuildPath = if ([System.IO.Path]::IsPathRooted($BuildPath)) {
    $BuildPath
}
else {
    Join-Path $projectRoot $BuildPath
}

$rootPath = [System.IO.Path]::GetFullPath(
    (Resolve-Path -LiteralPath $resolvedBuildPath).Path)
$server = [WebGlStaticServer]::new($rootPath, $Port)
$server.Start()

Write-Host "Serving $rootPath at http://localhost:$Port/"
Write-Host "Press Ctrl+C to stop."

try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    $server.Stop()
}
