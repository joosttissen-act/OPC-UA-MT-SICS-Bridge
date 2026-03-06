# OPC UA → MT-SICS TCP/IP Bridge (C#)

## Overview

This project implements a **protocol bridge** that allows a **MES system that only supports MT-SICS over TCP/IP** to communicate with a **scale that only exposes an OPC UA interface**.

The bridge translates **incoming MT-SICS commands** from the MES system into **OPC UA read/write operations** on the scale, and returns responses formatted according to the **MT-SICS protocol**.

Architecture:

```
MES System (MT-SICS TCP/IP)
        │
        │  TCP Socket
        ▼
MT-SICS Server (Bridge - C#)
        │
        │  Command Translator
        ▼
OPC UA Client
        │
        ▼
Scale (OPC UA only)
```

The bridge acts as:

* **TCP server** for MT-SICS
* **OPC UA client** for the scale
* **Command translator** between the two protocols

---

# Goals

* Allow MES to execute **MT-SICS commands**
* Translate commands into **OPC UA node interactions**
* Return responses formatted exactly as **MT-SICS**
* Maintain **low latency** (<100 ms typical)
* Ensure **stable connection to scale**

---

# Technology Stack

Language: **C# (.NET 8 or newer)**

Libraries:

* `OPCFoundation.NetStandard.Opc.Ua`
* `System.Net.Sockets`
* `Microsoft.Extensions.Hosting`
* `Microsoft.Extensions.Logging`

Project type:

```
.NET Worker Service
```

---

# Project Structure

```
/src
  /Bridge
      Program.cs
      MtSicsTcpServer.cs
      MtSicsCommandParser.cs
      MtSicsResponseFormatter.cs
      OpcUaScaleClient.cs
      ScaleService.cs
      CommandTranslator.cs
/config
      scale_nodes.json
/docs
      instructions.md
```

---

# Responsibilities

## MT-SICS TCP Server

File: `MtSicsTcpServer.cs`

Responsibilities:

* Listen for incoming TCP connections
* Accept commands terminated by CR/LF
* Forward commands to command translator
* Return formatted MT-SICS responses

Port (default):

```
8001
```

Connection model:

```
MES → TCP Socket → Bridge
```

Basic skeleton:

```csharp
var listener = new TcpListener(IPAddress.Any, 8001);
listener.Start();

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = HandleClient(client);
}
```

---

# MT-SICS Commands To Support

The MES system will primarily use the following commands.

## Read Stable Weight

Command:

```
SI
```

Meaning:

```
Send Immediate weight
```

Bridge logic:

1. Read OPC UA node for weight
2. Read OPC UA node for stability
3. Format MT-SICS response

Example response:

```
S S      1.234 kg
```

---

## Read Weight (unstable allowed)

Command:

```
S
```

Bridge logic:

1. Read weight node
2. Return value without stability check

Example response:

```
S D      1.235 kg
```

---

## Tare

Command:

```
T
```

Bridge logic:

1. Write `true` to OPC UA tare node
2. Wait for operation completion
3. Return success response

Example response:

```
T S
```

---

# OPC UA Client

File: `OpcUaScaleClient.cs`

Responsibilities:

* Maintain persistent OPC UA session
* Read weight nodes
* Write tare command
* Handle reconnects

Use **session keepalive**.

Example structure:

```csharp
public class OpcUaScaleClient
{
    public async Task<double> ReadWeightAsync()
    {
        var value = await _session.ReadValueAsync(_weightNode);
        return Convert.ToDouble(value.Value);
    }

    public async Task<bool> IsStableAsync()
    {
        var value = await _session.ReadValueAsync(_stableNode);
        return (bool)value.Value;
    }

    public async Task TareAsync()
    {
        await _session.WriteValueAsync(_tareNode, true);
    }
}
```

---

# OPC UA Node Configuration

All OPC UA node IDs must be configurable.

File:

```
config/scale_nodes.json
```

Example:

```json
{
  "WeightNode": "ns=2;s=Scale.Weight",
  "StabilityNode": "ns=2;s=Scale.Stable",
  "TareNode": "ns=2;s=Scale.Tare"
}
```

Never hardcode node IDs.

---

# Command Translator

File:

```
CommandTranslator.cs
```

Purpose:

Convert MT-SICS commands into OPC UA operations.

Example:

```csharp
public async Task<string> HandleCommand(string cmd)
{
    switch(cmd.Trim())
    {
        case "SI":
            return await HandleImmediateWeight();

        case "S":
            return await HandleWeight();

        case "T":
            return await HandleTare();

        default:
            return "I"; // invalid command
    }
}
```

---

# Response Formatter

File:

```
MtSicsResponseFormatter.cs
```

Responsibilities:

* Ensure MT-SICS compliant formatting
* Ensure fixed spacing
* Append CR/LF

Example:

```csharp
public static string FormatWeight(double weight, bool stable)
{
    var status = stable ? "S" : "D";
    return $"S {status} {weight,10:0.000} kg\r\n";
}
```

---

# Connection Reliability

The OPC UA client must:

* Automatically reconnect
* Retry reads
* Detect session drops

Recommended strategy:

```
Reconnect delay: 5 seconds
Retry read: 3 times
```

---

# Logging

Use structured logging.

Examples:

```
[INFO] MES connected
[INFO] Received command: SI
[INFO] Weight read: 1.234 kg
[ERROR] OPC UA connection lost
```

---

# Performance Considerations

* Do **not reconnect OPC UA per command**
* Maintain persistent session
* Avoid blocking calls
* Use async operations

---

# Error Handling

If OPC UA read fails:

Return MT-SICS error:

```
S I
```

Meaning:

```
Command understood but execution failed
```

---

# Testing

Test with:

### TCP client

```
telnet localhost 8001
```

Commands:

```
SI
S
T
```

Expected responses:

```
S S      1.234 kg
S D      1.234 kg
T S
```

---

# Future Extensions

Possible improvements:

* Support additional MT-SICS commands
* Multi-scale support
* Docker deployment
* Prometheus metrics
* OPC UA subscription instead of polling

---

# Important Implementation Notes

1. **MT-SICS protocol is line based**

Commands end with:

```
CR LF
```

2. Responses must follow exact format.

3. Only **ASCII encoding**.

4. Do not send additional text.

---

# Example Full Flow

MES sends:

```
SI
```

Bridge:

```
Read Weight → OPC UA
Read Stability → OPC UA
```

Bridge returns:

```
S S      0.542 kg
```

---

# Definition of Done

The project is complete when:

* MES can connect via TCP
* `SI`, `S`, and `T` commands work
* Weight is read from OPC UA
* Tare command writes to OPC UA
* Responses conform to MT-SICS format
* Bridge reconnects automatically to scale
