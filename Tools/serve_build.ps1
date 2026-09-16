# Локальный сервер для проверки WebGL-билда с Brotli без Decompression Fallback.
# Отдаёт .br с Content-Encoding: br, иначе загрузчик Unity откажется читать файлы.
param(
    [string]$Root = "C:\Users\diman\Desktop\strelki_build",
    [int]$Port = 8099
)

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "serving $Root on http://localhost:$Port/"

$mime = @{
    ".html" = "text/html";  ".js" = "application/javascript"; ".css" = "text/css"
    ".wasm" = "application/wasm"; ".data" = "application/octet-stream"
    ".json" = "application/json"; ".png" = "image/png"; ".ico" = "image/x-icon"
}

while ($listener.IsListening) {
    try {
        $ctx = $listener.GetContext()
        $rel = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath).TrimStart('/')
        if ([string]::IsNullOrEmpty($rel)) { $rel = "index.html" }
        $path = Join-Path $Root $rel

        if (-not (Test-Path $path -PathType Leaf)) {
            $ctx.Response.StatusCode = 404
            $ctx.Response.Close()
            continue
        }

        $name = [IO.Path]::GetFileName($path)
        $ext = [IO.Path]::GetExtension($path)
        if ($ext -eq ".br") {
            $ctx.Response.AddHeader("Content-Encoding", "br")
            $ext = [IO.Path]::GetExtension($name.Substring(0, $name.Length - 3))
        }

        $type = $mime[$ext]
        if (-not $type) { $type = "application/octet-stream" }
        $ctx.Response.ContentType = $type

        $bytes = [IO.File]::ReadAllBytes($path)
        $ctx.Response.ContentLength64 = $bytes.Length
        $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        $ctx.Response.OutputStream.Close()
    }
    catch {
        Write-Host "err: $_"
    }
}
