$port = 8094
  $endpoint = New-Object System.Net.IPEndPoint ([IPAddress]::Any, $port)
  Try {
      while($true) {
          $socket = New-Object System.Net.Sockets.UdpClient $port
          $content = $socket.Receive([ref]$endpoint)
          $socket.Close()
          [Text.Encoding]::ASCII.GetString($content)
      }
  } Catch {
      "$($Error[0])"
  }